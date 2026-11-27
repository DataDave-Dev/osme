// SPDX-License-Identifier: LGPL-3.0-or-later
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

    private static Dictionary<string, string?> Values(string subject, string? kind = null, string? extra = null) =>
        new() { ["subject"] = subject, ["kind"] = kind, ["extra"] = extra };

    [Fact]
    public async Task Creates_schema_and_table_with_the_fixed_columns()
    {
        await using var database = await TestDatabase.CreateAsync();
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
        await using var database = await TestDatabase.CreateAsync();
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
        await using var database = await TestDatabase.CreateAsync();
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
}
