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

        var (status, body) = await ApiTests.Send(app.CreateClient(), HttpMethod.Post, Tickets,
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
        var client = app.CreateClient();
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
            var client = app.CreateClient();
            await ApiTests.Send(client, HttpMethod.Post, Tickets, new { title = "Still here", priority = "high" });
            before = (await ApiTests.Send(client, HttpMethod.Get, Tickets)).Body.GetProperty("data").GetRawText();
        }

        await using var restarted = new OsmeApp(database.ConnectionString, Repo.Packages);
        var after = (await ApiTests.Send(restarted.CreateClient(), HttpMethod.Get, Tickets)).Body.GetProperty("data");

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
}
