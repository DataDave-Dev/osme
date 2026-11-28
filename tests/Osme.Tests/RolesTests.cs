// SPDX-License-Identifier: LGPL-3.0-or-later
using Microsoft.Extensions.Logging;
using Osme.Core.Access;

namespace Osme.Tests;

/// <summary>Factory roles, what an account ends up allowed to do, and the rules of editing a role (tasks 4.3 to 4.5).</summary>
public class RolesTests
{
    private static readonly Guid? Nobody = null;

    private static Grant Read(string document, string scope = "all") => new($"{document}.read", scope);
    private static Grant Create(string document) => new($"{document}.create", "all");

    private static string[] Shown(IEnumerable<Grant> grants) =>
        [.. grants.Select(g => $"{g.Key}:{g.Scope}").Order(StringComparer.Ordinal)];

    private static IEnumerable<(string, string, string)> Shown(RoleChange change) =>
        change.Errors.Select(e => (e.Field, e.Code, string.Join(",", e.Values ?? [])));

    // ---- 4.3 Factory roles ----

    [Fact]
    public async Task The_first_load_of_a_package_creates_its_factory_roles_ready_to_assign()
    {
        await using var db = await RolesDatabase.CreateAsync();

        var roles = await db.Roles.ListAsync();

        Assert.Equal(["Auditor", "core.administrator", "Worker"], roles.Select(r => r.Name).Order(StringComparer.OrdinalIgnoreCase));
        var worker = roles.Single(r => r.Name == "Worker");
        Assert.Equal(("gamma.worker", true, false), (worker.Origin, worker.IsActive, worker.IsSystem));
        Assert.Equal(["gamma.job.create:all", "gamma.job.read:own"], Shown(worker.Permissions));
        Assert.Equal(["gamma.job.read:all", "gamma.report.read:all"], Shown(roles.Single(r => r.Name == "Auditor").Permissions));
    }

    [Fact]
    public async Task An_edited_factory_role_survives_a_restart_and_no_second_one_appears()
    {
        await using var db = await RolesDatabase.CreateAsync();
        var worker = await db.RoleAsync("Worker");
        await db.Roles.UpdateAsync(worker.Id, "Field worker", [Read("gamma.job", "own")], null, Nobody);

        await db.StartAsync(Repo.RolePackages);

        var roles = await db.Roles.ListAsync();
        Assert.Equal(3, roles.Count);
        var edited = roles.Single(r => r.Origin == "gamma.worker");
        Assert.Equal("Field worker", edited.Name);
        Assert.Equal(["gamma.job.read:own"], Shown(edited.Permissions));
    }

    [Fact]
    public async Task A_deactivated_factory_role_does_not_come_back()
    {
        await using var db = await RolesDatabase.CreateAsync();
        await db.Roles.UpdateAsync((await db.RoleAsync("Auditor")).Id, null, null, false, Nobody);

        await db.StartAsync(Repo.RolePackages);

        var roles = await db.Roles.ListAsync();
        Assert.Equal(3, roles.Count);
        Assert.False(roles.Single(r => r.Origin == "gamma.auditor").IsActive);
    }

    [Fact]
    public async Task A_factory_role_whose_name_is_taken_arrives_with_its_package_in_brackets_and_the_log_says_so()
    {
        await using var db = await RolesDatabase.CreateAsync(Repo.LoaderPackages); // no gamma yet
        await db.Roles.CreateAsync("WORKER", [], Nobody);

        await db.StartAsync(Repo.RolePackages);

        var worker = (await db.Roles.ListAsync()).Single(r => r.Origin == "gamma.worker");
        Assert.Equal("Worker (gamma)", worker.Name);
        Assert.Equal(["gamma.job.create:all", "gamma.job.read:own"], Shown(worker.Permissions));
        Assert.Contains(db.Log.Entries, e =>
            e.Level == LogLevel.Warning && e.Message.Contains("gamma.worker") && e.Message.Contains("Worker (gamma)"));
    }

    // ---- 4.4 What an account is allowed ----

    [Fact]
    public async Task An_account_without_roles_is_allowed_nothing()
    {
        await using var db = await RolesDatabase.CreateAsync();

        Assert.Empty(await db.PermissionsOfAsync(await db.AccountWithAsync("mlopez")));
    }

