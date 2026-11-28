// SPDX-License-Identifier: LGPL-3.0-or-later
using System.Net;
using System.Text.Json;

namespace Osme.Tests;

/// <summary>The scenarios of specs/tickets-de-ti that can be checked through the API.</summary>
public class TicketTests
{
    private const string Tickets = "/api/docs/itdesk.ticket";

    [Fact]
    public async Task A_ticket_without_priority_is_stored_as_normal_with_its_date()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var app = new OsmeApp(database.ConnectionString, Repo.Packages);

        var (status, body) = await ApiTests.Send(await app.AdministratorAsync(), HttpMethod.Post, Tickets,
            new { title = "No sound", description = "Laptop speakers stopped working" });

        Assert.Equal(HttpStatusCode.Created, status);
        var ticket = body.GetProperty("data");
        Assert.Equal(("No sound", "Laptop speakers stopped working", "normal"),
            (ticket.GetProperty("title").GetString(), ticket.GetProperty("description").GetString(), ticket.GetProperty("priority").GetString()));
        Assert.Equal(JsonValueKind.String, ticket.GetProperty("created_at").ValueKind);
    }

    [Fact]
    public async Task Tickets_are_listed_newest_first_with_title_priority_and_date()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var app = new OsmeApp(database.ConnectionString, Repo.Packages);
        var client = await app.AdministratorAsync();
        foreach (var (title, priority) in new[] { ("first", "low"), ("second", "normal"), ("third", "high") })
            await ApiTests.Send(client, HttpMethod.Post, Tickets, new { title, priority });

        var list = (await ApiTests.Send(client, HttpMethod.Get, Tickets)).Body.GetProperty("data");

        Assert.Equal([("third", "high"), ("second", "normal"), ("first", "low")],
            list.EnumerateArray().Select(t => (t.GetProperty("title").GetString()!, t.GetProperty("priority").GetString()!)));
        Assert.All(list.EnumerateArray(), t => Assert.True(t.TryGetProperty("created_at", out _)));
    }

    [Fact]
    public async Task Tickets_survive_a_restart()
    {
        await using var database = await TestDatabase.CreateAsync();
        string before;
        await using (var app = new OsmeApp(database.ConnectionString, Repo.Packages))
        {
            var client = await app.AdministratorAsync();
            await ApiTests.Send(client, HttpMethod.Post, Tickets, new { title = "Still here", priority = "high" });
            before = (await ApiTests.Send(client, HttpMethod.Get, Tickets)).Body.GetProperty("data").GetRawText();
        }

        await using var restarted = new OsmeApp(database.ConnectionString, Repo.Packages);
        var after = (await ApiTests.Send(await restarted.AdministratorAsync(), HttpMethod.Get, Tickets)).Body.GetProperty("data");

        Assert.Equal(1, after.GetArrayLength());
        Assert.Equal(before, after.GetRawText());
    }

    [Fact]
    public async Task The_ticket_table_lives_in_the_schema_of_ItDesk_and_nowhere_else()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var app = new OsmeApp(database.ConnectionString, Repo.Packages);
        app.CreateClient();

        var tables = await database.ScalarAsync<string>("""
            SELECT string_agg(table_schema || '.' || table_name, ',' ORDER BY table_schema, table_name) FROM information_schema.tables
            WHERE table_schema NOT IN ('pg_catalog', 'information_schema')
            """);

        // The core keeps only its own seven tables; nothing of ItDesk leaks into it.
        Assert.Equal(
            "core.company,core.role,core.role_permission,core.role_user_account,core.session,core.setting,"
            + "core.user_account,itdesk.ticket",
            tables);
    }

    // ---- Who raises and who sees tickets (change 0-2-1) ----

    private const string Technician = "Técnico de sistemas";
    private const string Requester = "Solicitante";
    private const string NotFoundBody = """{"ok":false,"data":null,"error":{"code":"not_found","fields":null}}""";

    private static async Task<HttpClient> AccountAsync(TestDatabase database, OsmeApp app, string username, params string[] roles)
    {
        var account = await database.AddAccountAsync(username);
        foreach (var role in roles) await database.GiveRoleAsync(account, role);
        var browser = app.Browser();
        Assert.Equal(HttpStatusCode.OK, (await OsmeApp.SignInAsync(browser, username)).Status);
        return browser;
    }

    private static async Task<string> RaiseAsync(HttpClient browser, string title, object? extra = null)
    {
        var raised = await ApiTests.Send(browser, HttpMethod.Post, Tickets, extra ?? new { title });
        Assert.Equal(HttpStatusCode.Created, raised.Status);
        return raised.Body.GetProperty("data").GetProperty("id").GetString()!;
    }

    private static async Task<string[]> TitlesAsync(HttpClient browser) =>
        [.. (await ApiTests.Send(browser, HttpMethod.Get, Tickets)).Body.GetProperty("data").EnumerateArray()
            .Select(t => t.GetProperty("title").GetString()!).Order()];

    [Fact]
    public async Task ItDesk_brings_the_technician_and_the_requester_ready_to_assign_with_their_permissions()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var app = new OsmeApp(database.ConnectionString, Repo.Packages);

        var roles = (await ApiTests.Send(await app.AdministratorAsync(), HttpMethod.Get, "/api/roles")).Body.GetProperty("data")
            .EnumerateArray().ToDictionary(r => r.GetProperty("name").GetString()!);

        Assert.Equal([Requester, Technician, "core.administrator"], roles.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(("itdesk.technician", true, "itdesk.ticket.create:all,itdesk.ticket.read:all"), Shape(roles[Technician]));
        Assert.Equal(("itdesk.requester", true, "itdesk.ticket.create:all,itdesk.ticket.read:own"), Shape(roles[Requester]));

        static (string?, bool, string) Shape(JsonElement role) => (
            role.GetProperty("origin").GetString(), role.GetProperty("isActive").GetBoolean(),
            string.Join(",", role.GetProperty("permissions").EnumerateArray()
                .Select(p => $"{p.GetProperty("key").GetString()}:{p.GetProperty("scope").GetString()}").Order(StringComparer.Ordinal)));
    }

    [Fact]
    public async Task A_technician_sees_the_tickets_of_everyone()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var app = new OsmeApp(database.ConnectionString, Repo.Packages);
        await app.AdministratorAsync();
        await RaiseAsync(await AccountAsync(database, app, "requester", Requester), "No sound");

        var technician = await AccountAsync(database, app, "technician", Technician);
        await RaiseAsync(technician, "Replace toner");

        Assert.Equal(["No sound", "Replace toner"], await TitlesAsync(technician));
    }

    [Fact]
    public async Task A_requester_sees_only_the_tickets_they_raised_and_another_ones_ticket_is_not_found()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var app = new OsmeApp(database.ConnectionString, Repo.Packages);
        var administrator = await app.AdministratorAsync();
        var first = await AccountAsync(database, app, "first", Requester);
        var second = await AccountAsync(database, app, "second", Requester);
        var mine = await RaiseAsync(first, "Mine");
        var theirs = await RaiseAsync(second, "Theirs");
        await RaiseAsync(administrator, "Of the administrator");

        Assert.Equal(["Mine"], await TitlesAsync(first));
        Assert.Equal(HttpStatusCode.OK, (await ApiTests.Send(first, HttpMethod.Get, $"{Tickets}/{mine}")).Status);
        var other = await ApiTests.Send(first, HttpMethod.Get, $"{Tickets}/{theirs}");
        Assert.Equal((HttpStatusCode.NotFound, NotFoundBody), (other.Status, other.Body.GetRawText()));
    }

    [Fact]
    public async Task An_account_with_no_role_over_tickets_is_not_offered_them_and_the_document_answers_as_if_it_did_not_exist()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var app = new OsmeApp(database.ConnectionString, Repo.Packages);
        await RaiseAsync(await app.AdministratorAsync(), "Existing");
        var nobody = await AccountAsync(database, app, "mlopez");

        Assert.Equal("[]", (await ApiTests.Send(nobody, HttpMethod.Get, "/api/meta")).Body.GetProperty("data").GetRawText());
        foreach (var (method, url) in new[] { (HttpMethod.Get, Tickets), (HttpMethod.Post, Tickets), (HttpMethod.Get, "/api/meta/itdesk.ticket") })
        {
            var response = await ApiTests.Send(nobody, method, url, method == HttpMethod.Get ? null : new { title = "x" });
            Assert.Equal((HttpStatusCode.NotFound, NotFoundBody), (response.Status, response.Body.GetRawText()));
        }
    }

    [Fact]
    public async Task A_role_made_to_measure_that_only_sees_lists_the_tickets_and_cannot_raise_one()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var app = new OsmeApp(database.ConnectionString, Repo.Packages);
        var administrator = await app.AdministratorAsync();
        await RaiseAsync(administrator, "Printer");
        await ApiTests.Send(administrator, HttpMethod.Post, "/api/roles",
            new { name = "Supervisor", permissions = new[] { new { key = "itdesk.ticket.read", scope = "all" } } });

        var supervisor = await AccountAsync(database, app, "supervisor", "Supervisor");

        Assert.Equal(["Printer"], await TitlesAsync(supervisor));
        var refused = await ApiTests.Send(supervisor, HttpMethod.Post, Tickets, new { title = "Not allowed" });
        Assert.Equal((HttpStatusCode.Forbidden, "forbidden"), (refused.Status, refused.Body.GetProperty("error").GetProperty("code").GetString()));
        Assert.Equal(["Printer"], await TitlesAsync(administrator));
    }

    [Fact]
    public async Task A_ticket_is_raised_in_the_name_of_the_session_whatever_the_caller_sends()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var app = new OsmeApp(database.ConnectionString, Repo.Packages);
        await app.AdministratorAsync();
        var requester = await AccountAsync(database, app, "requester", Requester);
        var other = await database.AddAccountAsync("other");

        await RaiseAsync(requester, "Mine", new { title = "Mine", created_by = other });

        Assert.Equal("requester", await database.ScalarAsync<string>(
            "SELECT u.username FROM itdesk.ticket t JOIN core.user_account u ON u.id = t.created_by"));
        Assert.Equal(["Mine"], await TitlesAsync(requester));
    }

    [Fact]
    public async Task Tickets_from_before_there_were_accounts_stay_without_author_and_only_who_sees_all_sees_them()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var app = new OsmeApp(database.ConnectionString, Repo.Packages);
        await app.AdministratorAsync();
        await database.ScalarAsync<object>(
            "INSERT INTO itdesk.ticket (id, title, priority, created_at, updated_at) VALUES (gen_random_uuid(), 'From 0-1-1', 'normal', now(), now())");

        Assert.Equal(["From 0-1-1"], await TitlesAsync(await AccountAsync(database, app, "technician", Technician)));
        Assert.Empty(await TitlesAsync(await AccountAsync(database, app, "requester", Requester)));
    }
}
