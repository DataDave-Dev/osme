// SPDX-License-Identifier: LGPL-3.0-or-later
using System.Net;
using System.Text.Json;

namespace Osme.Tests;

/// <summary>
/// The document API applies the permission in one place, for every document of every package
/// (specs/motor-de-documentos and specs/permisos-por-modulo; change 0-2-1, decisions 7 and 8).
/// gamma brings Worker (reads its own jobs, creates jobs) and Auditor (reads every job and report).
/// </summary>
public class DocumentAccessTests
{
    private const string Jobs = "/api/docs/gamma.job";
    private const string Reports = "/api/docs/gamma.report";
    private const string NotFoundBody = """{"ok":false,"data":null,"error":{"code":"not_found","fields":null}}""";

    private static async Task<(TestDatabase Database, OsmeApp App, HttpClient Administrator)> StartAsync()
    {
        var database = await TestDatabase.CreateAsync();
        var app = new OsmeApp(database.ConnectionString, Repo.MixedPackages);
        return (database, app, await app.AdministratorAsync());
    }

    private static async Task<HttpClient> AccountAsync(TestDatabase database, OsmeApp app, string username, params string[] roles)
    {
        var account = await database.AddAccountAsync(username);
        foreach (var role in roles) await database.GiveRoleAsync(account, role);
        var browser = app.Browser();
        Assert.Equal(HttpStatusCode.OK, (await OsmeApp.SignInAsync(browser, username)).Status);
        return browser;
    }

    private static async Task<string> CreateAsync(HttpClient browser, string url, string title)
    {
        var created = await ApiTests.Send(browser, HttpMethod.Post, url, new { title });
        Assert.Equal(HttpStatusCode.Created, created.Status);
        return created.Body.GetProperty("data").GetProperty("id").GetString()!;
    }

    private static async Task<string[]> TitlesAsync(HttpClient browser, string url)
    {
        var list = await ApiTests.Send(browser, HttpMethod.Get, url);
        Assert.Equal(HttpStatusCode.OK, list.Status);
        return [.. list.Body.GetProperty("data").EnumerateArray().Select(r => r.GetProperty("title").GetString()!).Order()];
    }

    private static async Task<string[]> OfferedAsync(HttpClient browser) =>
        [.. (await ApiTests.Send(browser, HttpMethod.Get, "/api/meta")).Body.GetProperty("data").EnumerateArray()
            .Select(d => d.GetProperty("key").GetString()!).Order()];

