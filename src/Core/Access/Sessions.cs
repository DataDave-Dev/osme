// SPDX-License-Identifier: LGPL-3.0-or-later
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace Osme.Core.Access;

/// <summary>An open session, as the rest of the server sees it.</summary>
public sealed record Session(Guid Id, Guid UserAccountId);

/// <summary>
/// Open sessions (core.session). The token only lives in the cookie: the database keeps its
/// SHA-256, so reading the table or a backup does not let anyone in.
/// </summary>
public sealed class Sessions(NpgsqlDataSource dataSource, TimeProvider clock)
{
    public static readonly TimeSpan IdleLimit = TimeSpan.FromHours(12);
    public static readonly TimeSpan TotalLimit = TimeSpan.FromDays(7);

    // The last use is written at most this often, not on every request.
    private static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(1);

    private const int TokenBytes = 32;
    private static readonly int TokenLength = Base64Url.GetEncodedLength(TokenBytes);

    private readonly CoreSettings _settings = new(dataSource, clock);

    /// <summary>Starts a session and returns its token. The caller has already checked the password.</summary>
    public async Task<string> CreateAsync(Guid userAccountId, CancellationToken cancel = default)
    {
        var now = clock.GetUtcNow();
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));
        var keepOthers = await _settings.GetAsync(CoreSettings.AllowMultipleSessions, fallback: true, cancel);

        await using var connection = await dataSource.OpenConnectionAsync(cancel);
        await using var transaction = await connection.BeginTransactionAsync(cancel);

        // Signing in sweeps this account's expired sessions; when the installation allows a
        // single session, it closes the live ones too.
        await using (var sweep = new NpgsqlCommand(keepOthers
            ? "DELETE FROM core.session WHERE user_account_id = $1 AND (last_seen_at <= $2 OR created_at <= $3)"
            : "DELETE FROM core.session WHERE user_account_id = $1", connection, transaction))
        {
            sweep.Parameters.AddWithValue(userAccountId);
            if (keepOthers)
            {
                sweep.Parameters.AddWithValue(now - IdleLimit);
                sweep.Parameters.AddWithValue(now - TotalLimit);
            }
            await sweep.ExecuteNonQueryAsync(cancel);
        }

        await using (var insert = new NpgsqlCommand("""
            INSERT INTO core.session (id, user_account_id, token_hash, created_at, last_seen_at)
            VALUES ($1, $2, $3, $4, $4)
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue(Guid.CreateVersion7(now));
            insert.Parameters.AddWithValue(userAccountId);
            insert.Parameters.AddWithValue(HashOf(token));
            insert.Parameters.AddWithValue(now);
            await insert.ExecuteNonQueryAsync(cancel);
        }

        await transaction.CommitAsync(cancel);
        return token;
    }

    /// <summary>The session behind a token, or null when there is none or it has expired.</summary>
    public async Task<Session?> ResolveAsync(string token, CancellationToken cancel = default)
    {
        if (token.Length != TokenLength) return null;
        var hash = HashOf(token);
        var now = clock.GetUtcNow();

        Guid id, userAccountId;
        DateTimeOffset createdAt, lastSeenAt;
        await using (var find = dataSource.CreateCommand(
            "SELECT id, user_account_id, created_at, last_seen_at FROM core.session WHERE token_hash = $1"))
        {
            find.Parameters.AddWithValue(hash);
            await using var reader = await find.ExecuteReaderAsync(cancel);
            if (!await reader.ReadAsync(cancel)) return null;
            id = reader.GetGuid(0);
            userAccountId = reader.GetGuid(1);
            createdAt = reader.GetFieldValue<DateTimeOffset>(2);
            lastSeenAt = reader.GetFieldValue<DateTimeOffset>(3);
        }

        if (now - lastSeenAt >= IdleLimit || now - createdAt >= TotalLimit)
        {
            await ExecuteAsync("DELETE FROM core.session WHERE id = $1", cancel, id);
            return null;
        }

        if (now - lastSeenAt > TouchInterval)
            await ExecuteAsync("UPDATE core.session SET last_seen_at = $2 WHERE id = $1", cancel, id, now);

        return new Session(id, userAccountId);
    }

    /// <summary>Signing out: ends the session of this token and no other.</summary>
    public Task CloseAsync(string token, CancellationToken cancel = default) =>
        token.Length == TokenLength
            ? ExecuteAsync("DELETE FROM core.session WHERE token_hash = $1", cancel, HashOf(token))
            : Task.CompletedTask;

    /// <summary>Ends every session of an account, optionally sparing the one that asked.</summary>
    public Task CloseAllAsync(Guid userAccountId, Guid? except = null, CancellationToken cancel = default) =>
        except is { } spared
            ? ExecuteAsync("DELETE FROM core.session WHERE user_account_id = $1 AND id <> $2", cancel, userAccountId, spared)
            : ExecuteAsync("DELETE FROM core.session WHERE user_account_id = $1", cancel, userAccountId);

    private static byte[] HashOf(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    private async Task ExecuteAsync(string sql, CancellationToken cancel, params object[] values)
    {
        await using var command = dataSource.CreateCommand(sql);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        await command.ExecuteNonQueryAsync(cancel);
    }
}
