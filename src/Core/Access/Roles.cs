// SPDX-License-Identifier: LGPL-3.0-or-later
using Microsoft.Extensions.Logging;
using Npgsql;
using Osme.Core.Documents;

namespace Osme.Core.Access;

/// <summary>A role as it is shown: only with the permissions the loaded packages give meaning to.</summary>
public sealed record Role(Guid Id, string Name, bool IsActive, bool IsSystem, string? Origin, IReadOnlyList<Grant> Permissions);

public enum RoleChangeStatus
{
    Done,
    Invalid,
    NotFound,
    /// <summary>The administrator role is never edited, renamed nor deactivated.</summary>
    SystemRole,
}

public sealed record RoleChange(RoleChangeStatus Status, Role? Role, IReadOnlyList<FieldError> Errors);

/// <summary>
/// Roles are data the customer edits (core.role, core.role_permission). A package only brings
/// factory roles, copied here the first time it loads and never written again.
/// </summary>
public sealed class Roles(NpgsqlDataSource dataSource, PermissionCatalog catalog, TimeProvider clock)
{
    public const int NameMaxLength = 100;

    // Arbitrary, fixed number: two servers starting at once take turns to create the factory roles.
    private const long SeedLock = 0x4F534D45_524F4C45; // "OSMEROLE"

    private const string UniqueViolation = "23505";

    /// <summary>
    /// Creates the factory roles that were never created. One that exists, by origin, is left
    /// alone whatever the customer did to it: renamed, edited or deactivated.
    /// </summary>
    public async Task SeedAsync(IEnumerable<FactoryRole> factoryRoles, ILogger logger, CancellationToken cancel = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancel);
        await using var transaction = await connection.BeginTransactionAsync(cancel);
        await ExecuteAsync(connection, transaction, "SELECT pg_advisory_xact_lock($1)", cancel, SeedLock);

        foreach (var factory in factoryRoles)
        {
            if (await ExistsAsync(connection, transaction, "SELECT 1 FROM core.role WHERE origin = $1", cancel, factory.Origin))
                continue;

            // A name is never repeated: when the label is taken, the role arrives with its package in brackets.
            var name = factory.Label;
            for (var attempt = 1; await IsNameTakenAsync(connection, transaction, name, null, cancel); attempt++)
                name = attempt == 1 ? $"{factory.Label} ({factory.Package})" : $"{factory.Label} ({factory.Package} {attempt})";

            var now = clock.GetUtcNow();
            var id = Guid.CreateVersion7(now);
            await ExecuteAsync(connection, transaction,
                "INSERT INTO core.role (id, name, origin, created_at, updated_at) VALUES ($1, $2, $3, $4, $4)",
                cancel, id, name, factory.Origin, now);
            await InsertGrantsAsync(connection, transaction, id, factory.Grants, null, now, cancel);

            if (name == factory.Label)
                logger.LogInformation("Created factory role {Origin} as '{Name}'", factory.Origin, name);
            else
                logger.LogWarning(
                    "Created factory role {Origin} as '{Name}': the name '{Label}' was already taken", factory.Origin, name, factory.Label);
        }