    [Fact]
    public async Task The_permissions_of_an_account_are_the_sum_of_its_roles()
    {
        await using var db = await RolesDatabase.CreateAsync();
        await db.Roles.CreateAsync("Report reader", [Read("gamma.report")], Nobody);
        await db.Roles.CreateAsync("Report writer", [Read("gamma.report", "own"), Create("gamma.report"), new("core.users", "all")], Nobody);

        var account = await db.AccountWithAsync("mlopez", "Worker", "Report reader", "Report writer");

        Assert.Equal(
            ["core.users:all", "gamma.job.create:all", "gamma.job.read:own", "gamma.report.create:all", "gamma.report.read:all"],
            await db.PermissionsOfAsync(account));
    }

    [Fact]
    public async Task With_two_scopes_on_the_same_document_the_widest_wins()
    {
        await using var db = await RolesDatabase.CreateAsync();

        // Worker reads its own jobs; Auditor reads all of them.
        var account = await db.AccountWithAsync("mlopez", "Worker", "Auditor");

        Assert.Contains("gamma.job.read:all", await db.PermissionsOfAsync(account));
        Assert.DoesNotContain("gamma.job.read:own", await db.PermissionsOfAsync(account));
    }

    [Fact]
    public async Task A_deactivated_role_grants_nothing_and_reactivated_it_grants_again_to_the_same_accounts()
    {
        await using var db = await RolesDatabase.CreateAsync();
        var account = await db.AccountWithAsync("mlopez", "Auditor");
        var auditor = await db.RoleAsync("Auditor");

        await db.Roles.UpdateAsync(auditor.Id, null, null, false, Nobody);
        Assert.Empty(await db.PermissionsOfAsync(account));
        Assert.Empty((await db.Accounts.FindAsync(account))!.Roles);

        await db.Roles.UpdateAsync(auditor.Id, null, null, true, Nobody);
        Assert.Equal(["gamma.job.read:all", "gamma.report.read:all"], await db.PermissionsOfAsync(account));
    }

    [Fact]
    public async Task Editing_a_role_changes_what_its_accounts_are_allowed_at_once()
    {
        await using var db = await RolesDatabase.CreateAsync();
        var account = await db.AccountWithAsync("mlopez", "Auditor");

        await db.Roles.UpdateAsync((await db.RoleAsync("Auditor")).Id, null, [Read("gamma.job")], null, Nobody);

        Assert.Equal(["gamma.job.read:all"], await db.PermissionsOfAsync(account));
    }

    [Fact]
    public async Task The_administrator_is_allowed_everything_in_the_catalog_even_what_no_role_grants()
    {
        await using var db = await RolesDatabase.CreateAsync();
        var account = await db.AccountWithAsync("root", "core.administrator");

        var found = (await db.Accounts.FindAsync(account))!;

        Assert.True(found.IsAdministrator);
        Assert.Equal(
            ["core.access_settings:all", "core.roles:all", "core.users:all",
             "gamma.job.create:all", "gamma.job.read:all", "gamma.report.create:all", "gamma.report.read:all"],
            await db.PermissionsOfAsync(account));
        // gamma.report.create is granted by no role: only the administrator has it.
        Assert.Equal("all", found.ScopeOf("gamma.report.create"));
        Assert.Null((await db.Accounts.FindAsync(await db.AccountWithAsync("mlopez", "Worker", "Auditor")))!.ScopeOf("gamma.report.create"));
    }

    [Fact]
    public async Task The_administrator_gets_the_documents_of_a_package_loaded_later_without_touching_any_role()
    {
        await using var db = await RolesDatabase.CreateAsync(Repo.LoaderPackages);
        var account = await db.AccountWithAsync("root", "core.administrator");
        Assert.DoesNotContain("gamma.job.read:all", await db.PermissionsOfAsync(account));

        await db.StartAsync(Repo.RolePackages);

        Assert.Contains("gamma.job.read:all", await db.PermissionsOfAsync(account));
    }

