// SPDX-License-Identifier: LGPL-3.0-or-later
using System.Net;
using System.Text.Json;
using Osme.Core.Access;

namespace Osme.Host;

/// <summary>Who is calling: the session of the request and its account.</summary>
public sealed record Caller(Session Session, Account Account, string Token);

/// <summary>
/// The gate every /api request goes through, and the routes of the initial setup and of the
/// session (change 0-2-1, decisions 3, 4, 12 and 13).
/// </summary>
public sealed class AccessApi(
    InitialSetup setup, Sessions sessions, SignIn signIn, Accounts accounts, InstallationCode? installationCode)
{
    public const string CookieName = "__Host-osme_session";

    private const string SessionPath = "/api/session";
    private const string PasswordPath = "/api/session/password";
    private const string SetupPath = "/api/setup";
    private static readonly object CallerKey = new();

    // A company never goes away, so a yes is remembered; a no is asked again.
    private volatile bool _isSetUp;

    public static Caller CallerOf(HttpContext context) =>
        context.Items[CallerKey] as Caller ?? throw new InvalidOperationException("This route runs behind the access gate.");

    /// <summary>For the routes of a view of the core: without the view the answer is "forbidden", with no data.</summary>
    public static IResult? RefuseWithout(HttpContext context, string view) =>
        CallerOf(context).Account.Can(view) ? null : Api.Fail(StatusCodes.Status403Forbidden, "forbidden");

    public static async Task<JsonDocument?> ReadObjectAsync(HttpRequest request, CancellationToken cancel)
    {
        try
        {
            var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancel);
            if (document.RootElement.ValueKind == JsonValueKind.Object) return document;
            document.Dispose();
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static IResult InvalidBody() => Api.Fail(StatusCodes.Status400BadRequest, "invalid_body");

    /// <summary>Nothing under /api answers before this has let the request through.</summary>
    public void UseGate(WebApplication app) => app.Use(async (context, next) =>
    {
        var request = context.Request;
        if (!request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        var method = request.Method;
        var path = request.Path.Value!.TrimEnd('/');
        var reads = HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);

        // Cross-site request forgery: a write must come from this server's own pages, as JSON.
        if (!reads)
        {
            if (!HasOwnOrigin(request))
            {
                await Api.Fail(StatusCodes.Status403Forbidden, "invalid_origin").ExecuteAsync(context);
                return;
            }
            if (!HttpMethods.IsDelete(method) && !request.HasJsonContentType())
            {
                await Api.Fail(StatusCodes.Status400BadRequest, "invalid_body").ExecuteAsync(context);
                return;
            }
        }

        var isState = HttpMethods.IsGet(method) && Is(path, SessionPath);
        var isSignIn = HttpMethods.IsPost(method) && Is(path, SessionPath);
        var isSetup = HttpMethods.IsPost(method) && Is(path, SetupPath);

        if (!await IsSetUpAsync(context.RequestAborted))
        {
            if (isState || isSetup) await next(context);
            else await Api.Fail(StatusCodes.Status409Conflict, "setup_required").ExecuteAsync(context);
            return;
        }

        // These three work without a session and look at it themselves.
        if (isState || isSignIn || isSetup)
        {
            await next(context);
            return;
        }

        if (await ResolveAsync(context) is not { } caller)
        {
            await Api.Fail(StatusCodes.Status401Unauthorized, "unauthenticated").ExecuteAsync(context);
            return;
        }

        // With a temporary password the only things left are leaving and changing it.
        var isSignOut = HttpMethods.IsDelete(method) && Is(path, SessionPath);
        var isPasswordChange = HttpMethods.IsPost(method) && Is(path, PasswordPath);
        if (caller.Account.MustChangePassword && !isSignOut && !isPasswordChange)
        {
            await Api.Fail(StatusCodes.Status403Forbidden, "password_change_required").ExecuteAsync(context);
            return;
        }

        context.Items[CallerKey] = caller;
        await next(context);
    });

    public void MapRoutes(WebApplication app)
    {
        app.MapGet(SessionPath, async (HttpContext context, CancellationToken cancel) =>
        {
            if (!await IsSetUpAsync(cancel))
                return Api.Ok(new { State = "setup_required", CodeRequired = !IsServerMachine(context) });
            return await ResolveAsync(context) is { } caller ? Api.Ok(SignedIn(caller.Account)) : Api.Ok(SignedOut);
        });

        app.MapPost(SetupPath, async (HttpContext context, CancellationToken cancel) =>
        {
            if (await IsSetUpAsync(cancel)) return SetupDone();
            if (await ReadObjectAsync(context.Request, cancel) is not { } document) return InvalidBody();
            using (document)
            {
                var body = document.RootElement;

                // From any other machine the installation code comes first, before looking at the data.
                if (!IsServerMachine(context))
                {
                    var check = installationCode?.Check(Text(body, "code")) ?? CodeCheck.Exhausted;
                    if (check == CodeCheck.Exhausted) return Api.Fail(StatusCodes.Status403Forbidden, "code_attempts_exhausted");
                    if (check != CodeCheck.Accepted) return Api.Fail(StatusCodes.Status403Forbidden, "invalid_code");
                }

                var result = await setup.RunAsync(
                    Text(body, "company_name"), Text(body, "name"), Text(body, "username"), Text(body, "password"), cancel);
                if (result.Status == SetupStatus.AlreadyDone) return SetupDone();
                if (result.Status == SetupStatus.Invalid)
                    return Api.Fail(StatusCodes.Status422UnprocessableEntity, "validation", result.Errors);

                // The administrator stays inside, without typing the password again.
                _isSetUp = true;
                return await EnterAsync(
                    context, await sessions.CreateAsync(result.UserAccountId, cancel), result.UserAccountId,
                    StatusCodes.Status201Created, cancel);
            }
        });

        app.MapPost(SessionPath, async (HttpContext context, CancellationToken cancel) =>
        {
            if (await ReadObjectAsync(context.Request, cancel) is not { } document) return InvalidBody();
            using (document)
            {
                var body = document.RootElement;
                return await signIn.SignInAsync(Text(body, "username"), Text(body, "password"), cancel) is { } entered
                    ? await EnterAsync(context, entered.Token, entered.UserAccountId, StatusCodes.Status200OK, cancel)
                    : Api.Fail(StatusCodes.Status401Unauthorized, "invalid_credentials");
            }
        });

        app.MapDelete(SessionPath, async (HttpContext context, CancellationToken cancel) =>
        {
            await sessions.CloseAsync(CallerOf(context).Token, cancel);
            context.Response.Cookies.Delete(CookieName, Cookie(maxAge: null));
            return Api.Ok(SignedOut);
        });

        app.MapPost(PasswordPath, async (HttpContext context, CancellationToken cancel) =>
        {
            if (await ReadObjectAsync(context.Request, cancel) is not { } document) return InvalidBody();
            using (document)
            {
                var body = document.RootElement;
                var caller = CallerOf(context);
                var errors = await accounts.ChangeOwnPasswordAsync(
                    caller.Account.Id, caller.Session.Id, Text(body, "current_password"), Text(body, "password"), cancel);
                return errors.Count > 0
                    ? Api.Fail(StatusCodes.Status422UnprocessableEntity, "validation", errors)
                    : Api.Ok(SignedIn((await accounts.FindAsync(caller.Account.Id, cancel))!));
            }
        });
    }

    private static readonly object SignedOut = new { State = "signed_out" };

    private static object SignedIn(Account account) => new
    {
        State = "signed_in",
        Account = new { account.Id, account.Username, account.Name, account.MustChangePassword },
        account.IsAdministrator,
        account.Roles,
        account.Permissions,
    };

    private static IResult SetupDone() => Api.Fail(StatusCodes.Status409Conflict, "setup_done");

    private async Task<IResult> EnterAsync(HttpContext context, string token, Guid accountId, int status, CancellationToken cancel)
    {
        context.Response.Cookies.Append(CookieName, token, Cookie(Sessions.TotalLimit));
        return Api.Ok(SignedIn((await accounts.FindAsync(accountId, cancel))!), status);
    }

    private static CookieOptions Cookie(TimeSpan? maxAge) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        MaxAge = maxAge,
    };

    private async ValueTask<bool> IsSetUpAsync(CancellationToken cancel)
    {
        if (!_isSetUp && await setup.IsDoneAsync(cancel)) _isSetUp = true;
        return _isSetUp;
    }

    /// <summary>The open session of the request with its account, or null. A deactivated account has none.</summary>
    private async Task<Caller?> ResolveAsync(HttpContext context)
    {
        if (!context.Request.Cookies.TryGetValue(CookieName, out var token) || string.IsNullOrEmpty(token)) return null;
        if (await sessions.ResolveAsync(token, context.RequestAborted) is not { } session) return null;
        return await accounts.FindAsync(session.UserAccountId, context.RequestAborted) is { IsActive: true } account
            ? new Caller(session, account, token)
            : null;
    }

    /// <summary>
    /// The machine the server runs on: a loopback connection that nobody forwarded. Behind a
    /// proxy that announces itself, every caller counts as another machine.
    /// </summary>
    private static bool IsServerMachine(HttpContext context) =>
        context.Connection.RemoteIpAddress is { } address
        && IPAddress.IsLoopback(address)
        && !context.Request.Headers.ContainsKey("X-Forwarded-For")
        && !context.Request.Headers.ContainsKey("Forwarded");

    private static bool HasOwnOrigin(HttpRequest request) =>
        request.Headers.Origin is { Count: 1 } origin
        && string.Equals(origin[0], $"{request.Scheme}://{request.Host.Value}", StringComparison.OrdinalIgnoreCase);

    private static bool Is(string path, string route) => string.Equals(path, route, StringComparison.OrdinalIgnoreCase);

    /// <summary>A text of the body; whatever is missing or is not a text counts as empty.</summary>
    private static string Text(JsonElement body, string name) =>
        body.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : "";
}
