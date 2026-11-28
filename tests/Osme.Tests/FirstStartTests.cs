// SPDX-License-Identifier: LGPL-3.0-or-later
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Osme.Tests;

/// <summary>The scenarios of specs/primer-arranque, through the real server.</summary>
public class FirstStartTests
{
    private const string State = "/api/session";
    private const string Setup = "/api/setup";
    private const string Other = OsmeApp.OtherMachine;

    private static object Form(string? code = null, string password = OsmeApp.Password) => new
    {
        company_name = "Acme", name = "Ana Admin", username = "admin", password, code,
    };

    private static string? CodeOf((HttpStatusCode Status, JsonElement Body) response) =>
        response.Body.GetProperty("error").GetProperty("code").GetString();

    private static async Task<string> RowsAsync(TestDatabase database) => (await database.ScalarAsync<string>(
        "SELECT (SELECT count(*) FROM core.company) || ' ' || (SELECT count(*) FROM core.user_account)"))!;

    private static async Task<(TestDatabase Database, OsmeApp App)> StartAsync()
    {
        var database = await TestDatabase.CreateAsync();
        return (database, new OsmeApp(database.ConnectionString, Repo.Packages));
    }

    [Fact]
    public async Task A_new_installation_says_the_setup_is_missing_and_whether_this_machine_needs_the_code()
    {
        var (database, app) = await StartAsync();
        await using var _ = database; await using var __ = app;

        var here = await ApiTests.Send(app.Browser(), HttpMethod.Get, State);
        var elsewhere = await ApiTests.Send(app.Browser(from: Other), HttpMethod.Get, State);

        Assert.Equal(HttpStatusCode.OK, here.Status);
        Assert.Equal("""{"state":"setup_required","codeRequired":false}""", here.Body.GetProperty("data").GetRawText());
        Assert.Equal("""{"state":"setup_required","codeRequired":true}""", elsewhere.Body.GetProperty("data").GetRawText());
    }