        await transaction.CommitAsync(cancel);
    }

    /// <summary>Every role, active or not, by name.</summary>
    public async Task<IReadOnlyList<Role>> ListAsync(CancellationToken cancel = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancel);
        return await ReadAsync(connection, null, null, cancel);
    }

    public async Task<RoleChange> CreateAsync(
        string name, IReadOnlyList<Grant> permissions, Guid? by, CancellationToken cancel = default)
    {
        name = name.Trim();
        await using var connection = await dataSource.OpenConnectionAsync(cancel);
        await using var transaction = await connection.BeginTransactionAsync(cancel);

        var errors = new List<FieldError>();
        await CheckNameAsync(connection, transaction, errors, name, null, cancel);
        var grants = CheckGrants(errors, permissions);
        if (errors.Count > 0) return new RoleChange(RoleChangeStatus.Invalid, null, errors);

        var now = clock.GetUtcNow();
        var id = Guid.CreateVersion7(now);
        try
        {
            await ExecuteAsync(connection, transaction, """
                INSERT INTO core.role (id, name, created_at, created_by, updated_at, updated_by) VALUES ($1, $2, $3, $4, $3, $4)
                """, cancel, id, name, now, (object?)by ?? DBNull.Value);
        }
        catch (PostgresException taken) when (taken.SqlState == UniqueViolation)
        {
            // Two people creating the same name at once: the index decides.
            return new RoleChange(RoleChangeStatus.Invalid, null, [new FieldError("name", "taken")]);
        }
        await InsertGrantsAsync(connection, transaction, id, grants, by, now, cancel);

        var role = (await ReadAsync(connection, transaction, id, cancel)).Single();
        await transaction.CommitAsync(cancel);
        return new RoleChange(RoleChangeStatus.Done, role, []);
    }

    /// <summary>What comes as null stays as it was. The permissions, when they come, replace the whole set.</summary>
    public async Task<RoleChange> UpdateAsync(
        Guid id, string? name, IReadOnlyList<Grant>? permissions, bool? isActive, Guid? by, CancellationToken cancel = default)
    {
        name = name?.Trim();
        await using var connection = await dataSource.OpenConnectionAsync(cancel);
        await using var transaction = await connection.BeginTransactionAsync(cancel);

        bool isSystem;
        await using (var find = new NpgsqlCommand("SELECT is_system FROM core.role WHERE id = $1 FOR UPDATE", connection, transaction))
        {
            find.Parameters.AddWithValue(id);
            if (await find.ExecuteScalarAsync(cancel) is not bool found) return new RoleChange(RoleChangeStatus.NotFound, null, []);
            isSystem = found;
        }
        if (isSystem) return new RoleChange(RoleChangeStatus.SystemRole, null, []);

        var errors = new List<FieldError>();
        if (name is not null) await CheckNameAsync(connection, transaction, errors, name, id, cancel);
        var grants = permissions is null ? null : CheckGrants(errors, permissions);
        if (errors.Count > 0) return new RoleChange(RoleChangeStatus.Invalid, null, errors);

        var now = clock.GetUtcNow();
        try
        {
            await ExecuteAsync(connection, transaction, """
                UPDATE core.role
                SET name = coalesce($2, name), is_active = coalesce($3, is_active), updated_at = $4, updated_by = $5, version = version + 1
                WHERE id = $1
                """, cancel, id, (object?)name ?? DBNull.Value, (object?)isActive ?? DBNull.Value, now, (object?)by ?? DBNull.Value);
        }
        catch (PostgresException taken) when (taken.SqlState == UniqueViolation)
        {
            return new RoleChange(RoleChangeStatus.Invalid, null, [new FieldError("name", "taken")]);
        }

        if (grants is not null)
        {
            // Only what the catalog knows is replaced: permissions of a package that is not loaded
            // are not shown, so they are kept for when it comes back.
            await ExecuteAsync(connection, transaction,
                "DELETE FROM core.role_permission WHERE role_id = $1 AND permission = ANY($2)",
                cancel, id, catalog.All.Select(g => g.Key).ToArray());
            await InsertGrantsAsync(connection, transaction, id, grants, by, now, cancel);
        }

        var role = (await ReadAsync(connection, transaction, id, cancel)).Single();
        await transaction.CommitAsync(cancel);
        return new RoleChange(RoleChangeStatus.Done, role, []);
    }

    private static async Task CheckNameAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, List<FieldError> errors, string name, Guid? self,
        CancellationToken cancel)
    {
        if (name.Length == 0) errors.Add(new FieldError("name", DocumentValidator.Required));
        else if (name.Length > NameMaxLength) errors.Add(new FieldError("name", DocumentValidator.TooLong, MaxLength: NameMaxLength));
        else if (await IsNameTakenAsync(connection, transaction, name, self, cancel)) errors.Add(new FieldError("name", "taken"));
    }

    /// <summary>Checks a set of permissions against the catalog and returns it without repetitions.</summary>
    private List<Grant> CheckGrants(List<FieldError> errors, IReadOnlyList<Grant> permissions)
    {
        var unknown = permissions.Where(p => !catalog.Contains(p.Key)).Select(p => p.Key).Distinct().ToList();
        if (unknown.Count > 0) errors.Add(new FieldError("permissions", "unknown_permission", Values: unknown));

        // Only reading can be narrowed to one's own records.
        var known = permissions.Where(p => catalog.Contains(p.Key)).ToList();
        var badScope = known
            .Where(p => p.Scope != Grant.All && !(p.Scope == Grant.Own && catalog.IsRead(p.Key)))
            .Select(p => p.Key).Distinct().ToList();
        if (badScope.Count > 0) errors.Add(new FieldError("permissions", "invalid_scope", Values: badScope));

        // Creating a document requires seeing it, at least one's own.
        var keys = known.Select(p => p.Key).ToHashSet();
        var blind = known
            .Select(p => catalog.DocumentOfCreate(p.Key))
            .Where(document => document is not null && !keys.Contains(PermissionCatalog.ReadOf(document)))
            .Select(document => document!).Distinct().ToList();
        if (blind.Count > 0) errors.Add(new FieldError("permissions", "create_without_read", Values: blind));

        // The same permission twice: the widest scope stays.
        return [.. known.GroupBy(p => p.Key).Select(g => new Grant(g.Key, g.Any(p => p.Scope == Grant.All) ? Grant.All : Grant.Own))];
    }

    private async Task<List<Role>> ReadAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid? only, CancellationToken cancel)
    {
        var granted = new Dictionary<Guid, List<Grant>>();
        await using (var list = new NpgsqlCommand(
            "SELECT role_id, permission, scope FROM core.role_permission WHERE $1::uuid IS NULL OR role_id = $1", connection, transaction))
        {
            list.Parameters.AddWithValue((object?)only ?? DBNull.Value);
            await using var reader = await list.ExecuteReaderAsync(cancel);
            while (await reader.ReadAsync(cancel))
            {
                var key = reader.GetString(1);
                if (!catalog.Contains(key)) continue;
                if (!granted.TryGetValue(reader.GetGuid(0), out var grants)) granted[reader.GetGuid(0)] = grants = [];
                grants.Add(new Grant(key, reader.GetString(2)));
            }
        }

        var roles = new List<Role>();
        await using (var list = new NpgsqlCommand("""
            SELECT id, name, is_active, is_system, origin FROM core.role
            WHERE $1::uuid IS NULL OR id = $1 ORDER BY lower(name)
            """, connection, transaction))
        {
            list.Parameters.AddWithValue((object?)only ?? DBNull.Value);
            await using var reader = await list.ExecuteReaderAsync(cancel);
            while (await reader.ReadAsync(cancel))
            {
                var id = reader.GetGuid(0);
                var isSystem = reader.GetBoolean(3);
                roles.Add(new Role(
                    id, reader.GetString(1), reader.GetBoolean(2), isSystem, reader.IsDBNull(4) ? null : reader.GetString(4),
                    // The administrator grants everything, whatever is loaded.
                    isSystem ? catalog.All : granted.GetValueOrDefault(id) ?? []));
            }
        }
        return roles;
    }

    private static async Task InsertGrantsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid roleId, IEnumerable<Grant> grants, Guid? by,
        DateTimeOffset now, CancellationToken cancel)
    {
        foreach (var grant in grants)
            await ExecuteAsync(connection, transaction, """
                INSERT INTO core.role_permission (id, role_id, permission, scope, created_at, created_by) VALUES ($1, $2, $3, $4, $5, $6)
                """, cancel, Guid.CreateVersion7(now), roleId, grant.Key, grant.Scope, now, (object?)by ?? DBNull.Value);
    }

    private static Task<bool> IsNameTakenAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string name, Guid? self, CancellationToken cancel) =>
        ExistsAsync(connection, transaction,
            "SELECT 1 FROM core.role WHERE lower(name) = lower($1) AND ($2::uuid IS NULL OR id <> $2)",
            cancel, name, (object?)self ?? DBNull.Value);

    private static async Task<bool> ExistsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, CancellationToken cancel, params object[] values)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        return await command.ExecuteScalarAsync(cancel) is not null;
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, CancellationToken cancel, params object[] values)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        await command.ExecuteNonQueryAsync(cancel);
    }
}
