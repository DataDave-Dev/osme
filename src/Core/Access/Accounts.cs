// SPDX-License-Identifier: LGPL-3.0-or-later
using Npgsql;
using Osme.Core.Documents;

namespace Osme.Core.Access;

public sealed record AccountRole(Guid Id, string Name);

/// <summary>An account as the rest of the server sees it. Never carries the password nor its hash.</summary>
public sealed record Account(
    Guid Id, string Username, string Name, bool IsActive, bool MustChangePassword,
    bool IsAdministrator, IReadOnlyList<AccountRole> Roles, IReadOnlyList<Grant> Permissions)
{
    /// <summary>The scope the account has a permission with, or null when no active role of its grants it.</summary>
    public string? ScopeOf(string permission) => Permissions.FirstOrDefault(p => p.Key == permission)?.Scope;

    public bool Can(string permission) => ScopeOf(permission) is not null;
}

/// <summary>Accounts (core.user_account) and what their owner can do with their own.</summary>
public sealed class Accounts(NpgsqlDataSource dataSource, Sessions sessions, PermissionCatalog catalog, TimeProvider clock)
{
    /// <summary>
    /// The account with its active roles and what they add up to, or null when it does not exist.
    /// Read on every request, so a change to a role or to an account holds from the next one.
    /// </summary>
    public async Task<Account?> FindAsync(Guid id, CancellationToken cancel = default)
    {
        string username, name;
        bool isActive, mustChangePassword;
        await using (var find = dataSource.CreateCommand(
            "SELECT username, display_name, is_active, must_change_password FROM core.user_account WHERE id = $1"))
        {
            find.Parameters.AddWithValue(id);
            await using var reader = await find.ExecuteReaderAsync(cancel);
            if (!await reader.ReadAsync(cancel)) return null;
            username = reader.GetString(0);
            name = reader.GetString(1);
            isActive = reader.GetBoolean(2);
            mustChangePassword = reader.GetBoolean(3);
        }

        var roles = new List<AccountRole>();
        var isAdministrator = false;
        await using (var list = dataSource.CreateCommand("""
            SELECT r.id, r.name, r.origin FROM core.role_user_account ru
            JOIN core.role r ON r.id = ru.role_id
            WHERE ru.user_account_id = $1 AND r.is_active
            ORDER BY lower(r.name)
            """))
        {
            list.Parameters.AddWithValue(id);
            await using var reader = await list.ExecuteReaderAsync(cancel);
            while (await reader.ReadAsync(cancel))
            {
                roles.Add(new AccountRole(reader.GetGuid(0), reader.GetString(1)));
                if (!reader.IsDBNull(2) && reader.GetString(2) == CoreSchema.AdministratorRole) isAdministrator = true;
            }
        }

        var permissions = isAdministrator ? catalog.All : await PermissionsOfAsync(id, cancel);
        return new Account(id, username, name, isActive, mustChangePassword, isAdministrator, roles, permissions);
    }

    /// <summary>
    /// The sum of the active roles of an account. A role never takes away what another gives:
    /// with two scopes on the same permission the widest holds. What no loaded package gives
    /// meaning to grants nothing.
    /// </summary>
    private async Task<IReadOnlyList<Grant>> PermissionsOfAsync(Guid id, CancellationToken cancel)
    {
        var widest = new Dictionary<string, string>();
        await using var list = dataSource.CreateCommand("""
            SELECT rp.permission, rp.scope FROM core.role_user_account ru
            JOIN core.role r ON r.id = ru.role_id AND r.is_active
            JOIN core.role_permission rp ON rp.role_id = r.id
            WHERE ru.user_account_id = $1
            """);
        list.Parameters.AddWithValue(id);
        await using var reader = await list.ExecuteReaderAsync(cancel);
        while (await reader.ReadAsync(cancel))
        {
            var key = reader.GetString(0);
            if (!catalog.Contains(key)) continue;
            if (reader.GetString(1) == Grant.All || !widest.ContainsKey(key)) widest[key] = reader.GetString(1);
        }
        return [.. widest.Select(p => new Grant(p.Key, p.Value))];
    }

    /// <summary>
    /// The owner changes their own password presenting the current one. Empty when it was changed;
    /// then every other session of the account is closed and the one that asked stays.
    /// </summary>
    public async Task<IReadOnlyList<FieldError>> ChangeOwnPasswordAsync(
        Guid accountId, Guid sessionId, string currentPassword, string password, CancellationToken cancel = default)
    {
        var now = clock.GetUtcNow();
        var errors = new List<FieldError>();

        await using (var connection = await dataSource.OpenConnectionAsync(cancel))
        await using (var transaction = await connection.BeginTransactionAsync(cancel))
        {
            string hash;
            int failedAttempts;
            DateTimeOffset? lockedUntil;
            await using (var find = new NpgsqlCommand(
                "SELECT password_hash, failed_attempts, locked_until FROM core.user_account WHERE id = $1 FOR UPDATE",
                connection, transaction))
            {
                find.Parameters.AddWithValue(accountId);
                await using var reader = await find.ExecuteReaderAsync(cancel);
                if (!await reader.ReadAsync(cancel)) throw new InvalidOperationException("The account of a session must exist.");
                hash = reader.GetString(0);
                failedAttempts = reader.GetInt32(1);
                lockedUntil = reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2);
            }

            // A wrong current password counts as a failed attempt, in the same count as signing in;
            // while the account waits nothing is accepted, and the answer does not tell the two apart.
            bool isRight;
            if (lockedUntil > now)
            {
                PasswordHasher.VerifyAgainstNothing(currentPassword);
                isRight = false;
            }
            else
            {
                isRight = await SignIn.VerifyCountingAsync(
                    connection, transaction, accountId, hash, failedAttempts, lockedUntil, currentPassword, now, cancel);
            }

            if (!isRight) errors.Add(new FieldError("current_password", "wrong_password"));
            errors.AddRange(PasswordRules.Check(password).Select(code => new FieldError("password", code)));
            // Only said to who proved knowing the current one.
            if (isRight && password == currentPassword) errors.Add(new FieldError("password", "same_as_current"));

            if (errors.Count == 0)
            {
                await using var update = new NpgsqlCommand("""
                    UPDATE core.user_account
                    SET password_hash = $2, must_change_password = false, updated_at = $3, updated_by = $1, version = version + 1
                    WHERE id = $1
                    """, connection, transaction);
                update.Parameters.AddWithValue(accountId);
                update.Parameters.AddWithValue(PasswordHasher.Hash(password));
                update.Parameters.AddWithValue(now);
                await update.ExecuteNonQueryAsync(cancel);
            }

            // Committed either way: a failure has to keep its count.
            await transaction.CommitAsync(cancel);
        }

        if (errors.Count == 0) await sessions.CloseAllAsync(accountId, except: sessionId, cancel);
        return errors;
    }
}