    [Fact]
    public async Task Permissions_of_a_package_that_is_not_loaded_grant_nothing_are_kept_and_come_back_with_it()
    {
        await using var db = await RolesDatabase.CreateAsync();
        var account = await db.AccountWithAsync("mlopez", "Worker");

        // The server starts without gamma: the account still exists, with nothing from gamma.
        await db.StartAsync(Repo.LoaderPackages);
        Assert.Empty(await db.PermissionsOfAsync(account));
        var hidden = (await db.Roles.ListAsync()).Single(r => r.Origin == "gamma.worker");
        Assert.Empty(hidden.Permissions);

        // Saving the role meanwhile does not lose what it cannot show.
        await db.Roles.UpdateAsync(hidden.Id, "Field worker", [new("core.users", "all")], null, Nobody);
        Assert.Equal(["core.users:all"], await db.PermissionsOfAsync(account));

        await db.StartAsync(Repo.RolePackages);
        Assert.Equal(["core.users:all", "gamma.job.create:all", "gamma.job.read:own"], await db.PermissionsOfAsync(account));
    }

    // ---- 4.5 Creating and editing roles ----

    [Fact]
    public async Task A_role_is_created_with_any_mix_of_the_catalog_and_names_its_author()
    {
        await using var db = await RolesDatabase.CreateAsync();
        var author = await db.AccountWithAsync("root", "core.administrator");

        var change = await db.Roles.CreateAsync("  Front desk ", [Read("gamma.job", "own"), new("core.users", "all")], author);

        Assert.Equal(RoleChangeStatus.Done, change.Status);
        Assert.Equal(("Front desk", true, false, null), (change.Role!.Name, change.Role.IsActive, change.Role.IsSystem, change.Role.Origin));
        Assert.Equal(["core.users:all", "gamma.job.read:own"], Shown(change.Role.Permissions));
        Assert.Equal(Shown(change.Role.Permissions), Shown((await db.RoleAsync("Front desk")).Permissions));
        Assert.Equal($"{author} {author}", await db.Database.ScalarAsync<string>(
            "SELECT created_by || ' ' || updated_by FROM core.role WHERE name = 'Front desk'"));
    }

    public static TheoryData<string, string, string> BadNames() => new()
    {
        { "", "required", "" },
        { "   ", "required", "" },
        { new string('x', 101), "too_long", "" },
        { "worker", "taken", "" },
        { " AUDITOR ", "taken", "" },
        { "Core.Administrator", "taken", "" },
    };

    [Theory]
    [MemberData(nameof(BadNames))]
    public async Task A_role_name_is_required_fits_in_100_characters_and_is_never_repeated(string name, string code, string values)
    {
        await using var db = await RolesDatabase.CreateAsync();

        var change = await db.Roles.CreateAsync(name, [], Nobody);

        Assert.Equal(RoleChangeStatus.Invalid, change.Status);
        Assert.Equal([("name", code, values)], Shown(change));
        Assert.Equal(3, (await db.Roles.ListAsync()).Count);
    }

    [Fact]
    public async Task The_name_of_a_deactivated_role_stays_taken()
    {
        await using var db = await RolesDatabase.CreateAsync();
        await db.Roles.UpdateAsync((await db.RoleAsync("Auditor")).Id, null, null, false, Nobody);

        var change = await db.Roles.CreateAsync("auditor", [], Nobody);

        Assert.Equal([("name", "taken", "")], Shown(change));
    }

    [Fact]
    public async Task A_role_can_keep_its_own_name_and_a_name_of_100_characters_fits()
    {
        await using var db = await RolesDatabase.CreateAsync();
        var worker = await db.RoleAsync("Worker");

        Assert.Equal(RoleChangeStatus.Done, (await db.Roles.UpdateAsync(worker.Id, "WORKER", null, null, Nobody)).Status);
        Assert.Equal(RoleChangeStatus.Done, (await db.Roles.UpdateAsync(worker.Id, new string('x', 100), null, null, Nobody)).Status);
    }

    [Fact]
    public async Task A_permission_outside_the_catalog_is_refused_naming_it_and_the_role_stays_as_it_was()
    {
        await using var db = await RolesDatabase.CreateAsync();
        var worker = await db.RoleAsync("Worker");

        var change = await db.Roles.UpdateAsync(
            worker.Id, "Renamed", [Read("gamma.job"), Read("itdesk.ticket"), new("gamma.job.delete", "all")], null, Nobody);

        Assert.Equal(RoleChangeStatus.Invalid, change.Status);
        Assert.Equal([("permissions", "unknown_permission", "itdesk.ticket.read,gamma.job.delete")], Shown(change));
        var after = await db.RoleAsync("Worker");
        Assert.Equal(["gamma.job.create:all", "gamma.job.read:own"], Shown(after.Permissions));
    }

