// SPDX-License-Identifier: LGPL-3.0-or-later
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Osme.Tests;

/// <summary>Signing in, signing out and changing the own password, through the real server (specs/sesion-y-cuentas).</summary>
public class SessionApiTests
{
    private const string Session = "/api/session";
    private const string PasswordRoute = "/api/session/password";
    private const string Tickets = "/api/docs/itdesk.ticket";

    // Asks for a session and nothing else: what an account without roles can still call.
    private const string Packages = "/api/packages";
    private const string Right = OsmeApp.Password;
    private const string Wrong = "Abcdef1?";
    private const string Fresh = "Qwerty2#";

    /// <summary>A server that already has its company and administrator, and nobody signed in.</summary>
    private static async Task<(TestDatabase Database, OsmeApp App)> StartAsync()
    {
        var database = await TestDatabase.CreateAsync();
        var app = new OsmeApp(database.ConnectionString, Repo.Packages);
        await app.AdministratorAsync();
        return (database, app);
    }

    private static string? CodeOf((HttpStatusCode Status, JsonElement Body) response) =>
        response.Body.GetProperty("error").GetProperty("code").GetString();

    private static string? StateOf((HttpStatusCode Status, JsonElement Body) response) =>
        response.Body.GetProperty("data").GetProperty("state").GetString();

    private static IEnumerable<(string?, string?)> FieldsOf((HttpStatusCode Status, JsonElement Body) response) =>
        response.Body.GetProperty("error").GetProperty("fields").EnumerateArray()
            .Select(e => (e.GetProperty("field").GetString(), e.GetProperty("code").GetString()));

    private static Task<(HttpStatusCode Status, JsonElement Body)> ChangeAsync(HttpClient browser, string current, string password) =>
        ApiTests.Send(browser, HttpMethod.Post, PasswordRoute, new { current_password = current, password });

