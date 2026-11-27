// SPDX-License-Identifier: LGPL-3.0-or-later
using Osme.Core.Access;

namespace Osme.Tests;

public class CoreSchemaTests
{
    private const string CatalogColumns = "created_at,created_by,custom,id,updated_at,updated_by,version";

    [Fact]
    public async Task Two_starts_on_an_empty_database_leave_the_seven_tables_and_one_system_role()
    {
        await using var database = await TestDatabase.CreateAsync();

        await CoreSchema.EnsureAsync(database.DataSource);
        await CoreSchema.EnsureAsync(database.DataSource);

        var tables = await database.ScalarAsync<string>("""
            SELECT string_agg(table_name, ',' ORDER BY table_name)
            FROM information_schema.tables WHERE table_schema = 'core'
            """);
        Assert.Equal("company,role,role_permission,role_user_account,session,setting,user_account", tables);

        Assert.Equal(1L, await database.ScalarAsync<long>("SELECT count(*) FROM core.role WHERE is_system"));
        Assert.Equal(
            "core.administrator|core.administrator|true",
            await database.ScalarAsync<string>("SELECT name || '|' || origin || '|' || is_active FROM core.role"));
    }

    [Theory]
    [InlineData("company", CatalogColumns + ",is_active,name")]
    [InlineData("user_account", CatalogColumns
        + ",display_name,failed_attempts,is_active,locked_until,must_change_password,password_hash,username")]
    [InlineData("role", CatalogColumns + ",is_active,is_system,name,origin")]
    [InlineData("role_permission", "created_at,created_by,id,permission,role_id,scope")]
    [InlineData("role_user_account", "created_at,created_by,id,role_id,user_account_id")]
    [InlineData("session", "created_at,id,last_seen_at,token_hash,user_account_id")]
    [InlineData("setting", "id,key,updated_at,updated_by,value")]
    public async Task Each_table_has_the_agreed_columns(string table, string expected)
    {
        await using var database = await TestDatabase.CreateAsync();
        await CoreSchema.EnsureAsync(database.DataSource);

        var columns = await database.ScalarAsync<string>($"""
            SELECT string_agg(column_name, ',' ORDER BY column_name)
            FROM information_schema.columns WHERE table_schema = 'core' AND table_name = '{table}'
            """);

        Assert.Equal(string.Join(',', expected.Split(',').Order(StringComparer.Ordinal)), columns);
    }

    [Fact]
    public async Task Usernames_and_role_names_cannot_repeat_whatever_the_case()
    {
        await using var database = await TestDatabase.CreateAsync();
        await CoreSchema.EnsureAsync(database.DataSource);
        const string role = "INSERT INTO core.role (id, name, created_at, updated_at) VALUES (gen_random_uuid(), '{0}', now(), now())";
        const string account = "INSERT INTO core.user_account (id, username, display_name, password_hash, created_at, updated_at) "
            + "VALUES (gen_random_uuid(), '{0}', 'Someone', 'x', now(), now())";
        await database.ScalarAsync<object>(string.Format(role, "Auditor"));
        await database.ScalarAsync<object>(string.Format(account, "mlopez"));

        var repeatedRole = await Assert.ThrowsAsync<Npgsql.PostgresException>(
            () => database.ScalarAsync<object>(string.Format(role, "AUDITOR")));
        var repeatedAccount = await Assert.ThrowsAsync<Npgsql.PostgresException>(
            () => database.ScalarAsync<object>(string.Format(account, "mlopez")));

        Assert.Equal("uq_role__name", repeatedRole.ConstraintName);
        Assert.Equal("uq_user_account__username", repeatedAccount.ConstraintName);
    }
}