    [Fact]
    public async Task Creating_without_reading_is_refused_naming_the_document()
    {
        await using var db = await RolesDatabase.CreateAsync();

        var change = await db.Roles.CreateAsync(
            "Writer", [Create("gamma.job"), Create("gamma.report"), Read("gamma.report", "own")], Nobody);

        Assert.Equal([("permissions", "create_without_read", "gamma.job")], Shown(change));
        Assert.Equal(3, (await db.Roles.ListAsync()).Count);
    }

    [Fact]
    public async Task A_scope_that_does_not_exist_or_does_not_apply_is_refused_naming_the_permission()
    {
        await using var db = await RolesDatabase.CreateAsync();

        var change = await db.Roles.CreateAsync("Odd", [
            new("gamma.job.read", "some"), new("gamma.report.read", ""),
            new("gamma.report.create", "own"), new("core.users", "own"),
        ], Nobody);

        Assert.Equal(
            [("permissions", "invalid_scope", "gamma.job.read,gamma.report.read,gamma.report.create,core.users")], Shown(change));
    }

    [Fact]
    public async Task Every_problem_of_a_change_is_reported_at_once()
    {
        await using var db = await RolesDatabase.CreateAsync();

        var change = await db.Roles.CreateAsync("", [new("nothing.here", "all"), Create("gamma.job")], Nobody);

        Assert.Equal(
            [("name", "required", ""), ("permissions", "unknown_permission", "nothing.here"),
             ("permissions", "create_without_read", "gamma.job")],
            Shown(change));
    }

    [Fact]
    public async Task What_a_change_does_not_mention_stays_as_it_was()
    {
        await using var db = await RolesDatabase.CreateAsync();
        var worker = await db.RoleAsync("Worker");

        var renamed = await db.Roles.UpdateAsync(worker.Id, "Field worker", null, null, Nobody);
        var deactivated = await db.Roles.UpdateAsync(worker.Id, null, null, false, Nobody);

        Assert.Equal(["gamma.job.create:all", "gamma.job.read:own"], Shown(renamed.Role!.Permissions));
        Assert.Equal(("Field worker", false), (deactivated.Role!.Name, deactivated.Role.IsActive));
        Assert.Equal(["gamma.job.create:all", "gamma.job.read:own"], Shown(deactivated.Role.Permissions));
        Assert.Equal(3, await db.Database.ScalarAsync<int>("SELECT version FROM core.role WHERE origin = 'gamma.worker'"));
    }

    [Fact]
    public async Task The_permissions_of_a_change_replace_the_whole_set()
    {
        await using var db = await RolesDatabase.CreateAsync();
        var worker = await db.RoleAsync("Worker");

        var change = await db.Roles.UpdateAsync(worker.Id, null, [Read("gamma.report")], null, Nobody);
        var emptied = await db.Roles.UpdateAsync(worker.Id, null, [], null, Nobody);

        Assert.Equal(["gamma.report.read:all"], Shown(change.Role!.Permissions));
        Assert.Empty(emptied.Role!.Permissions);
    }

    [Fact]
    public async Task The_administrator_role_is_shown_with_the_whole_catalog_and_cannot_be_touched()
    {
        await using var db = await RolesDatabase.CreateAsync();
        var administrator = await db.RoleAsync("core.administrator");

        var renamed = await db.Roles.UpdateAsync(administrator.Id, "Boss", null, null, Nobody);
        var trimmed = await db.Roles.UpdateAsync(administrator.Id, null, [], null, Nobody);
        var deactivated = await db.Roles.UpdateAsync(administrator.Id, null, null, false, Nobody);

        Assert.All([renamed, trimmed, deactivated], change => Assert.Equal(RoleChangeStatus.SystemRole, change.Status));
        var after = await db.RoleAsync("core.administrator");
        Assert.Equal((true, true), (after.IsSystem, after.IsActive));
        Assert.Equal(Shown(db.Catalog.All), Shown(after.Permissions));
    }

    [Fact]
    public async Task A_role_that_does_not_exist_is_not_found()
    {
        await using var db = await RolesDatabase.CreateAsync();

        Assert.Equal(RoleChangeStatus.NotFound, (await db.Roles.UpdateAsync(Guid.CreateVersion7(), "x", null, null, Nobody)).Status);
    }
}
