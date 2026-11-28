// SPDX-License-Identifier: LGPL-3.0-or-later
using Osme.Core.Access;
using Osme.Core.Documents;

namespace Osme.Tests;

public class DocumentStoreTests
{
    private static readonly FieldDescription Subject = new("subject", "Subject", FieldType.Text, Required: true);
    private static readonly FieldDescription Kind =
        new("kind", "Kind", FieldType.Option, Required: true, Options: [new("one", "One"), new("two", "Two")], Default: "one");
    private static readonly FieldDescription CreatedAt = new("created_at", "Created", FieldType.Datetime, ReadOnly: true);

    private static DocumentDescription Document(params FieldDescription[] fields) =>
        new("sample", "request", "Request", [.. fields, CreatedAt]);

    /// <summary>Document tables point at the core, so its tables come first, as when the server starts.</summary>
    private static async Task<TestDatabase> CoreDatabaseAsync()
    {
        var database = await TestDatabase.CreateAsync();
        await CoreSchema.EnsureAsync(database.DataSource);
        return database;
    }

    private static Task<string?> ForeignKeysAsync(TestDatabase database) => database.ScalarAsync<string>("""
        SELECT coalesce(string_agg(conname || '>' || confrelid::regclass, ',' ORDER BY conname), '') FROM pg_constraint
        WHERE conrelid = 'sample.request'::regclass AND contype = 'f'
        """);

    private static Dictionary<string, string?> Values(string subject, string? kind = null, string? extra = null) =>
        new() { ["subject"] = subject, ["kind"] = kind, ["extra"] = extra };

    [Fact]
    public async Task Creates_schema_and_table_with_the_fixed_columns()
    {
        await using var database = await CoreDatabaseAsync();
        var store = new DocumentStore(database.DataSource);

        await store.EnsureStructureAsync(Document(Subject, Kind));

        var columns = await database.ScalarAsync<string>("""
            SELECT string_agg(column_name || ':' || data_type || ':' || is_nullable, ',' ORDER BY column_name)
            FROM information_schema.columns WHERE table_schema = 'sample' AND table_name = 'request'
            """);
        Assert.Equal(
            "company_id:uuid:YES,created_at:timestamp with time zone:NO,created_by:uuid:YES,custom:jsonb:NO,"
            + "id:uuid:NO,kind:text:NO,subject:text:NO,updated_at:timestamp with time zone:NO,updated_by:uuid:YES,version:integer:NO",
            columns);
        var constraints = await database.ScalarAsync<string>("""
            SELECT string_agg(conname, ',' ORDER BY conname) FROM pg_constraint
            WHERE conrelid = 'sample.request'::regclass AND contype IN ('p', 'c')
            """);
        Assert.Equal("ck_request__kind,pk_request", constraints);
    }

    [Fact]
    public async Task A_second_start_with_one_more_field_keeps_the_records()
    {
        await using var database = await CoreDatabaseAsync();
        var store = new DocumentStore(database.DataSource);
        var before = Document(Subject);
        await store.EnsureStructureAsync(before);
        var old = await store.InsertAsync(before, Values("kept"));

        var after = Document(Subject, Kind, new FieldDescription("extra", "Extra", FieldType.Text, Required: true));
        await store.EnsureStructureAsync(after);
        await store.EnsureStructureAsync(after);
        var added = await store.InsertAsync(after, Values("new", "two", "filled"));

        var kept = await store.GetAsync(after, (Guid)old["id"]!);
        Assert.Equal(("kept", null, "one"), (kept!["subject"], kept["extra"], kept["kind"]));
        Assert.Equal(("two", "filled"), (added["kind"], added["extra"]));
    }

    [Fact]
    public async Task Inserts_reads_by_identifier_and_lists_newest_first()
    {
        await using var database = await CoreDatabaseAsync();
        var store = new DocumentStore(database.DataSource);
        var document = Document(Subject, Kind);
        await store.EnsureStructureAsync(document);
        var before = DateTime.UtcNow.AddSeconds(-1);

        var first = await store.InsertAsync(document, Values("first", "one"));
        var second = await store.InsertAsync(document, Values("second", "two"));
        var third = await store.InsertAsync(document, Values("it's \"quoted\"; DROP TABLE sample.request", "one"));

        var id = Assert.IsType<Guid>(first["id"]);
        Assert.Equal(7, id.Version);
        Assert.InRange(Assert.IsType<DateTime>(first["created_at"]), before, DateTime.UtcNow.AddSeconds(1));
        Assert.Equal("first", (await store.GetAsync(document, id))!["subject"]);
        Assert.Null(await store.GetAsync(document, Guid.CreateVersion7()));

        var list = await store.ListAsync(document);
        Assert.Equal([third["id"], second["id"], first["id"]], list.Select(r => r["id"]));
        Assert.Equal("it's \"quoted\"; DROP TABLE sample.request", list[0]["subject"]);
    }