    private static void AssertNotFound((HttpStatusCode Status, JsonElement Body) response)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        Assert.Equal(NotFoundBody, response.Body.GetRawText());
    }

    [Fact]
    public async Task The_list_of_documents_brings_only_what_the_account_can_read()
    {
        var (database, app, administrator) = await StartAsync();
        await using var _ = database; await using var __ = app;

        Assert.Equal(["gamma.job", "gamma.report", "itdesk.ticket"], await OfferedAsync(administrator));
        Assert.Equal(["gamma.job"], await OfferedAsync(await AccountAsync(database, app, "worker", "Worker")));
        Assert.Equal(["gamma.job", "gamma.report"], await OfferedAsync(await AccountAsync(database, app, "auditor", "Auditor")));
    }

    [Fact]
    public async Task An_account_without_roles_is_offered_no_document_and_every_document_answers_as_if_it_did_not_exist()
    {
        var (database, app, administrator) = await StartAsync();
        await using var _ = database; await using var __ = app;
        var id = await CreateAsync(administrator, Jobs, "Existing");
        var nobody = await AccountAsync(database, app, "mlopez");

        Assert.Empty(await OfferedAsync(nobody));
        AssertNotFound(await ApiTests.Send(nobody, HttpMethod.Get, Jobs));
        AssertNotFound(await ApiTests.Send(nobody, HttpMethod.Get, $"{Jobs}/{id}"));
        AssertNotFound(await ApiTests.Send(nobody, HttpMethod.Get, "/api/meta/gamma.job"));
        AssertNotFound(await ApiTests.Send(nobody, HttpMethod.Post, Jobs, new { title = "Sneaked in" }));
        // Exactly what a document nobody declares answers.
        AssertNotFound(await ApiTests.Send(nobody, HttpMethod.Get, "/api/docs/sales.invoice"));
        Assert.Equal(["Existing"], await TitlesAsync(administrator, Jobs));
    }

    [Fact]
    public async Task The_list_of_packages_asks_for_a_session_and_nothing_else()
    {
        var (database, app, _) = await StartAsync();
        await using var __ = database; await using var ___ = app;

        var packages = await ApiTests.Send(await AccountAsync(database, app, "mlopez"), HttpMethod.Get, "/api/packages");

        Assert.Equal(HttpStatusCode.OK, packages.Status);
        Assert.Equal(3, packages.Body.GetProperty("data").GetArrayLength());
    }

    [Fact]
    public async Task A_document_one_cannot_read_is_not_found_even_when_another_of_the_same_package_can_be_read()
    {
        var (database, app, administrator) = await StartAsync();
        await using var _ = database; await using var __ = app;
        var report = await CreateAsync(administrator, Reports, "Quarterly");
        var worker = await AccountAsync(database, app, "worker", "Worker");

        AssertNotFound(await ApiTests.Send(worker, HttpMethod.Get, Reports));
        AssertNotFound(await ApiTests.Send(worker, HttpMethod.Get, $"{Reports}/{report}"));
        AssertNotFound(await ApiTests.Send(worker, HttpMethod.Get, "/api/meta/gamma.report"));
        AssertNotFound(await ApiTests.Send(worker, HttpMethod.Post, Reports, new { title = "Mine" }));
        Assert.Equal(HttpStatusCode.OK, (await ApiTests.Send(worker, HttpMethod.Get, "/api/meta/gamma.job")).Status);
    }

    [Fact]
    public async Task Who_can_read_but_not_create_is_told_so_and_nothing_is_stored()
    {
        var (database, app, administrator) = await StartAsync();
        await using var _ = database; await using var __ = app;
        var auditor = await AccountAsync(database, app, "auditor", "Auditor");

        var refused = await ApiTests.Send(auditor, HttpMethod.Post, Jobs, new { title = "Not allowed" });

        Assert.Equal(HttpStatusCode.Forbidden, refused.Status);
        Assert.Equal("""{"ok":false,"data":null,"error":{"code":"forbidden","fields":null}}""", refused.Body.GetRawText());
        Assert.Empty(await TitlesAsync(administrator, Jobs));
    }

    [Fact]
    public async Task With_the_scope_of_ones_own_the_list_and_the_record_by_identifier_are_only_ones_own()
    {
        var (database, app, administrator) = await StartAsync();
        await using var _ = database; await using var __ = app;
        var first = await AccountAsync(database, app, "first", "Worker");
        var second = await AccountAsync(database, app, "second", "Worker");
        var mine = await CreateAsync(first, Jobs, "Of the first");
        var theirs = await CreateAsync(second, Jobs, "Of the second");
        await CreateAsync(administrator, Jobs, "Of the administrator");

        Assert.Equal(["Of the first"], await TitlesAsync(first, Jobs));
        Assert.Equal(["Of the second"], await TitlesAsync(second, Jobs));
        Assert.Equal(HttpStatusCode.OK, (await ApiTests.Send(first, HttpMethod.Get, $"{Jobs}/{mine}")).Status);
        // Somebody else's record answers exactly like an identifier that does not exist.
        AssertNotFound(await ApiTests.Send(first, HttpMethod.Get, $"{Jobs}/{theirs}"));
        AssertNotFound(await ApiTests.Send(first, HttpMethod.Get, $"{Jobs}/019a0000-0000-7000-8000-000000000000"));
    }

    [Fact]
    public async Task Whoever_reads_all_sees_the_records_of_every_account_and_with_two_scopes_the_widest_wins()
    {
        var (database, app, administrator) = await StartAsync();
        await using var _ = database; await using var __ = app;
        var worker = await AccountAsync(database, app, "worker", "Worker");
        var theirs = await CreateAsync(worker, Jobs, "Of the worker");
        await CreateAsync(administrator, Jobs, "Of the administrator");
        string[] everything = ["Of the administrator", "Of the worker"];

        Assert.Equal(everything, await TitlesAsync(await AccountAsync(database, app, "auditor", "Auditor"), Jobs));
        Assert.Equal(everything, await TitlesAsync(administrator, Jobs));
        // Worker alone reads its own; together with Auditor it reads all.
        var both = await AccountAsync(database, app, "both", "Worker", "Auditor");
        Assert.Equal(everything, await TitlesAsync(both, Jobs));
        Assert.Equal(HttpStatusCode.OK, (await ApiTests.Send(both, HttpMethod.Get, $"{Jobs}/{theirs}")).Status);
    }

    [Fact]
    public async Task Every_record_keeps_who_created_it_and_the_company_whatever_the_caller_sends()
    {
        var (database, app, administrator) = await StartAsync();
        await using var _ = database; await using var __ = app;
        var worker = await AccountAsync(database, app, "worker", "Worker");
        var other = await database.AddAccountAsync("other");

        var created = await ApiTests.Send(worker, HttpMethod.Post, Jobs, new
        {
            title = "Mine", created_by = other, updated_by = other, company_id = Guid.CreateVersion7(),
        });

        Assert.Equal(HttpStatusCode.Created, created.Status);
        Assert.Equal("worker|worker|Acme", await database.ScalarAsync<string>("""
            SELECT a.username || '|' || u.username || '|' || c.name FROM gamma.job j
            JOIN core.user_account a ON a.id = j.created_by
            JOIN core.user_account u ON u.id = j.updated_by
            JOIN core.company c ON c.id = j.company_id
            """));
        // It is its own record: it shows up in its list.
        Assert.Equal(["Mine"], await TitlesAsync(worker, Jobs));
        Assert.DoesNotContain("created_by", created.Body.GetRawText());
    }

    [Fact]
    public async Task Records_without_author_are_seen_by_who_reads_all_and_not_by_who_reads_ones_own()
    {
        var (database, app, administrator) = await StartAsync();
        await using var _ = database; await using var __ = app;
        // As the previous change left them: no author, no company.
        await database.ScalarAsync<object>(
            "INSERT INTO gamma.job (id, title, created_at, updated_at) VALUES (gen_random_uuid(), 'From before', now(), now())");

        Assert.Equal(["From before"], await TitlesAsync(administrator, Jobs));
        Assert.Equal(["From before"], await TitlesAsync(await AccountAsync(database, app, "auditor", "Auditor"), Jobs));
        Assert.Empty(await TitlesAsync(await AccountAsync(database, app, "worker", "Worker"), Jobs));
    }

    [Fact]
    public async Task Taking_the_read_permission_from_a_role_hides_the_document_from_the_next_request()
    {
        var (database, app, administrator) = await StartAsync();
        await using var _ = database; await using var __ = app;
        var auditor = await AccountAsync(database, app, "auditor", "Auditor");
        Assert.Equal(HttpStatusCode.OK, (await ApiTests.Send(auditor, HttpMethod.Get, Reports)).Status);
        var role = (await ApiTests.Send(administrator, HttpMethod.Get, "/api/roles")).Body.GetProperty("data").EnumerateArray()
            .Single(r => r.GetProperty("name").GetString() == "Auditor").GetProperty("id").GetString();

        await ApiTests.Send(administrator, HttpMethod.Patch, $"/api/roles/{role}",
            new { permissions = new[] { new { key = "gamma.job.read", scope = "all" } } });

        AssertNotFound(await ApiTests.Send(auditor, HttpMethod.Get, Reports));
        Assert.Equal(["gamma.job"], await OfferedAsync(auditor));
    }

    [Fact]
    public async Task A_role_made_to_measure_lets_see_without_creating()
    {
        var (database, app, administrator) = await StartAsync();
        await using var _ = database; await using var __ = app;
        await CreateAsync(administrator, "/api/docs/itdesk.ticket", "Printer");
        await ApiTests.Send(administrator, HttpMethod.Post, "/api/roles",
            new { name = "Ticket watcher", permissions = new[] { new { key = "itdesk.ticket.read", scope = "all" } } });

        var watcher = await AccountAsync(database, app, "watcher", "Ticket watcher");

        Assert.Equal(["Printer"], await TitlesAsync(watcher, "/api/docs/itdesk.ticket"));
        Assert.Equal(HttpStatusCode.Forbidden, (await ApiTests.Send(watcher, HttpMethod.Post, "/api/docs/itdesk.ticket", new { title = "No" })).Status);
    }
}
