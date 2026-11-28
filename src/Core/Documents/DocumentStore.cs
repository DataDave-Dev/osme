// SPDX-License-Identifier: LGPL-3.0-or-later
using System.Text;
using Npgsql;
using NpgsqlTypes;

namespace Osme.Core.Documents;

/// <summary>Who creates a record: the account of the session and the company of the installation.</summary>
public sealed record Author(Guid UserAccountId, Guid CompanyId);

/// <summary>
/// The only place that writes SQL for documents. Names come from validated declarations and are
/// quoted; every value travels as a parameter.
/// </summary>
public sealed class DocumentStore(NpgsqlDataSource dataSource)
{
    // ponytail: fixed cap, newest first. Filters, sorting and paging arrive when a screen asks for them.
    private const int ListLimit = 100;

    // Every document table points at the core, which is always installed (base-de-datos.md, section 6).
    private static readonly (string Column, string Target)[] CoreReferences =
    [
        ("company_id", "core.company"), ("created_by", "core.user_account"), ("updated_by", "core.user_account"),
    ];

    /// <summary>
    /// Creates the schema, the table, any missing column and the missing foreign keys to the core,
    /// each with its index. Never drops or alters what exists; rows with those columns empty stay as they are.
    /// </summary>
    public async Task EnsureStructureAsync(DocumentDescription document, CancellationToken cancel = default)
    {
        var table = Table(document);
        await using var connection = await dataSource.OpenConnectionAsync(cancel);
        await using var transaction = await connection.BeginTransactionAsync(cancel);

        await using (var exists = new NpgsqlCommand("SELECT to_regclass($1) IS NOT NULL", connection, transaction))
        {
            exists.Parameters.AddWithValue(table);
            var existed = (bool)(await exists.ExecuteScalarAsync(cancel))!;

            var sql = new StringBuilder();
            sql.Append($"CREATE SCHEMA IF NOT EXISTS {Quote(document.Package)};");
            sql.Append($"""
                CREATE TABLE IF NOT EXISTS {table} (
                    id          uuid        NOT NULL,
                    company_id  uuid,
                    created_at  timestamptz NOT NULL,
                    created_by  uuid,
                    updated_at  timestamptz NOT NULL,
                    updated_by  uuid,
                    version     integer     NOT NULL DEFAULT 1,
                    custom      jsonb       NOT NULL DEFAULT '{"{}"}',
                    CONSTRAINT {Quote("pk_" + document.Name)} PRIMARY KEY (id)
                );
                """);
            foreach (var field in Declared(document))
                sql.Append($"ALTER TABLE {table} ADD COLUMN IF NOT EXISTS {ColumnDefinition(document, field, existed)};");

            await using var ddl = new NpgsqlCommand(sql.ToString(), connection, transaction);
            await ddl.ExecuteNonQueryAsync(cancel);
        }

        foreach (var (column, target) in CoreReferences)
        {
            // There is no ADD CONSTRAINT IF NOT EXISTS: look first.
            var constraint = $"fk_{document.Name}__{column}";
            await using (var find = new NpgsqlCommand(
                "SELECT 1 FROM pg_constraint WHERE conrelid = to_regclass($1) AND conname = $2", connection, transaction))
            {
                find.Parameters.AddWithValue(table);
                find.Parameters.AddWithValue(constraint);
                if (await find.ExecuteScalarAsync(cancel) is not null) continue;
            }

            await using var add = new NpgsqlCommand($"""
                ALTER TABLE {table} ADD CONSTRAINT {Quote(constraint)} FOREIGN KEY ({Quote(column)}) REFERENCES {target} (id);
                CREATE INDEX IF NOT EXISTS {Quote($"ix_{document.Name}__{column}")} ON {table} ({Quote(column)});
                """, connection, transaction);
            await add.ExecuteNonQueryAsync(cancel);
        }

        await transaction.CommitAsync(cancel);
    }

