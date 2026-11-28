// SPDX-License-Identifier: LGPL-3.0-or-later
using System.Net;
using System.Text.Json;

namespace Osme.Tests;

/// <summary>The catalog of permissions and the roles through the real server (specs/permisos-por-modulo).</summary>
public class RolesApiTests
{
    private const string Roles = "/api/roles";
    private const string Catalog = "/api/permissions";
    private static readonly HttpMethod Patch = HttpMethod.Patch;

    /// <summary>A server with itdesk and gamma, its company, and the administrator signed in.</summary>
    private static async Task<(TestDatabase Database, OsmeApp App, HttpClient Administrator)> StartAsync()
    {
        var database = await TestDatabase.CreateAsync();
        var app = new OsmeApp(database.ConnectionString, Repo.MixedPackages);
        return (database, app, await app.AdministratorAsync());
    }

    private static object Grant(string key, string scope = "all") => new { key, scope };

    private static async Task<JsonElement> RoleAsync(HttpClient client, string name) =>
        (await ApiTests.Send(client, HttpMethod.Get, Roles)).Body.GetProperty("data").EnumerateArray()
            .Single(r => r.GetProperty("name").GetString() == name);

    private static string[] PermissionsOf(JsonElement holder) =>
        [.. holder.GetProperty("permissions").EnumerateArray()
            .Select(p => $"{p.GetProperty("key").GetString()}:{p.GetProperty("scope").GetString()}").Order(StringComparer.Ordinal)];

    private static IEnumerable<(string?, string?, string)> FieldsOf((HttpStatusCode Status, JsonElement Body) response) =>
        response.Body.GetProperty("error").GetProperty("fields").EnumerateArray().Select(e => (
            e.GetProperty("field").GetString(), e.GetProperty("code").GetString(),
            e.GetProperty("values").ValueKind == JsonValueKind.Null
                ? "" : string.Join(",", e.GetProperty("values").EnumerateArray().Select(v => v.GetString()))));

