// SPDX-License-Identifier: LGPL-3.0-or-later
using Osme.Core.Access;

namespace Osme.Tests;

public class InitialSetupTests
{
    private const string Password = AccessDatabase.Password;

    private static InitialSetup SetupOf(AccessDatabase access) => new(access.Database.DataSource, access.Clock);

    private static Task<string?> RowsAsync(AccessDatabase access) => access.Database.ScalarAsync<string>("""
        SELECT (SELECT count(*) FROM core.company) || ' ' || (SELECT count(*) FROM core.user_account)
            || ' ' || (SELECT count(*) FROM core.role_user_account)
        """);

    [Fact]
    public async Task An_empty_installation_is_not_set_up_and_a_complete_setup_creates_company_and_administrator()
    {
        await using var access = await AccessDatabase.CreateAsync();
        var setup = SetupOf(access);
        Assert.False(await setup.IsDoneAsync());

        var result = await setup.RunAsync("  Acme S.A.  ", "  María López ", " MLopez ", Password);

        Assert.Equal(SetupStatus.Done, result.Status);
        Assert.True(await setup.IsDoneAsync());
        Assert.Equal("1 1 1", await RowsAsync(access));
        Assert.Equal("Acme S.A.", await access.Database.ScalarAsync<string>("SELECT name FROM core.company"));
        Assert.Equal("mlopez|María López|true|false", await access.Database.ScalarAsync<string>(
            "SELECT username || '|' || display_name || '|' || is_active || '|' || must_change_password FROM core.user_account"));
        Assert.Equal(result.UserAccountId, await access.Database.ScalarAsync<Guid>("SELECT id FROM core.user_account"));
    }

    [Fact]
    public async Task The_account_gets_the_administrator_role_and_can_sign_in_with_its_password()
    {
        await using var access = await AccessDatabase.CreateAsync();

        var result = await SetupOf(access).RunAsync("Acme", "María López", "mlopez", Password);

        Assert.Equal(CoreSchema.AdministratorRole, await access.Database.ScalarAsync<string>("""
            SELECT r.origin FROM core.role_user_account ru JOIN core.role r ON r.id = ru.role_id
            WHERE ru.user_account_id = (SELECT id FROM core.user_account)
            """));
        Assert.Equal(result.UserAccountId, (await access.SignIn.SignInAsync("mlopez", Password))?.UserAccountId);
        Assert.StartsWith("$argon2id$", await access.Database.ScalarAsync<string>("SELECT password_hash FROM core.user_account"));
    }

    [Fact]
    public async Task The_company_and_the_account_name_that_administrator_as_their_author()
    {
        await using var access = await AccessDatabase.CreateAsync();

        var result = await SetupOf(access).RunAsync("Acme", "María López", "mlopez", Password);

        var id = result.UserAccountId.ToString();
        Assert.Equal($"{id} {id} {id} {id} {id}", await access.Database.ScalarAsync<string>("""
            SELECT c.created_by || ' ' || c.updated_by || ' ' || u.created_by || ' ' || u.updated_by || ' ' || ru.created_by
            FROM core.company c, core.user_account u, core.role_user_account ru
            """));
    }

    [Fact]
    public async Task A_second_setup_is_refused_and_changes_nothing()
    {
        await using var access = await AccessDatabase.CreateAsync();
        var setup = SetupOf(access);
        await setup.RunAsync("Acme", "María López", "mlopez", Password);

        var second = await setup.RunAsync("Other", "Someone Else", "other", Password);

        Assert.Equal(SetupStatus.AlreadyDone, second.Status);
        Assert.Equal("1 1 1", await RowsAsync(access));
        Assert.Equal("Acme|mlopez", await access.Database.ScalarAsync<string>(
            "SELECT c.name || '|' || u.username FROM core.company c, core.user_account u"));
    }