    [Fact]
    public async Task A_complete_setup_from_the_servers_machine_needs_no_code_and_leaves_the_administrator_inside()
    {
        var (database, app) = await StartAsync();
        await using var _ = database; await using var __ = app;
        var browser = app.Browser();

        using var response = await browser.PostAsJsonAsync(Setup, Form());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("1 1", await RowsAsync(database));
        var data = body.GetProperty("data");
        Assert.Equal(("signed_in", "admin", "Ana Admin", false, true), (
            data.GetProperty("state").GetString(),
            data.GetProperty("account").GetProperty("username").GetString(),
            data.GetProperty("account").GetProperty("name").GetString(),
            data.GetProperty("account").GetProperty("mustChangePassword").GetBoolean(),
            data.GetProperty("isAdministrator").GetBoolean()));
        Assert.Equal("core.administrator", Assert.Single(data.GetProperty("roles").EnumerateArray()).GetProperty("name").GetString());
        // The administrator has the whole catalog: the views of the core and every document.
        Assert.Equal(
            ["core.access_settings:all", "core.roles:all", "core.users:all", "itdesk.ticket.create:all", "itdesk.ticket.read:all"],
            data.GetProperty("permissions").EnumerateArray()
                .Select(p => $"{p.GetProperty("key").GetString()}:{p.GetProperty("scope").GetString()}").Order(StringComparer.Ordinal));

        // Inside without typing the password again: the same browser is signed in and can use the API.
        var state = await ApiTests.Send(browser, HttpMethod.Get, State);
        Assert.Equal(data.GetRawText(), state.Body.GetProperty("data").GetRawText());
        Assert.Equal(HttpStatusCode.OK, (await ApiTests.Send(browser, HttpMethod.Get, "/api/meta")).Status);

        // Nothing of the password travels back.
        Assert.DoesNotContain(OsmeApp.Password, body.GetRawText());
        Assert.DoesNotContain("argon2", body.GetRawText());
        Assert.DoesNotContain("hash", body.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_session_cookie_is_host_only_secure_strict_and_lasts_seven_days()
    {
        var (database, app) = await StartAsync();
        await using var _ = database; await using var __ = app;

        using var response = await app.Browser().PostAsJsonAsync(Setup, Form());

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Matches("^__Host-osme_session=[A-Za-z0-9_-]{43}; ", cookie);
        var attributes = cookie.Split("; ").Skip(1).Select(a => a.ToLowerInvariant()).Order();
        Assert.Equal(["httponly", "max-age=604800", "path=/", "samesite=strict", "secure"], attributes);
    }

    [Fact]
    public async Task A_password_outside_the_rules_is_refused_pointing_at_it_and_nothing_is_created()
    {
        var (database, app) = await StartAsync();
        await using var _ = database; await using var __ = app;
        var browser = app.Browser();

        var response = await ApiTests.Send(browser, HttpMethod.Post, Setup, Form(password: "Abcdefg1"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.Status);
        Assert.Equal("validation", CodeOf(response));
        var error = Assert.Single(response.Body.GetProperty("error").GetProperty("fields").EnumerateArray());
        Assert.Equal(("password", "missing_symbol"), (error.GetProperty("field").GetString(), error.GetProperty("code").GetString()));
        Assert.Equal("0 0", await RowsAsync(database));
        Assert.Equal("setup_required", (await ApiTests.Send(browser, HttpMethod.Get, State)).Body.GetProperty("data").GetProperty("state").GetString());
    }

    [Theory]
    [InlineData("GET", "/api/docs/itdesk.ticket")]
    [InlineData("POST", "/api/docs/itdesk.ticket")]
    [InlineData("GET", "/api/meta")]
    [InlineData("GET", "/api/meta/itdesk.ticket")]
    [InlineData("GET", "/api/packages")]
    [InlineData("GET", "/api/anything/else")]
    [InlineData("POST", "/api/session")]
    [InlineData("DELETE", "/api/session")]
    [InlineData("POST", "/api/session/password")]
    public async Task Before_the_setup_nothing_else_answers_and_no_data_comes_back(string method, string url)
    {
        var (database, app) = await StartAsync();
        await using var _ = database; await using var __ = app;

        var body = method == "GET" ? null : new { title = "x", username = "admin", password = OsmeApp.Password };
        var response = await ApiTests.Send(app.Browser(), new HttpMethod(method), url, body);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Equal("""{"ok":false,"data":null,"error":{"code":"setup_required","fields":null}}""", response.Body.GetRawText());
    }

    [Fact]
    public async Task The_server_writes_to_its_log_a_code_of_eight_characters_easy_to_dictate()
    {
        var (database, app) = await StartAsync();
        await using var _ = database; await using var __ = app;

        Assert.Matches("^[A-HJKMNP-Z2-9]{4}-[A-HJKMNP-Z2-9]{4}$", app.InstallationCode());
    }

    [Theory]
    [InlineData("as written")]
    [InlineData("lowercase")]
    [InlineData("no hyphen")]
    [InlineData("spaces")]
    public async Task From_another_machine_the_setup_goes_through_with_the_code_however_it_is_typed(string how)
    {
        var (database, app) = await StartAsync();
        await using var _ = database; await using var __ = app;
        var code = app.InstallationCode();
        var typed = how switch
        {
            "lowercase" => code.ToLowerInvariant(),
            "no hyphen" => code.Replace("-", ""),
            "spaces" => $" {code.Replace('-', ' ')} ",
            _ => code,
        };

        var response = await ApiTests.Send(app.Browser(from: Other), HttpMethod.Post, Setup, Form(typed));

        Assert.Equal(HttpStatusCode.Created, response.Status);
        Assert.Equal("1 1", await RowsAsync(database));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("AAAA-AAAA")]
    [InlineData("K7QM")]
    public async Task From_another_machine_without_the_code_or_with_a_wrong_one_it_is_refused_with_no_hint(string? code)
    {
        var (database, app) = await StartAsync();
        await using var _ = database; await using var __ = app;
        var real = app.InstallationCode();

        var response = await ApiTests.Send(app.Browser(from: Other), HttpMethod.Post, Setup, Form(code));

        Assert.Equal(HttpStatusCode.Forbidden, response.Status);
        Assert.Equal("""{"ok":false,"data":null,"error":{"code":"invalid_code","fields":null}}""", response.Body.GetRawText());
        Assert.DoesNotContain(real, response.Body.GetRawText());
        Assert.Equal("0 0", await RowsAsync(database));
    }

    [Fact]
    public async Task The_code_is_asked_before_looking_at_the_data()
    {
        var (database, app) = await StartAsync();
        await using var _ = database; await using var __ = app;

        var response = await ApiTests.Send(app.Browser(from: Other), HttpMethod.Post, Setup, Form(password: "short"));

        Assert.Equal("invalid_code", CodeOf(response));
    }

    [Fact]
    public async Task After_five_wrong_codes_not_even_the_right_one_works_but_the_servers_machine_still_can()
    {
        var (database, app) = await StartAsync();
        await using var _ = database; await using var __ = app;
        var code = app.InstallationCode();
        var elsewhere = app.Browser(from: Other);

        for (var i = 0; i < 5; i++)
            Assert.Equal("invalid_code", CodeOf(await ApiTests.Send(elsewhere, HttpMethod.Post, Setup, Form("ZZZZ-ZZZ" + (i + 2)))));
        var afterwards = await ApiTests.Send(elsewhere, HttpMethod.Post, Setup, Form(code));

        Assert.Equal(HttpStatusCode.Forbidden, afterwards.Status);
        Assert.Equal("code_attempts_exhausted", CodeOf(afterwards));
        Assert.Equal("0 0", await RowsAsync(database));
        Assert.Equal(HttpStatusCode.Created, (await ApiTests.Send(app.Browser(), HttpMethod.Post, Setup, Form())).Status);
    }

    [Fact]
    public async Task A_request_without_code_does_not_spend_an_attempt()
    {
        var (database, app) = await StartAsync();
        await using var _ = database; await using var __ = app;
        var code = app.InstallationCode();
        var elsewhere = app.Browser(from: Other);

        for (var i = 0; i < 4; i++) await ApiTests.Send(elsewhere, HttpMethod.Post, Setup, Form("ZZZZ-ZZZZ"));
        for (var i = 0; i < 10; i++) await ApiTests.Send(elsewhere, HttpMethod.Post, Setup, Form());

        Assert.Equal(HttpStatusCode.Created, (await ApiTests.Send(elsewhere, HttpMethod.Post, Setup, Form(code))).Status);
    }

    [Fact]
    public async Task A_restart_without_company_makes_a_new_code_and_the_old_one_stops_working()
    {
        await using var database = await TestDatabase.CreateAsync();
        string old;
        await using (var first = new OsmeApp(database.ConnectionString, Repo.Packages))
            old = first.InstallationCode();

        await using var restarted = new OsmeApp(database.ConnectionString, Repo.Packages);
        var current = restarted.InstallationCode();
        var elsewhere = restarted.Browser(from: Other);

        Assert.NotEqual(old, current);
        Assert.Equal("invalid_code", CodeOf(await ApiTests.Send(elsewhere, HttpMethod.Post, Setup, Form(old))));
        Assert.Equal(HttpStatusCode.Created, (await ApiTests.Send(elsewhere, HttpMethod.Post, Setup, Form(current))).Status);
    }

    [Theory]
    [InlineData("X-Forwarded-For", "192.168.1.50")]
    [InlineData("Forwarded", "for=192.168.1.50")]
    public async Task A_forwarded_request_does_not_count_as_the_servers_machine(string header, string value)
    {
        var (database, app) = await StartAsync();
        await using var _ = database; await using var __ = app;
        var proxied = app.Browser();
        proxied.DefaultRequestHeaders.Add(header, value);

        var state = await ApiTests.Send(proxied, HttpMethod.Get, State);
        var setup = await ApiTests.Send(proxied, HttpMethod.Post, Setup, Form());

        Assert.True(state.Body.GetProperty("data").GetProperty("codeRequired").GetBoolean());
        Assert.Equal("invalid_code", CodeOf(setup));
        Assert.Equal("0 0", await RowsAsync(database));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(Other)]
    public async Task A_second_setup_is_refused_wherever_it_comes_from_and_changes_nothing(string? from)
    {
        var (database, app) = await StartAsync();
        await using var _ = database; await using var __ = app;
        var code = app.InstallationCode();
        await app.AdministratorAsync();

        var response = await ApiTests.Send(app.Browser(from), HttpMethod.Post, Setup, new
        {
            company_name = "Other", name = "Someone Else", username = "other", password = OsmeApp.Password, code,
        });

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Equal("setup_done", CodeOf(response));
        Assert.Equal("Acme|admin", await database.ScalarAsync<string>(
            "SELECT string_agg(c.name || '|' || u.username, ',') FROM core.company c, core.user_account u"));
    }

    [Fact]
    public async Task Once_there_is_a_company_a_restart_writes_no_code()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using (var first = new OsmeApp(database.ConnectionString, Repo.Packages))
            await first.AdministratorAsync();

        await using var restarted = new OsmeApp(database.ConnectionString, Repo.Packages);
        restarted.CreateClient();

        Assert.DoesNotContain(restarted.Log.Entries, e => e.Message.Contains("Installation code", StringComparison.OrdinalIgnoreCase));
    }
}