    private static void AssertForbidden((HttpStatusCode Status, JsonElement Body) response)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.Status);
        Assert.Equal("""{"ok":false,"data":null,"error":{"code":"forbidden","fields":null}}""", response.Body.GetRawText());
    }

    /// <summary>A browser signed in as a new account that has the given roles.</summary>
    private static async Task<HttpClient> AccountAsync(TestDatabase database, OsmeApp app, string username, params string[] roles)
    {
        var account = await database.AddAccountAsync(username);
        foreach (var role in roles) await database.GiveRoleAsync(account, role);
        var browser = app.Browser();
        Assert.Equal(HttpStatusCode.OK, (await OsmeApp.SignInAsync(browser, username)).Status);
        return browser;
    }

    private static async Task<string[]> SessionPermissionsAsync(HttpClient browser) =>
        PermissionsOf((await ApiTests.Send(browser, HttpMethod.Get, "/api/session")).Body.GetProperty("data"));

    [Fact]
    public async Task The_catalog_offers_the_views_of_the_core_and_each_document_of_the_loaded_packages_with_its_actions()
    {
        var (database, app, administrator) = await StartAsync();
        await using var _ = database; await using var __ = app;

        var (status, body) = await ApiTests.Send(administrator, HttpMethod.Get, Catalog);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(
            """
            {"core":["core.users","core.roles","core.access_settings"],"packages":[
            {"name":"gamma","label":"Gamma module","documents":[
            {"key":"gamma.job","label":"Job","read":"gamma.job.read","create":"gamma.job.create"},
            {"key":"gamma.report","label":"Report","read":"gamma.report.read","create":"gamma.report.create"}]},
            {"name":"itdesk","label":"TI interno","documents":[
            {"key":"itdesk.ticket","label":"Ticket","read":"itdesk.ticket.read","create":"itdesk.ticket.create"}]}]}
            """.ReplaceLineEndings(""),
            body.GetProperty("data").GetRawText());
    }

    [Fact]
    public async Task The_list_of_roles_shows_the_administrator_with_everything_and_the_factory_roles()
    {
        var (database, app, administrator) = await StartAsync();
        await using var _ = database; await using var __ = app;

        var roles = (await ApiTests.Send(administrator, HttpMethod.Get, Roles)).Body.GetProperty("data");

        Assert.Equal(["Auditor", "core.administrator", "Solicitante", "Técnico de sistemas", "Worker"], roles.EnumerateArray().Select(r => r.GetProperty("name").GetString()));
        var system = roles[1];
        var worker = roles[4];
        Assert.Equal((true, true, "core.administrator"), (
            system.GetProperty("isSystem").GetBoolean(), system.GetProperty("isActive").GetBoolean(), system.GetProperty("origin").GetString()));
        Assert.Equal(9, PermissionsOf(system).Length); // three views and three documents with two actions each
        Assert.Equal(("gamma.worker", false), (worker.GetProperty("origin").GetString(), worker.GetProperty("isSystem").GetBoolean()));
        Assert.Equal(["gamma.job.create:all", "gamma.job.read:own"], PermissionsOf(worker));
    }

    [Fact]
    public async Task Without_the_Roles_view_nothing_of_roles_answers_and_nothing_changes()
    {
        var (database, app, administrator) = await StartAsync();
        await using var _ = database; await using var __ = app;
        var worker = (await RoleAsync(administrator, "Worker")).GetProperty("id").GetString();
        var outsider = await AccountAsync(database, app, "mlopez", "Worker", "Auditor");

        AssertForbidden(await ApiTests.Send(outsider, HttpMethod.Get, Catalog));
        AssertForbidden(await ApiTests.Send(outsider, HttpMethod.Get, Roles));
        AssertForbidden(await ApiTests.Send(outsider, HttpMethod.Post, Roles, new { name = "Mine", permissions = new[] { Grant("core.roles") } }));
        AssertForbidden(await ApiTests.Send(outsider, Patch, $"{Roles}/{worker}", new { name = "Renamed", permissions = new[] { Grant("core.roles") } }));

        var roles = (await ApiTests.Send(administrator, HttpMethod.Get, Roles)).Body.GetProperty("data");
        Assert.Equal(["Auditor", "core.administrator", "Solicitante", "Técnico de sistemas", "Worker"], roles.EnumerateArray().Select(r => r.GetProperty("name").GetString()));
        Assert.Equal(["gamma.job.create:all", "gamma.job.read:own"], PermissionsOf(roles[4]));
    }

    [Theory]
    [InlineData("GET", Catalog)]
    [InlineData("GET", Roles)]
    [InlineData("POST", Roles)]
    [InlineData("PATCH", Roles + "/019a0000-0000-7000-8000-000000000000")]
    public async Task Without_a_session_nothing_of_roles_answers(string method, string url)
    {
        var (database, app, _) = await StartAsync();
        await using var __ = database; await using var ___ = app;

        var response = await ApiTests.Send(app.Browser(), new HttpMethod(method), url, method == "GET" ? null : new { name = "x" });

        Assert.Equal((HttpStatusCode.Unauthorized, "unauthenticated"),
            (response.Status, response.Body.GetProperty("error").GetProperty("code").GetString()));
    }

    [Fact]
    public async Task A_role_made_to_measure_mixes_packages_and_views_and_its_account_gets_exactly_that()
    {
        var (database, app, administrator) = await StartAsync();
        await using var _ = database; await using var __ = app;

        var created = await ApiTests.Send(administrator, HttpMethod.Post, Roles, new
        {
            name = " Front desk ",
            permissions = new[] { Grant("itdesk.ticket.read"), Grant("gamma.job.read", "own"), Grant("core.users") },
        });

        Assert.Equal(HttpStatusCode.Created, created.Status);
        var role = created.Body.GetProperty("data");
        Assert.Equal(("Front desk", true, false, JsonValueKind.Null), (
            role.GetProperty("name").GetString(), role.GetProperty("isActive").GetBoolean(),
            role.GetProperty("isSystem").GetBoolean(), role.GetProperty("origin").ValueKind));
        Assert.Equal(7, role.GetProperty("id").GetGuid().Version);
        Assert.Equal(["core.users:all", "gamma.job.read:own", "itdesk.ticket.read:all"], PermissionsOf(role));

        // Sees the documents of both packages and the users screen; is offered no creating.
        var account = await AccountAsync(database, app, "mlopez", "Front desk");
        Assert.Equal(["core.users:all", "gamma.job.read:own", "itdesk.ticket.read:all"], await SessionPermissionsAsync(account));
    }

    [Fact]
    public async Task Whoever_has_the_Roles_view_manages_roles_without_being_administrator()
    {
        var (database, app, administrator) = await StartAsync();
        await using var _ = database; await using var __ = app;
        await ApiTests.Send(administrator, HttpMethod.Post, Roles, new { name = "Role keeper", permissions = new[] { Grant("core.roles") } });
        var keeper = await AccountAsync(database, app, "keeper", "Role keeper");

        Assert.Equal(HttpStatusCode.OK, (await ApiTests.Send(keeper, HttpMethod.Get, Catalog)).Status);
        Assert.Equal(HttpStatusCode.OK, (await ApiTests.Send(keeper, HttpMethod.Get, Roles)).Status);
        var created = await ApiTests.Send(keeper, HttpMethod.Post, Roles, new { name = "Made by keeper", permissions = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.Created, created.Status);
        Assert.Equal("keeper", await database.ScalarAsync<string>(
            "SELECT u.username FROM core.role r JOIN core.user_account u ON u.id = r.created_by WHERE r.name = 'Made by keeper'"));
    }

    [Fact]
    public async Task Editing_or_deactivating_a_role_in_use_holds_from_the_next_request_of_its_accounts()
    {
        var (database, app, administrator) = await StartAsync();
        await using var _ = database; await using var __ = app;
        var auditor = (await RoleAsync(administrator, "Auditor")).GetProperty("id").GetString();
        var account = await AccountAsync(database, app, "mlopez", "Auditor");
        Assert.Equal(["gamma.job.read:all", "gamma.report.read:all"], await SessionPermissionsAsync(account));

        var edited = await ApiTests.Send(administrator, Patch, $"{Roles}/{auditor}", new { permissions = new[] { Grant("gamma.job.read") } });
        Assert.Equal(HttpStatusCode.OK, edited.Status);
        Assert.Equal(["gamma.job.read:all"], PermissionsOf(edited.Body.GetProperty("data")));
        Assert.Equal(["gamma.job.read:all"], await SessionPermissionsAsync(account));

        var deactivated = await ApiTests.Send(administrator, Patch, $"{Roles}/{auditor}", new { is_active = false });
        Assert.False(deactivated.Body.GetProperty("data").GetProperty("isActive").GetBoolean());
        Assert.Empty(await SessionPermissionsAsync(account));

        // Reactivated, it is worth again for the same accounts without assigning it anew.
        await ApiTests.Send(administrator, Patch, $"{Roles}/{auditor}", new { is_active = true });
        Assert.Equal(["gamma.job.read:all"], await SessionPermissionsAsync(account));
    }

    [Fact]
    public async Task A_role_is_renamed_and_what_the_change_does_not_mention_stays()
    {
        var (database, app, administrator) = await StartAsync();
        await using var _ = database; await using var __ = app;
        var worker = (await RoleAsync(administrator, "Worker")).GetProperty("id").GetString();

        var renamed = await ApiTests.Send(administrator, Patch, $"{Roles}/{worker}", new { name = "Field worker" });

        var role = renamed.Body.GetProperty("data");
        Assert.Equal(("Field worker", true, "gamma.worker"),
            (role.GetProperty("name").GetString(), role.GetProperty("isActive").GetBoolean(), role.GetProperty("origin").GetString()));
        Assert.Equal(["gamma.job.create:all", "gamma.job.read:own"], PermissionsOf(role));
    }

    [Fact]
    public async Task What_breaks_the_rules_of_a_role_is_refused_naming_the_field_and_what_it_is_about()
    {
        var (database, app, administrator) = await StartAsync();
        await using var _ = database; await using var __ = app;
        await ApiTests.Send(administrator, Patch, $"{Roles}/{(await RoleAsync(administrator, "Auditor")).GetProperty("id").GetString()}", new { is_active = false });

        // The name of a deactivated role, whatever the case; a permission nobody declares;
        // creating without reading; a scope that does not apply.
        var refused = await ApiTests.Send(administrator, HttpMethod.Post, Roles, new
        {
            name = "AUDITOR",
            permissions = new[]
            {
                Grant("sales.invoice.read"), Grant("itdesk.ticket.create"), Grant("gamma.job.create", "own"), Grant("gamma.job.read", "own"),
            },
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.Status);
        Assert.Equal("validation", refused.Body.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(
            [
                ("name", "taken", ""),
                ("permissions", "unknown_permission", "sales.invoice.read"),
                ("permissions", "invalid_scope", "gamma.job.create"),
                ("permissions", "create_without_read", "itdesk.ticket"),
            ],
            FieldsOf(refused));
        Assert.Equal(5, (await ApiTests.Send(administrator, HttpMethod.Get, Roles)).Body.GetProperty("data").GetArrayLength());
    }

    [Fact]
    public async Task The_administrator_role_cannot_be_edited_renamed_nor_deactivated()
    {
        var (database, app, administrator) = await StartAsync();
        await using var _ = database; await using var __ = app;
        var system = (await RoleAsync(administrator, "core.administrator")).GetProperty("id").GetString();

        foreach (var change in new object[] { new { name = "Boss" }, new { permissions = Array.Empty<object>() }, new { is_active = false } })
        {
            var refused = await ApiTests.Send(administrator, Patch, $"{Roles}/{system}", change);
            Assert.Equal(HttpStatusCode.Conflict, refused.Status);
            Assert.Equal("""{"ok":false,"data":null,"error":{"code":"system_role","fields":null}}""", refused.Body.GetRawText());
        }

        var after = await RoleAsync(administrator, "core.administrator");
        Assert.True(after.GetProperty("isActive").GetBoolean());
        Assert.Equal(9, PermissionsOf(after).Length);
        Assert.Equal(HttpStatusCode.OK, (await ApiTests.Send(administrator, HttpMethod.Get, "/api/docs/gamma.report")).Status);
    }

    [Theory]
    [InlineData("019a0000-0000-7000-8000-000000000000")]
    [InlineData("not-an-id")]
    public async Task A_role_that_does_not_exist_is_not_found(string id)
    {
        var (database, app, administrator) = await StartAsync();
        await using var _ = database; await using var __ = app;

        var response = await ApiTests.Send(administrator, Patch, $"{Roles}/{id}", new { name = "x" });

        Assert.Equal((HttpStatusCode.NotFound, "not_found"), (response.Status, response.Body.GetProperty("error").GetProperty("code").GetString()));
    }

    [Theory]
    [InlineData("""{"name":7}""")]
    [InlineData("""{"name":"x","permissions":"all"}""")]
    [InlineData("""{"name":"x","permissions":["gamma.job.read"]}""")]
    [InlineData("""{"name":"x","permissions":[{"scope":"all"}]}""")]
    [InlineData("""{"is_active":"no"}""")]
    [InlineData("""[]""")]
    public async Task A_change_with_the_wrong_shape_is_an_invalid_body(string json)
    {
        var (database, app, administrator) = await StartAsync();
        await using var _ = database; await using var __ = app;
        var worker = (await RoleAsync(administrator, "Worker")).GetProperty("id").GetString();

        using var response = await administrator.PatchAsync(
            $"{Roles}/{worker}", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Worker", (await RoleAsync(administrator, "Worker")).GetProperty("name").GetString());
    }

    [Fact]
    public async Task A_permission_without_scope_is_refused_as_an_invalid_scope()
    {
        var (database, app, administrator) = await StartAsync();
        await using var _ = database; await using var __ = app;

        var refused = await ApiTests.Send(administrator, HttpMethod.Post, Roles, new { name = "Odd", permissions = new[] { new { key = "gamma.job.read" } } });

        Assert.Equal([("permissions", "invalid_scope", "gamma.job.read")], FieldsOf(refused));
    }

    [Fact]
    public async Task Without_a_package_its_permissions_grant_nothing_and_they_come_back_with_it()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using (var withGamma = new OsmeApp(database.ConnectionString, Repo.MixedPackages))
        {
            await withGamma.AdministratorAsync();
            Assert.Equal(["gamma.job.create:all", "gamma.job.read:own"],
                await SessionPermissionsAsync(await AccountAsync(database, withGamma, "mlopez", "Worker")));
        }

        // The server starts without gamma: the account still gets in, with nothing of that package.
        await using (var withoutGamma = new OsmeApp(database.ConnectionString, Repo.Packages))
        {
            var browser = withoutGamma.Browser();
            Assert.Equal(HttpStatusCode.OK, (await OsmeApp.SignInAsync(browser, "mlopez")).Status);
            Assert.Empty(await SessionPermissionsAsync(browser));
            Assert.Empty(PermissionsOf(await RoleAsync(await withoutGamma.AdministratorAsync(), "Worker")));
        }

        await using var again = new OsmeApp(database.ConnectionString, Repo.MixedPackages);
        var back = again.Browser();
        await OsmeApp.SignInAsync(back, "mlopez");
        Assert.Equal(["gamma.job.create:all", "gamma.job.read:own"], await SessionPermissionsAsync(back));
    }
}