    private static void AssertUnauthenticated((HttpStatusCode Status, JsonElement Body) response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.Status);
        Assert.Equal("""{"ok":false,"data":null,"error":{"code":"unauthenticated","fields":null}}""", response.Body.GetRawText());
    }

    [Fact]
    public async Task The_right_username_and_password_get_in_and_the_answer_names_the_account_and_its_roles()
    {
        var (database, app) = await StartAsync();
        await using var _ = database; await using var __ = app;
        var browser = app.Browser();

        using var response = await browser.PostAsJsonAsync(Session, new { username = "  ADMIN ", password = Right });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("__Host-osme_session=", Assert.Single(response.Headers.GetValues("Set-Cookie")));
        var data = body.GetProperty("data");
        Assert.Equal(("signed_in", OsmeApp.AdministratorName, true), (
            data.GetProperty("state").GetString(),
            data.GetProperty("account").GetProperty("name").GetString(),
            data.GetProperty("isAdministrator").GetBoolean()));
        Assert.Equal("core.administrator", Assert.Single(data.GetProperty("roles").EnumerateArray()).GetProperty("name").GetString());
        Assert.DoesNotContain(Right, body.GetRawText());
        Assert.DoesNotContain("hash", body.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, (await ApiTests.Send(browser, HttpMethod.Get, Tickets)).Status);
    }

    [Fact]
    public async Task A_wrong_password_an_unknown_user_and_a_deactivated_account_get_the_very_same_refusal()
    {
        var (database, app) = await StartAsync();
        await using var _ = database; await using var __ = app;
        await database.AddAccountAsync("gone", isActive: false);
        var browser = app.Browser();

        using var wrongPassword = await browser.PostAsJsonAsync(Session, new { username = "admin", password = Wrong });
        var unknown = await OsmeApp.SignInAsync(browser, "nobody");
        var deactivated = await OsmeApp.SignInAsync(browser, "gone");

        var refusal = (await wrongPassword.Content.ReadFromJsonAsync<JsonElement>()).GetRawText();
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal("""{"ok":false,"data":null,"error":{"code":"invalid_credentials","fields":null}}""", refusal);
        Assert.False(wrongPassword.Headers.Contains("Set-Cookie"));
        Assert.Equal((HttpStatusCode.Unauthorized, refusal), (unknown.Status, unknown.Body.GetRawText()));
        Assert.Equal((HttpStatusCode.Unauthorized, refusal), (deactivated.Status, deactivated.Body.GetRawText()));
        Assert.Equal("signed_out", StateOf(await ApiTests.Send(browser, HttpMethod.Get, Session)));
    }

    [Fact]
    public async Task Five_failures_in_a_row_stop_the_account_even_with_the_right_password()
    {
        var (database, app) = await StartAsync();
        await using var _ = database; await using var __ = app;
        var browser = app.Browser();

        for (var i = 0; i < 5; i++) await OsmeApp.SignInAsync(browser, "admin", Wrong);
        var locked = await OsmeApp.SignInAsync(browser, "admin");
        app.Clock.Advance(TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(1));
        var afterwards = await OsmeApp.SignInAsync(browser, "admin");

        Assert.Equal("invalid_credentials", CodeOf(locked));
        Assert.Equal(HttpStatusCode.OK, afterwards.Status);
    }

    [Theory]
    [InlineData("GET", Tickets)]
    [InlineData("POST", Tickets)]
    [InlineData("GET", "/api/meta")]
    [InlineData("GET", "/api/meta/itdesk.ticket")]
    [InlineData("GET", "/api/packages")]
    [InlineData("GET", "/api/anything/else")]
    [InlineData("DELETE", Session)]
    [InlineData("POST", PasswordRoute)]
    public async Task Without_a_session_nothing_answers_and_no_data_comes_back(string method, string url)
    {
        var (database, app) = await StartAsync();
        await using var _ = database; await using var __ = app;

        var body = method == "GET" ? null : new { title = "x", current_password = Right, password = Fresh };
        AssertUnauthenticated(await ApiTests.Send(app.Browser(), new HttpMethod(method), url, body));
    }

    [Fact]
    public async Task A_made_up_cookie_is_no_session()
    {
        var (database, app) = await StartAsync();
        await using var _ = database; await using var __ = app;
        var browser = app.Browser();
        browser.DefaultRequestHeaders.Add("Cookie", "__Host-osme_session=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");

        AssertUnauthenticated(await ApiTests.Send(browser, HttpMethod.Get, Tickets));
        Assert.Equal("signed_out", StateOf(await ApiTests.Send(browser, HttpMethod.Get, Session)));
    }

    [Fact]
    public async Task Signing_out_ends_that_session_and_no_other()
    {
        var (database, app) = await StartAsync();
        await using var _ = database; await using var __ = app;
        var computer = await app.AdministratorAsync();
        var phone = await app.AdministratorAsync();

        using var response = await computer.DeleteAsync(Session);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("""{"state":"signed_out"}""", body.GetProperty("data").GetRawText());
        Assert.Contains("__Host-osme_session=;", Assert.Single(response.Headers.GetValues("Set-Cookie")));
        AssertUnauthenticated(await ApiTests.Send(computer, HttpMethod.Get, Tickets));
        Assert.Equal("signed_out", StateOf(await ApiTests.Send(computer, HttpMethod.Get, Session)));
        Assert.Equal(HttpStatusCode.OK, (await ApiTests.Send(phone, HttpMethod.Get, Tickets)).Status);
    }

    [Fact]
    public async Task The_old_token_does_not_work_after_signing_out_even_if_someone_kept_it()
    {
        var (database, app) = await StartAsync();
        await using var _ = database; await using var __ = app;
        using var entered = await app.Browser().PostAsJsonAsync(Session, new { username = "admin", password = Right });
        var cookie = entered.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        var kept = app.Browser();
        kept.DefaultRequestHeaders.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.OK, (await ApiTests.Send(kept, HttpMethod.Get, Tickets)).Status);

        await kept.DeleteAsync(Session);

        AssertUnauthenticated(await ApiTests.Send(kept, HttpMethod.Get, Tickets));
    }

    [Fact]
    public async Task A_session_unused_for_twelve_hours_is_no_session()
    {
        var (database, app) = await StartAsync();
        await using var _ = database; await using var __ = app;
        var browser = await app.AdministratorAsync();

        app.Clock.Advance(TimeSpan.FromHours(12));

        AssertUnauthenticated(await ApiTests.Send(browser, HttpMethod.Get, Tickets));
        Assert.Equal("signed_out", StateOf(await ApiTests.Send(browser, HttpMethod.Get, Session)));
    }

    [Fact]
    public async Task Deactivating_an_account_ends_what_its_open_session_can_do()
    {
        var (database, app) = await StartAsync();
        await using var _ = database; await using var __ = app;
        await database.AddAccountAsync("mlopez");
        var browser = app.Browser();
        await OsmeApp.SignInAsync(browser, "mlopez");
        Assert.Equal(HttpStatusCode.OK, (await ApiTests.Send(browser, HttpMethod.Get, Packages)).Status);

        await database.ScalarAsync<object>("UPDATE core.user_account SET is_active = false WHERE username = 'mlopez'");

        AssertUnauthenticated(await ApiTests.Send(browser, HttpMethod.Get, Packages));
        Assert.Equal("signed_out", StateOf(await ApiTests.Send(browser, HttpMethod.Get, Session)));
    }

    [Fact]
    public async Task An_account_without_roles_gets_in_and_is_told_it_has_none()
    {
        var (database, app) = await StartAsync();
        await using var _ = database; await using var __ = app;
        await database.AddAccountAsync("mlopez");

        var entered = await OsmeApp.SignInAsync(app.Browser(), "mlopez");

        var data = entered.Body.GetProperty("data");
        Assert.Equal(("signed_in", false, "[]", "[]"), (
            data.GetProperty("state").GetString(), data.GetProperty("isAdministrator").GetBoolean(),
            data.GetProperty("roles").GetRawText(), data.GetProperty("permissions").GetRawText()));
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("http://localhost")]
    [InlineData("https://localhost:8443")]
    [InlineData("null")]
    [InlineData(null)]
    public async Task A_write_from_another_origin_or_without_origin_is_refused_and_does_nothing(string? origin)
    {
        var (database, app) = await StartAsync();
        await using var _ = database; await using var __ = app;
        var browser = app.Browser(origin: origin);

        var entered = await OsmeApp.SignInAsync(browser, "admin");

        Assert.Equal(HttpStatusCode.Forbidden, entered.Status);
        Assert.Equal("""{"ok":false,"data":null,"error":{"code":"invalid_origin","fields":null}}""", entered.Body.GetRawText());
        // Only the session StartAsync opened is there.
        Assert.Equal(1L, await database.ScalarAsync<long>("SELECT count(*) FROM core.session"));
        Assert.Equal("signed_out", StateOf(await ApiTests.Send(browser, HttpMethod.Get, Session)));
    }

    [Fact]
    public async Task A_signed_in_write_from_another_origin_is_refused_too()
    {
        var (database, app) = await StartAsync();
        await using var _ = database; await using var __ = app;
        using var entered = await app.Browser().PostAsJsonAsync(Session, new { username = "admin", password = Right });
        var foreign = app.Browser(origin: "https://evil.example");
        foreign.DefaultRequestHeaders.Add("Cookie", entered.Headers.GetValues("Set-Cookie").Single().Split(';')[0]);

        var created = await ApiTests.Send(foreign, HttpMethod.Post, Tickets, new { title = "Forged" });
        var signedOut = await ApiTests.Send(foreign, HttpMethod.Delete, Session);

        Assert.Equal("invalid_origin", CodeOf(created));
        Assert.Equal("invalid_origin", CodeOf(signedOut));
        // Reading needs no Origin, and the session is still there.
        var list = await ApiTests.Send(foreign, HttpMethod.Get, Tickets);
        Assert.Equal((HttpStatusCode.OK, 0), (list.Status, list.Body.GetProperty("data").GetArrayLength()));
    }

    [Theory]
    [InlineData(Session)]
    [InlineData(Tickets)]
    [InlineData("/api/setup")]
    public async Task A_write_that_is_not_json_is_refused(string url)
    {
        var (database, app) = await StartAsync();
        await using var _ = database; await using var __ = app;
        var browser = await app.AdministratorAsync();

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = "admin", ["password"] = Right, ["title"] = "x",
        });
        using var response = await browser.PostAsync(url, form);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_body", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{not json")]
    [InlineData("")]
    public async Task Signing_in_with_a_body_that_is_not_a_json_object_is_refused(string json)
    {
        var (database, app) = await StartAsync();
        await using var _ = database; await using var __ = app;

        using var response = await app.Browser().PostAsync(Session, new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Changing_the_own_password_needs_the_current_one_and_closes_the_other_sessions()
    {
        var (database, app) = await StartAsync();
        await using var _ = database; await using var __ = app;
        var computer = await app.AdministratorAsync();
        var phone = await app.AdministratorAsync();

        var changed = await ChangeAsync(computer, Right, Fresh);

        Assert.Equal(HttpStatusCode.OK, changed.Status);
        Assert.Equal("signed_in", StateOf(changed));
        Assert.DoesNotContain(Fresh, changed.Body.GetRawText());
        // The one that asked stays in; the other device is out.
        Assert.Equal(HttpStatusCode.OK, (await ApiTests.Send(computer, HttpMethod.Get, Tickets)).Status);
        AssertUnauthenticated(await ApiTests.Send(phone, HttpMethod.Get, Tickets));
        // The old password stops working and the new one works.
        Assert.Equal("invalid_credentials", CodeOf(await OsmeApp.SignInAsync(app.Browser(), "admin", Right)));
        Assert.Equal(HttpStatusCode.OK, (await OsmeApp.SignInAsync(app.Browser(), "admin", Fresh)).Status);
    }

    [Fact]
    public async Task With_a_wrong_current_password_nothing_changes()
    {
        var (database, app) = await StartAsync();
        await using var _ = database; await using var __ = app;
        var browser = await app.AdministratorAsync();

        var refused = await ChangeAsync(browser, Wrong, Fresh);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.Status);
        Assert.Equal("validation", CodeOf(refused));
        Assert.Equal([("current_password", "wrong_password")], FieldsOf(refused));
        Assert.Equal(HttpStatusCode.OK, (await OsmeApp.SignInAsync(app.Browser(), "admin", Right)).Status);
        Assert.Equal("invalid_credentials", CodeOf(await OsmeApp.SignInAsync(app.Browser(), "admin", Fresh)));
    }

    [Fact]
    public async Task The_new_password_must_follow_the_rules_and_differ_from_the_current_one()
    {
        var (database, app) = await StartAsync();
        await using var _ = database; await using var __ = app;
        var browser = await app.AdministratorAsync();

        var weak = await ChangeAsync(browser, Right, "abcdefgh");
        var same = await ChangeAsync(browser, Right, Right);

        Assert.Equal(
            [("password", "missing_uppercase"), ("password", "missing_digit"), ("password", "missing_symbol")], FieldsOf(weak));
        Assert.Equal([("password", "same_as_current")], FieldsOf(same));
        Assert.Equal(HttpStatusCode.OK, (await OsmeApp.SignInAsync(app.Browser(), "admin", Right)).Status);
    }

    [Fact]
    public async Task Wrong_current_passwords_count_as_failed_attempts_and_the_fifth_stops_the_account()
    {
        var (database, app) = await StartAsync();
        await using var _ = database; await using var __ = app;
        var browser = await app.AdministratorAsync();

        for (var i = 0; i < 5; i++) await ChangeAsync(browser, Wrong, Fresh);
        var whileWaiting = await ChangeAsync(browser, Right, Fresh);

        // Same answer as a wrong one, the session stays open, and signing in elsewhere is refused.
        Assert.Equal([("current_password", "wrong_password")], FieldsOf(whileWaiting));
        Assert.Equal(HttpStatusCode.OK, (await ApiTests.Send(browser, HttpMethod.Get, Tickets)).Status);
        Assert.Equal("invalid_credentials", CodeOf(await OsmeApp.SignInAsync(app.Browser(), "admin", Right)));

        app.Clock.Advance(TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(1));
        Assert.Equal(HttpStatusCode.OK, (await ChangeAsync(browser, Right, Fresh)).Status);
    }

    [Fact]
    public async Task With_a_temporary_password_the_only_things_left_are_changing_it_and_leaving()
    {
        var (database, app) = await StartAsync();
        await using var _ = database; await using var __ = app;
        await database.AddAccountAsync("mlopez", mustChangePassword: true);
        var browser = app.Browser();

        var entered = await OsmeApp.SignInAsync(browser, "mlopez");
        Assert.True(entered.Body.GetProperty("data").GetProperty("account").GetProperty("mustChangePassword").GetBoolean());

        foreach (var (method, url) in new[]
        {
            (HttpMethod.Get, Tickets), (HttpMethod.Post, Tickets), (HttpMethod.Get, "/api/meta"),
            (HttpMethod.Get, "/api/packages"), (HttpMethod.Get, "/api/anything/else"),
        })
        {
            var refused = await ApiTests.Send(browser, method, url, method == HttpMethod.Get ? null : new { title = "x" });
            Assert.Equal(HttpStatusCode.Forbidden, refused.Status);
            Assert.Equal("""{"ok":false,"data":null,"error":{"code":"password_change_required","fields":null}}""", refused.Body.GetRawText());
        }
        Assert.Equal("signed_in", StateOf(await ApiTests.Send(browser, HttpMethod.Get, Session)));

        // Changing it opens the application, and the temporary one stops working.
        var changed = await ChangeAsync(browser, Right, Fresh);
        Assert.False(changed.Body.GetProperty("data").GetProperty("account").GetProperty("mustChangePassword").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await ApiTests.Send(browser, HttpMethod.Get, Packages)).Status);
        Assert.Equal("invalid_credentials", CodeOf(await OsmeApp.SignInAsync(app.Browser(), "mlopez", Right)));
    }

    [Fact]
    public async Task With_a_temporary_password_one_can_still_leave()
    {
        var (database, app) = await StartAsync();
        await using var _ = database; await using var __ = app;
        await database.AddAccountAsync("mlopez", mustChangePassword: true);
        var browser = app.Browser();
        await OsmeApp.SignInAsync(browser, "mlopez");

        Assert.Equal(HttpStatusCode.OK, (await ApiTests.Send(browser, HttpMethod.Delete, Session)).Status);
        Assert.Equal(0L, await database.ScalarAsync<long>(
            "SELECT count(*) FROM core.session s JOIN core.user_account u ON u.id = s.user_account_id WHERE u.username = 'mlopez'"));
    }
}
