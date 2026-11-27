// SPDX-License-Identifier: LGPL-3.0-or-later
using Osme.Core.Access;

namespace Osme.Tests;

/// <summary>A clock the test moves by hand.</summary>
public sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan time) => Now += time;
}

/// <summary>An empty database with the tables of the core and what the access tests keep reaching for.</summary>
public sealed class AccessDatabase : IAsyncDisposable
{
    public const string Password = "Abcdef1!";

    public TestDatabase Database { get; private init; } = null!;
    public TestClock Clock { get; } = new();
    public Sessions Sessions { get; private init; } = null!;
    public SignIn SignIn { get; private init; } = null!;

    public static async Task<AccessDatabase> CreateAsync()
    {
        var database = await TestDatabase.CreateAsync();
        await CoreSchema.EnsureAsync(database.DataSource);
        var clock = new TestClock();
        var sessions = new Sessions(database.DataSource, clock);
        return new AccessDatabase
        {
            Database = database,
            Clock = clock,
            Sessions = sessions,
            SignIn = new SignIn(database.DataSource, sessions, clock),
        };
    }

    // ponytail: accounts are inserted by hand until the task that creates them (6.1) exists.
    public async Task<Guid> AddAccountAsync(string username, bool isActive = true)
    {
        var id = Guid.CreateVersion7();
        await using var command = Database.DataSource.CreateCommand("""
            INSERT INTO core.user_account (id, username, display_name, password_hash, is_active, created_at, updated_at)
            VALUES ($1, $2, $2, $3, $4, now(), now())
            """);
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(username);
        command.Parameters.AddWithValue(PasswordHasher.Hash(Password));
        command.Parameters.AddWithValue(isActive);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    public Task<long> SessionCountAsync() => Database.ScalarAsync<long>("SELECT count(*) FROM core.session");

    public ValueTask DisposeAsync() => Database.DisposeAsync();
}
