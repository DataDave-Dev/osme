// SPDX-License-Identifier: LGPL-3.0-or-later
using Osme.Core.Access;

namespace Osme.Tests;

public class SessionsTests
{
    [Fact]
    public async Task A_created_session_resolves_to_its_account_and_only_its_hash_is_stored()
    {
        await using var access = await AccessDatabase.CreateAsync();
        var account = await access.AddAccountAsync("mlopez");

        var token = await access.Sessions.CreateAsync(account);

        Assert.Equal(account, (await access.Sessions.ResolveAsync(token))?.UserAccountId);
        Assert.Equal(43, token.Length); // 32 bytes, base64url
        Assert.Equal(32, await access.Database.ScalarAsync<int>("SELECT octet_length(token_hash) FROM core.session"));
        Assert.Equal(0L, await access.Database.ScalarAsync<long>(
            $"SELECT count(*) FROM core.session WHERE encode(token_hash, 'escape') LIKE '%{token}%'"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task An_unknown_or_malformed_token_resolves_to_nothing(string token)
    {
        await using var access = await AccessDatabase.CreateAsync();
        await access.Sessions.CreateAsync(await access.AddAccountAsync("mlopez"));

        Assert.Null(await access.Sessions.ResolveAsync(token));
    }

    [Fact]
    public async Task A_session_unused_for_twelve_hours_expires_and_its_row_goes_away()
    {
        await using var access = await AccessDatabase.CreateAsync();
        var token = await access.Sessions.CreateAsync(await access.AddAccountAsync("mlopez"));

        access.Clock.Advance(TimeSpan.FromHours(12) + TimeSpan.FromSeconds(1));

        Assert.Null(await access.Sessions.ResolveAsync(token));
        Assert.Equal(0L, await access.SessionCountAsync());
    }

    [Fact]
    public async Task Using_a_session_keeps_it_alive_past_twelve_hours()
    {
        await using var access = await AccessDatabase.CreateAsync();
        var token = await access.Sessions.CreateAsync(await access.AddAccountAsync("mlopez"));

        access.Clock.Advance(TimeSpan.FromHours(11));
        Assert.NotNull(await access.Sessions.ResolveAsync(token));
        access.Clock.Advance(TimeSpan.FromHours(11));

        Assert.NotNull(await access.Sessions.ResolveAsync(token));
    }

    [Fact]
    public async Task A_session_ends_seven_days_after_it_started_however_much_it_is_used()
    {
        await using var access = await AccessDatabase.CreateAsync();
        var token = await access.Sessions.CreateAsync(await access.AddAccountAsync("mlopez"));

        for (var hours = 0; hours < 7 * 24; hours += 6)
        {
            Assert.NotNull(await access.Sessions.ResolveAsync(token));
            access.Clock.Advance(TimeSpan.FromHours(6));
        }

        Assert.Null(await access.Sessions.ResolveAsync(token));
    }

    [Fact]
    public async Task The_last_use_is_written_at_most_once_a_minute()
    {
        await using var access = await AccessDatabase.CreateAsync();
        var token = await access.Sessions.CreateAsync(await access.AddAccountAsync("mlopez"));
        var start = access.Clock.Now;
        Task<DateTime> LastSeen() => access.Database.ScalarAsync<DateTime>("SELECT last_seen_at FROM core.session");

        access.Clock.Advance(TimeSpan.FromSeconds(59));
        await access.Sessions.ResolveAsync(token);
        Assert.Equal(start.UtcDateTime, await LastSeen());

        access.Clock.Advance(TimeSpan.FromSeconds(2));
        await access.Sessions.ResolveAsync(token);
        Assert.Equal(access.Clock.Now.UtcDateTime, await LastSeen());
    }

    [Fact]
    public async Task Closing_a_session_ends_that_one_only()
    {
        await using var access = await AccessDatabase.CreateAsync();
        var account = await access.AddAccountAsync("mlopez");
        var computer = await access.Sessions.CreateAsync(account);
        var phone = await access.Sessions.CreateAsync(account);

        await access.Sessions.CloseAsync(computer);

        Assert.Null(await access.Sessions.ResolveAsync(computer));
        Assert.NotNull(await access.Sessions.ResolveAsync(phone));
    }

    [Fact]
    public async Task Closing_all_sessions_of_an_account_leaves_the_others_alone()
    {
        await using var access = await AccessDatabase.CreateAsync();
        var leaving = await access.AddAccountAsync("mlopez");
        var first = await access.Sessions.CreateAsync(leaving);
        var second = await access.Sessions.CreateAsync(leaving);
        var other = await access.Sessions.CreateAsync(await access.AddAccountAsync("jperez"));

        await access.Sessions.CloseAllAsync(leaving);

        Assert.Null(await access.Sessions.ResolveAsync(first));
        Assert.Null(await access.Sessions.ResolveAsync(second));
        Assert.NotNull(await access.Sessions.ResolveAsync(other));
    }

    [Fact]
    public async Task Closing_all_but_one_keeps_the_session_that_asked()
    {
        await using var access = await AccessDatabase.CreateAsync();
        var account = await access.AddAccountAsync("mlopez");
        var here = await access.Sessions.CreateAsync(account);
        var elsewhere = await access.Sessions.CreateAsync(account);
        var current = (await access.Sessions.ResolveAsync(here))!;

        await access.Sessions.CloseAllAsync(account, except: current.Id);

        Assert.NotNull(await access.Sessions.ResolveAsync(here));
        Assert.Null(await access.Sessions.ResolveAsync(elsewhere));
    }

    [Fact]
    public async Task Out_of_the_box_an_account_can_be_signed_in_on_two_devices()
    {
        await using var access = await AccessDatabase.CreateAsync();
        var account = await access.AddAccountAsync("mlopez");

        var computer = await access.Sessions.CreateAsync(account);
        var phone = await access.Sessions.CreateAsync(account);

        Assert.NotNull(await access.Sessions.ResolveAsync(computer));
        Assert.NotNull(await access.Sessions.ResolveAsync(phone));
    }

    [Fact]
    public async Task When_the_installation_allows_one_session_a_second_sign_in_closes_the_first()
    {
        await using var access = await AccessDatabase.CreateAsync();
        var account = await access.AddAccountAsync("mlopez");
        var other = await access.Sessions.CreateAsync(await access.AddAccountAsync("jperez"));
        var settings = new CoreSettings(access.Database.DataSource, access.Clock);
        await settings.SetAsync(CoreSettings.AllowMultipleSessions, false, updatedBy: account);
        await settings.SetAsync(CoreSettings.AllowMultipleSessions, false, updatedBy: account);

        var computer = await access.Sessions.CreateAsync(account);
        var phone = await access.Sessions.CreateAsync(account);

        Assert.Null(await access.Sessions.ResolveAsync(computer));
        Assert.NotNull(await access.Sessions.ResolveAsync(phone));
        Assert.NotNull(await access.Sessions.ResolveAsync(other));
        Assert.Equal(1L, await access.Database.ScalarAsync<long>("SELECT count(*) FROM core.setting"));
    }

    [Fact]
    public async Task Signing_in_clears_the_expired_sessions_of_that_account()
    {
        await using var access = await AccessDatabase.CreateAsync();
        var account = await access.AddAccountAsync("mlopez");
        await access.Sessions.CreateAsync(account);
        await access.Sessions.CreateAsync(account);

        access.Clock.Advance(TimeSpan.FromHours(13));
        await access.Sessions.CreateAsync(account);

        Assert.Equal(1L, await access.SessionCountAsync());
    }
}