    /// <summary>The author and the company are filled here, from the session; the caller cannot set them.</summary>
    public async Task<Dictionary<string, object?>> InsertAsync(
        DocumentDescription document, IReadOnlyDictionary<string, string?> values, Author? author = null,
        CancellationToken cancel = default)
    {
        var fields = Declared(document).ToList();
        var now = DateTimeOffset.UtcNow;
        var id = Guid.CreateVersion7(now);

        var columns = string.Join(", ", fields.Select(f => Quote(f.Name)));
        var parameters = string.Join(", ", fields.Select((_, i) => $"${i + 5}"));
        var sql = $"INSERT INTO {Table(document)} (id, created_at, updated_at, created_by, updated_by, company_id"
            + $"{(fields.Count > 0 ? ", " + columns : "")}) "
            + $"VALUES ($1, $2, $2, $3, $3, $4{(fields.Count > 0 ? ", " + parameters : "")})";

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(now);
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid, Value = (object?)author?.UserAccountId ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid, Value = (object?)author?.CompanyId ?? DBNull.Value });
        foreach (var field in fields)
            command.Parameters.AddWithValue((object?)values.GetValueOrDefault(field.Name) ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancel);

        return (await GetAsync(document, id, null, cancel))!;
    }

    /// <summary>
    /// The record, or null. With <paramref name="onlyCreatedBy"/>, only when that account created it:
    /// somebody else's record is as absent as one that does not exist.
    /// </summary>
    public async Task<Dictionary<string, object?>?> GetAsync(
        DocumentDescription document, Guid id, Guid? onlyCreatedBy = null, CancellationToken cancel = default)
    {
        await using var command = dataSource.CreateCommand(
            $"{Select(document)} WHERE id = $1{(onlyCreatedBy is null ? "" : " AND created_by = $2")}");
        command.Parameters.AddWithValue(id);
        if (onlyCreatedBy is { } owner) command.Parameters.AddWithValue(owner);
        return (await ReadAsync(command, cancel)).SingleOrDefault();
    }

    /// <summary>Newest first. With <paramref name="onlyCreatedBy"/>, only the records that account created.</summary>
    public async Task<List<Dictionary<string, object?>>> ListAsync(
        DocumentDescription document, Guid? onlyCreatedBy = null, CancellationToken cancel = default)
    {
        await using var command = dataSource.CreateCommand(
            $"{Select(document)}{(onlyCreatedBy is null ? "" : " WHERE created_by = $1")} ORDER BY created_at DESC, id DESC LIMIT {ListLimit}");
        if (onlyCreatedBy is { } owner) command.Parameters.AddWithValue(owner);
        return await ReadAsync(command, cancel);
    }

    private static IEnumerable<FieldDescription> Declared(DocumentDescription document) =>
        document.Fields.Where(f => !DocumentModelBuilder.FixedColumns.Contains(f.Name));

    private static string Select(DocumentDescription document) =>
        $"SELECT id, {string.Join(", ", document.Fields.Select(f => Quote(f.Name)))} FROM {Table(document)}";

    private static async Task<List<Dictionary<string, object?>>> ReadAsync(NpgsqlCommand command, CancellationToken cancel)
    {
        var rows = new List<Dictionary<string, object?>>();
        await using var reader = await command.ExecuteReaderAsync(cancel);
        while (await reader.ReadAsync(cancel))
        {
            var row = new Dictionary<string, object?>(reader.FieldCount);
            for (var i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }

    private static string ColumnDefinition(DocumentDescription document, FieldDescription field, bool tableExisted)
    {
        var sql = new StringBuilder($"{Quote(field.Name)} text");

        // A column added to a table that already has rows can only be NOT NULL if it has a default.
        if (field.Required && (!tableExisted || field.Default is not null)) sql.Append(" NOT NULL");
        if (field.Default is not null) sql.Append($" DEFAULT {Literal(field.Default)}");

        // ponytail: the check is written once, with the column. Changing the options of a declared
        // field later needs a versioned migration (base-de-datos.md, section 11).
        if (field.Options is { } options)
            sql.Append($" CONSTRAINT {Quote($"ck_{document.Name}__{field.Name}")} "
                + $"CHECK ({Quote(field.Name)} IN ({string.Join(", ", options.Select(o => Literal(o.Value)))}))");

        return sql.ToString();
    }

    private static string Table(DocumentDescription document) => $"{Quote(document.Package)}.{Quote(document.Name)}";

    private static string Quote(string identifier)
    {
        if (!DocumentModelBuilder.IsIdentifier(identifier))
            throw new ArgumentException($"'{identifier}' is not a valid database name.");
        return $"\"{identifier}\"";
    }

    // DDL cannot take parameters. These values come from package declarations, never from a request.
    private static string Literal(string value) => $"'{value.Replace("'", "''")}'";
}
