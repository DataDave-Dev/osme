// SPDX-License-Identifier: LGPL-3.0-or-later
using Osme.Core.Access;
using Osme.Core.Packages;

namespace Osme.Tests;

/// <summary>A database with the core tables and the roles of a set of packages, as the server leaves it after starting.</summary>
public sealed class RolesDatabase : IAsyncDisposable
{
    public AccessDatabase Access { get; private init; } = null!;
    public CapturingLogger Log { get; } = new();
    public PermissionCatalog Catalog { get; private set; } = null!;
    public Roles Roles { get; private set; } = null!;
    public Accounts Accounts { get; private set; } = null!;

    public TestDatabase Database => Access.Database;

    public static async Task<RolesDatabase> CreateAsync(string? packagesPath = null)
    {
        var roles = new RolesDatabase { Access = await AccessDatabase.CreateAsync() };
        await roles.StartAsync(packagesPath ?? Repo.RolePackages);
        return roles;
    }

    /// <summary>What a start of the server does with the packages of a folder: catalog and factory roles.</summary>
    public async Task StartAsync(string packagesPath)
    {
        var packages = PackageLoader.Load(packagesPath, Log);
        Catalog = new PermissionCatalog(packages);
        Roles = new Roles(Database.DataSource, Catalog, Access.Clock);
        Accounts = new Accounts(Database.DataSource, Access.Sessions, Catalog, Access.Clock);
        await Roles.SeedAsync(packages.SelectMany(p => p.FactoryRoles), Log);
    }

    public async Task<Role> RoleAsync(string name) => (await Roles.ListAsync()).Single(r => r.Name == name);

    public async Task<Guid> AccountWithAsync(string username, params string[] roleNames)
    {
        var account = await Database.AddAccountAsync(username);
        foreach (var name in roleNames) await Database.GiveRoleAsync(account, name);
        return account;
    }

    /// <summary>The permissions of an account as "key:scope", in order.</summary>
    public async Task<string[]> PermissionsOfAsync(Guid account) =>
        [.. (await Accounts.FindAsync(account))!.Permissions.Select(p => $"{p.Key}:{p.Scope}").Order(StringComparer.Ordinal)];

    public ValueTask DisposeAsync() => Access.DisposeAsync();
}

public static class TestRoles
{
    // ponytail: roles are assigned by hand until the task that does it (6.1) exists.
    public static async Task GiveRoleAsync(this TestDatabase database, Guid account, string roleName)
    {
        await using var command = database.DataSource.CreateCommand("""
            INSERT INTO core.role_user_account (id, role_id, user_account_id, created_at)
            SELECT $1, id, $2, now() FROM core.role WHERE name = $3
            """);
        command.Parameters.AddWithValue(Guid.CreateVersion7());
        command.Parameters.AddWithValue(account);
        command.Parameters.AddWithValue(roleName);
        if (await command.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException($"There is no role named {roleName}.");
    }
}