    [Fact]
    public async Task A_second_setup_is_refused_even_when_its_data_is_invalid()
    {
        await using var access = await AccessDatabase.CreateAsync();
        var setup = SetupOf(access);
        await setup.RunAsync("Acme", "María López", "mlopez", Password);

        Assert.Equal(SetupStatus.AlreadyDone, (await setup.RunAsync("", "", "", "")).Status);
    }

    [Fact]
    public async Task A_password_that_breaks_the_rules_is_refused_naming_each_rule_and_nothing_is_created()
    {
        await using var access = await AccessDatabase.CreateAsync();
        var setup = SetupOf(access);

        var result = await setup.RunAsync("Acme", "María López", "mlopez", "abcdefg");

        Assert.Equal(SetupStatus.Invalid, result.Status);
        Assert.Equal(
            [("password", "too_short"), ("password", "missing_uppercase"), ("password", "missing_digit"), ("password", "missing_symbol")],
            result.Errors.Select(e => (e.Field, e.Code)));
        Assert.Equal("0 0 0", await RowsAsync(access));
        Assert.False(await setup.IsDoneAsync());
    }

    public static TheoryData<string, string, string, string, string> Rejections() => new()
    {
        { "", "María", "mlopez", "company_name", "required" },
        { "   ", "María", "mlopez", "company_name", "required" },
        { new string('x', 201), "María", "mlopez", "company_name", "too_long" },
        { "Acme", " ", "mlopez", "name", "required" },
        { "Acme", new string('x', 101), "mlopez", "name", "too_long" },
        { "Acme", "María", "", "username", "too_short" },
        { "Acme", "María", "ml", "username", "too_short" },
        { "Acme", "María", new string('a', 51), "username", "too_long" },
        { "Acme", "María", "m lopez", "username", "invalid_format" },
        { "Acme", "María", " m ", "username", "too_short" },
        { "Acme", "María", "maría", "username", "invalid_format" },
        { "Acme", "María", "m@lopez", "username", "invalid_format" },
    };

    [Theory]
    [MemberData(nameof(Rejections))]
    public async Task Names_and_username_outside_the_rules_are_refused_naming_the_field(
        string company, string name, string username, string field, string code)
    {
        await using var access = await AccessDatabase.CreateAsync();

        var result = await SetupOf(access).RunAsync(company, name, username, Password);

        Assert.Equal(SetupStatus.Invalid, result.Status);
        Assert.Equal([(field, code)], result.Errors.Select(e => (e.Field, e.Code)));
        Assert.Equal("0 0 0", await RowsAsync(access));
    }

    [Fact]
    public async Task Names_at_the_limit_and_a_username_with_every_allowed_character_are_accepted()
    {
        await using var access = await AccessDatabase.CreateAsync();

        // The limit is measured once trimmed.
        var result = await SetupOf(access).RunAsync(
            " " + new string('x', 200) + " ", new string('y', 100), "a.b-c_9", Password);

        Assert.Equal(SetupStatus.Done, result.Status);
    }

    [Fact]
    public async Task Every_broken_field_is_reported_at_once()
    {
        await using var access = await AccessDatabase.CreateAsync();

        var result = await SetupOf(access).RunAsync("", "", "x", "Abcdef1");

        Assert.Equal(
            [("company_name", "required"), ("name", "required"), ("username", "too_short"),
             ("password", "too_short"), ("password", "missing_symbol")],
            result.Errors.Select(e => (e.Field, e.Code)));
    }

    [Fact]
    public async Task Two_setups_at_the_same_time_leave_one_company_and_one_administrator()
    {
        await using var access = await AccessDatabase.CreateAsync();
        var setup = SetupOf(access);

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(i =>
            Task.Run(() => setup.RunAsync($"Company {i}", $"Person {i}", $"user{i}", Password))));

        Assert.Single(results, r => r.Status == SetupStatus.Done);
        Assert.Equal(7, results.Count(r => r.Status == SetupStatus.AlreadyDone));
        Assert.Equal("1 1 1", await RowsAsync(access));
    }
}
