// SPDX-License-Identifier: LGPL-3.0-or-later
using Npgsql;
using Osme.Core.Documents;

namespace Osme.Core.Access;

public enum SetupStatus
{
    Done,
    /// <summary>There is a company already: the setup happens once.</summary>
    AlreadyDone,
    Invalid,
}

public sealed record SetupResult(SetupStatus Status, Guid UserAccountId, IReadOnlyList<FieldError> Errors);

/// <summary>
/// The first thing an empty installation asks for: its company and the account of its
/// administrator, created together or not at all, once.
/// </summary>
public sealed class InitialSetup(NpgsqlDataSource dataSource, TimeProvider clock)
{
    public const int CompanyNameMaxLength = 200;
    public const int PersonNameMaxLength = 100;

    // Arbitrary, fixed number: two setups arriving at once take turns.
    private const long SetupLock = 0x4F534D45_53455455; // "OSMESETU"

    public async Task<bool> IsDoneAsync(CancellationToken cancel = default)
    {
        await using var command = dataSource.CreateCommand("SELECT EXISTS (SELECT 1 FROM core.company)");
        return (bool)(await command.ExecuteScalarAsync(cancel))!;
    }

    /// <summary>The company of the installation, or null while the setup has not been done.</summary>
    public async Task<Guid?> CompanyIdAsync(CancellationToken cancel = default)
    {
        await using var command = dataSource.CreateCommand("SELECT id FROM core.company ORDER BY created_at, id LIMIT 1");
        return await command.ExecuteScalarAsync(cancel) as Guid?;
    }

    public async Task<SetupResult> RunAsync(
        string companyName, string name, string username, string password, CancellationToken cancel = default)
    {
        // Checked before the data: an installation that is already set up says so and nothing else.
        if (await IsDoneAsync(cancel)) return new SetupResult(SetupStatus.AlreadyDone, Guid.Empty, []);

        companyName = companyName.Trim();
        name = name.Trim();

        var errors = new List<FieldError>();
        CheckName(errors, "company_name", companyName, CompanyNameMaxLength);
        CheckName(errors, "name", name, PersonNameMaxLength);
        errors.AddRange(UsernameRules.Check(username).Select(code => new FieldError("username", code)));
        errors.AddRange(PasswordRules.Check(password).Select(code => new FieldError("password", code)));
        if (errors.Count > 0) return new SetupResult(SetupStatus.Invalid, Guid.Empty, errors);

        // The slow part stays outside the lock.
        var hash = PasswordHasher.Hash(password);
        var now = clock.GetUtcNow();
        var accountId = Guid.CreateVersion7(now);

        await using var connection = await dataSource.OpenConnectionAsync(cancel);
        await using var transaction = await connection.BeginTransactionAsync(cancel);

        await using (var guard = new NpgsqlCommand("SELECT pg_advisory_xact_lock($1)", connection, transaction))
        {
            guard.Parameters.AddWithValue(SetupLock);
            await guard.ExecuteNonQueryAsync(cancel);
        }

        await using (var check = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM core.company)", connection, transaction))
            if ((bool)(await check.ExecuteScalarAsync(cancel))!)
                return new SetupResult(SetupStatus.AlreadyDone, Guid.Empty, []);

        // The account and the company name this same administrator as their author.
        await using (var account = new NpgsqlCommand("""
            INSERT INTO core.user_account (id, username, display_name, password_hash, created_at, created_by, updated_at, updated_by)
            VALUES ($1, $2, $3, $4, $5, $1, $5, $1)
            """, connection, transaction))
        {
            account.Parameters.AddWithValue(accountId);
            account.Parameters.AddWithValue(UsernameRules.Normalize(username));
            account.Parameters.AddWithValue(name);
            account.Parameters.AddWithValue(hash);
            account.Parameters.AddWithValue(now);
            await account.ExecuteNonQueryAsync(cancel);
        }

        await using (var company = new NpgsqlCommand("""
            INSERT INTO core.company (id, name, created_at, created_by, updated_at, updated_by)
            VALUES ($1, $2, $3, $4, $3, $4)
            """, connection, transaction))
        {
            company.Parameters.AddWithValue(Guid.CreateVersion7(now));
            company.Parameters.AddWithValue(companyName);
            company.Parameters.AddWithValue(now);
            company.Parameters.AddWithValue(accountId);
            await company.ExecuteNonQueryAsync(cancel);
        }

        await using (var role = new NpgsqlCommand("""
            INSERT INTO core.role_user_account (id, role_id, user_account_id, created_at, created_by)
            SELECT $1, id, $2, $3, $2 FROM core.role WHERE origin = $4
            """, connection, transaction))
        {
            role.Parameters.AddWithValue(Guid.CreateVersion7(now));
            role.Parameters.AddWithValue(accountId);
            role.Parameters.AddWithValue(now);
            role.Parameters.AddWithValue(CoreSchema.AdministratorRole);
            if (await role.ExecuteNonQueryAsync(cancel) != 1)
                throw new InvalidOperationException("The administrator role is missing: CoreSchema.EnsureAsync must run first.");
        }

        await transaction.CommitAsync(cancel);
        return new SetupResult(SetupStatus.Done, accountId, []);
    }

    private static void CheckName(List<FieldError> errors, string field, string value, int maxLength)
    {
        if (value.Length == 0) errors.Add(new FieldError(field, DocumentValidator.Required));
        else if (value.Length > maxLength) errors.Add(new FieldError(field, DocumentValidator.TooLong, MaxLength: maxLength));
    }
}
