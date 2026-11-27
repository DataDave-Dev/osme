// SPDX-License-Identifier: LGPL-3.0-or-later
using Npgsql;
using NpgsqlTypes;

namespace Osme.Core.Access;

/// <summary>Settings of the whole installation, for every account in every module (core.setting).</summary>
public sealed class CoreSettings(NpgsqlDataSource dataSource, TimeProvider clock)
{
    /// <summary>Whether an account may be signed in on several devices at once. True out of the box.</summary>
    public const string AllowMultipleSessions = "access.allow_multiple_sessions";

    /// <summary>The stored value, or <paramref name="fallback"/> when nobody has set it.</summary>
    public async Task<bool> GetAsync(string key, bool fallback, CancellationToken cancel = default)
    {
        await using var command = dataSource.CreateCommand("SELECT (value #>> '{}')::boolean FROM core.setting WHERE key = $1");
        command.Parameters.AddWithValue(key);
        return await command.ExecuteScalarAsync(cancel) as bool? ?? fallback;
    }

    public async Task SetAsync(string key, bool value, Guid? updatedBy, CancellationToken cancel = default)
    {
        await using var command = dataSource.CreateCommand("""
            INSERT INTO core.setting (id, key, value, updated_at, updated_by) VALUES ($1, $2, $3, $4, $5)
            ON CONFLICT ON CONSTRAINT uq_setting__key
            DO UPDATE SET value = excluded.value, updated_at = excluded.updated_at, updated_by = excluded.updated_by
            """);
        var now = clock.GetUtcNow();
        command.Parameters.AddWithValue(Guid.CreateVersion7(now));
        command.Parameters.AddWithValue(key);
        command.Parameters.AddWithValue(NpgsqlDbType.Jsonb, value ? "true" : "false");
        command.Parameters.AddWithValue(now);
        command.Parameters.AddWithValue((object?)updatedBy ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancel);
    }
}
