// SPDX-License-Identifier: LGPL-3.0-or-later
using Npgsql;

namespace Osme.Core.Access;

/// <summary>A sign-in that worked.</summary>
public sealed record SignInResult(string Token, Guid UserAccountId, bool MustChangePassword);

/// <summary>Signing in with username and password, and the brake on repeated failures.</summary>
public sealed class SignIn(NpgsqlDataSource dataSource, Sessions sessions, TimeProvider clock)
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockTime = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Null when the sign-in is refused. An unknown user, a wrong password, a deactivated account
    /// and a locked one get the same answer after the same work, so nothing tells them apart.
    /// </summary>
    public async Task<SignInResult?> SignInAsync(string username, string password, CancellationToken cancel = default)
    {
        var now = clock.GetUtcNow();
        Guid id;
        bool mustChangePassword;

        await using (var connection = await dataSource.OpenConnectionAsync(cancel))
        await using (var transaction = await connection.BeginTransactionAsync(cancel))
        {
            string hash;
            bool isActive;
            int failedAttempts;
            DateTimeOffset? lockedUntil;

            // The row stays locked while the password is checked: parallel guesses cannot skip the count.
            await using (var find = new NpgsqlCommand("""
                SELECT id, password_hash, is_active, must_change_password, failed_attempts, locked_until
                FROM core.user_account WHERE username = $1 FOR UPDATE
                """, connection, transaction))
            {
                find.Parameters.AddWithValue(username.Trim().ToLowerInvariant());
                await using var reader = await find.ExecuteReaderAsync(cancel);
                if (!await reader.ReadAsync(cancel))
                {
                    PasswordHasher.VerifyAgainstNothing(password);
                    return null;
                }
                id = reader.GetGuid(0);
                hash = reader.GetString(1);
                isActive = reader.GetBoolean(2);
                mustChangePassword = reader.GetBoolean(3);
                failedAttempts = reader.GetInt32(4);
                lockedUntil = reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5);
            }

            // Attempts on a deactivated or waiting account are refused without counting or extending the wait.
            if (!isActive || lockedUntil > now)
            {
                PasswordHasher.VerifyAgainstNothing(password);
                return null;
            }

            var isRight = PasswordHasher.Verify(password, hash);

            // Once a wait is over the count starts from zero.
            var failures = isRight ? 0 : (lockedUntil is null ? failedAttempts : 0) + 1;
            var locks = failures >= MaxFailedAttempts;

            await using (var update = new NpgsqlCommand(
                "UPDATE core.user_account SET failed_attempts = $2, locked_until = $3 WHERE id = $1", connection, transaction))
            {
                update.Parameters.AddWithValue(id);
                update.Parameters.AddWithValue(locks ? 0 : failures);
                update.Parameters.AddWithValue(locks ? now + LockTime : DBNull.Value);
                await update.ExecuteNonQueryAsync(cancel);
            }
            await transaction.CommitAsync(cancel);

            if (!isRight) return null;
        }

        return new SignInResult(await sessions.CreateAsync(id, cancel), id, mustChangePassword);
    }
}