    [Fact]
    public async Task A_new_table_points_at_the_company_and_the_accounts_of_the_core_each_with_its_index()
    {
        await using var database = await CoreDatabaseAsync();

        await new DocumentStore(database.DataSource).EnsureStructureAsync(Document(Subject));

        Assert.Equal(
            "fk_request__company_id>core.company,fk_request__created_by>core.user_account,fk_request__updated_by>core.user_account",
            await ForeignKeysAsync(database));
        Assert.Equal("ix_request__company_id,ix_request__created_by,ix_request__updated_by", await database.ScalarAsync<string>("""
            SELECT string_agg(indexname, ',' ORDER BY indexname) FROM pg_indexes
            WHERE schemaname = 'sample' AND tablename = 'request' AND indexname LIKE 'ix_%'
            """));
    }

    [Fact]
    public async Task A_table_of_the_previous_generator_gains_the_foreign_keys_and_keeps_its_records_without_author()
    {
        await using var database = await CoreDatabaseAsync();
        // As change 0-1-1 created it: the three columns, no foreign key, and a record nobody authored.
        await database.ScalarAsync<object>("""
            CREATE SCHEMA sample;
            CREATE TABLE sample.request (
                id uuid NOT NULL, company_id uuid, created_at timestamptz NOT NULL, created_by uuid,
                updated_at timestamptz NOT NULL, updated_by uuid, version integer NOT NULL DEFAULT 1,
                custom jsonb NOT NULL DEFAULT '{}', subject text NOT NULL,
                CONSTRAINT pk_request PRIMARY KEY (id));
            INSERT INTO sample.request (id, created_at, updated_at, subject) VALUES (gen_random_uuid(), now(), now(), 'from before');
            """);
        Assert.Equal("", await ForeignKeysAsync(database));
        var store = new DocumentStore(database.DataSource);
        var document = Document(Subject);

        await store.EnsureStructureAsync(document);
        await store.EnsureStructureAsync(document);

        Assert.Equal(
            "fk_request__company_id>core.company,fk_request__created_by>core.user_account,fk_request__updated_by>core.user_account",
            await ForeignKeysAsync(database));
        Assert.Equal("from before||", await database.ScalarAsync<string>(
            "SELECT subject || '|' || coalesce(created_by::text, '') || '|' || coalesce(company_id::text, '') FROM sample.request"));
        Assert.Equal("from before", Assert.Single(await store.ListAsync(document))["subject"]);
    }

    [Fact]
    public async Task A_record_is_stored_with_its_author_and_company_and_can_be_listed_and_read_only_by_its_author()
    {
        await using var database = await CoreDatabaseAsync();
        var store = new DocumentStore(database.DataSource);
        var document = Document(Subject);
        await store.EnsureStructureAsync(document);
        var first = await database.AddAccountAsync("first");
        var second = await database.AddAccountAsync("second");
        var company = Guid.CreateVersion7();
        await database.ScalarAsync<object>(
            $"INSERT INTO core.company (id, name, created_at, updated_at) VALUES ('{company}', 'Acme', now(), now())");

        var mine = await store.InsertAsync(document, Values("of the first"), new Author(first, company));
        var theirs = await store.InsertAsync(document, Values("of the second"), new Author(second, company));
        await store.InsertAsync(document, Values("of nobody"));

        Assert.Equal($"{first}|{first}|{company}", await database.ScalarAsync<string>(
            $"SELECT created_by || '|' || updated_by || '|' || company_id FROM sample.request WHERE id = '{mine["id"]}'"));
        Assert.Equal(["of the first"], (await store.ListAsync(document, first)).Select(r => r["subject"]));
        Assert.Equal(3, (await store.ListAsync(document)).Count);
        Assert.NotNull(await store.GetAsync(document, (Guid)mine["id"]!, first));
        Assert.Null(await store.GetAsync(document, (Guid)theirs["id"]!, first));
        Assert.NotNull(await store.GetAsync(document, (Guid)theirs["id"]!));
    }

    [Fact]
    public async Task An_author_that_is_not_an_account_is_refused_by_the_database()
    {
        await using var database = await CoreDatabaseAsync();
        var store = new DocumentStore(database.DataSource);
        var document = Document(Subject);
        await store.EnsureStructureAsync(document);

        await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
            store.InsertAsync(document, Values("orphan"), new Author(Guid.CreateVersion7(), Guid.CreateVersion7())));
    }
}
