// SPDX-License-Identifier: LGPL-3.0-or-later
namespace Osme.Tests;

public class SignInTests
{
    private const string Right = AccessDatabase.Password;
    private const string Wrong = "Abcdef1?";
    private static readonly TimeSpan Wait = TimeSpan.FromMinutes(15);

    [Fact]
    public async Task The_right_username_and_password_start_a_session()
    {
        await using var access = await AccessDatabase.CreateAsync();
        var account = await access.AddAccountAsync("mlopez");

        var result = await access.SignIn.SignInAsync("mlopez", Right);

        Assert.Equal(account, result!.UserAccountId);
        Assert.Equal(account, (await access.Sessions.ResolveAsync(result.Token))?.UserAccountId);
    }

    [Theory]
    [InlineData("MLopez")]
    [InlineData("  mlopez ")]
    public async Task The_username_ignores_case_and_surrounding_spaces(string typed)
    {
        await using var access = await AccessDatabase.CreateAsync();
        await access.AddAccountAsync("mlopez");

        Assert.NotNull(await access.SignIn.SignInAsync(typed, Right));
    }

    [Fact]
    public async Task A_wrong_password_an_unknown_user_and_a_deactivated_account_get_the_same_refusal()
    {
        await using var access = await AccessDatabase.CreateAsync();
        await access.AddAccountAsync("mlopez");
        await access.AddAccountAsync("gone", isActive: false);

        Assert.Null(await access.SignIn.SignInAsync("mlopez", Wrong));
        Assert.Null(await access.SignIn.SignInAsync("nobody", Right));
        Assert.Null(await access.SignIn.SignInAsync("gone", Right));
        Assert.Equal(0L, await access.SessionCountAsync());
    }

    [Fact]
    public async Task The_password_is_not_trimmed_nor_case_folded()
    {
        await using var access = await AccessDatabase.CreateAsync();
        await access.AddAccountAsync("mlopez");

        Assert.Null(await access.SignIn.SignInAsync("mlopez", Right + " "));
        Assert.Null(await access.SignIn.SignInAsync("mlopez", Right.ToUpperInvariant()));
    }

    [Fact]
    public async Task After_five_failures_in_a_row_even_the_right_password_is_refused()
    {
        await using var access = await AccessDatabase.CreateAsync();
        await access.AddAccountAsync("mlopez");

        for (var i = 0; i < 5; i++) await access.SignIn.SignInAsync("mlopez", Wrong);

        Assert.Null(await access.SignIn.SignInAsync("mlopez", Right));
    }

    [Fact]
    public async Task Once_the_fifteen_minutes_pass_the_right_password_works()
    {
        await using var access = await AccessDatabase.CreateAsync();
        await access.AddAccountAsync("mlopez");
        for (var i = 0; i < 5; i++) await access.SignIn.SignInAsync("mlopez", Wrong);

        access.Clock.Advance(Wait - TimeSpan.FromSeconds(1));
        Assert.Null(await access.SignIn.SignInAsync("mlopez", Right));
        access.Clock.Advance(TimeSpan.FromSeconds(2));

        Assert.NotNull(await access.SignIn.SignInAsync("mlopez", Right));
    }

    [Fact]
    public async Task Four_failures_do_not_lock_and_a_success_starts_the_count_again()
    {
        await using var access = await AccessDatabase.CreateAsync();
        await access.AddAccountAsync("mlopez");

        for (var i = 0; i < 4; i++) await access.SignIn.SignInAsync("mlopez", Wrong);
        Assert.NotNull(await access.SignIn.SignInAsync("mlopez", Right));
        for (var i = 0; i < 4; i++) await access.SignIn.SignInAsync("mlopez", Wrong);

        Assert.NotNull(await access.SignIn.SignInAsync("mlopez", Right));
    }

    [Fact]
    public async Task Attempts_during_the_wait_neither_count_nor_extend_it_and_afterwards_the_count_starts_from_zero()
    {
        await using var access = await AccessDatabase.CreateAsync();
        await access.AddAccountAsync("mlopez");
        for (var i = 0; i < 5; i++) await access.SignIn.SignInAsync("mlopez", Wrong);

        access.Clock.Advance(TimeSpan.FromMinutes(14));
        for (var i = 0; i < 10; i++) await access.SignIn.SignInAsync("mlopez", Wrong);
        access.Clock.Advance(TimeSpan.FromMinutes(2));

        for (var i = 0; i < 4; i++) await access.SignIn.SignInAsync("mlopez", Wrong);
        Assert.NotNull(await access.SignIn.SignInAsync("mlopez", Right));
    }

    [Fact]
    public async Task Failures_on_a_deactivated_account_change_nothing()
    {
        await using var access = await AccessDatabase.CreateAsync();
        await access.AddAccountAsync("gone", isActive: false);

        for (var i = 0; i < 6; i++) await access.SignIn.SignInAsync("gone", Wrong);

        Assert.Equal("0|", await access.Database.ScalarAsync<string>(
            "SELECT failed_attempts || '|' || coalesce(locked_until::text, '') FROM core.user_account"));
    }

    [Fact]
    public async Task The_result_says_when_the_password_must_be_changed()
    {
        await using var access = await AccessDatabase.CreateAsync();
        await access.AddAccountAsync("mlopez");
        Assert.False((await access.SignIn.SignInAsync("mlopez", Right))!.MustChangePassword);

        await access.Database.ScalarAsync<object>("UPDATE core.user_account SET must_change_password = true");

        Assert.True((await access.SignIn.SignInAsync("mlopez", Right))!.MustChangePassword);
    }
}
