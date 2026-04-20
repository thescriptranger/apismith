using ApiSmith.Core.Model;
using Microsoft.Data.SqlClient;

namespace ApiSmith.Introspection.Readers;

internal sealed class TablesReader
{
    public async Task<IReadOnlyList<Table>> ReadAsync(
        SqlConnection conn,
        IReadOnlyCollection<string>? schemaFilter,
        CancellationToken ct)
    {
        var rawColumns      = await ReadColumnsAsync(conn, schemaFilter, ct).ConfigureAwait(false);
        var primaryKeys     = await ReadPrimaryKeysAsync(conn, schemaFilter, ct).ConfigureAwait(false);
        var identityColumns = await ReadIdentityAsync(conn, schemaFilter, ct).ConfigureAwait(false);
        var computedColumns = await ReadComputedAsync(conn, schemaFilter, ct).ConfigureAwait(false);

        var tables = new List<Table>();

        foreach (var group in rawColumns
                     .GroupBy(c => (c.Schema, c.TableName))
                     .OrderBy(g => g.Key.Schema, System.StringComparer.Ordinal)
                     .ThenBy(g => g.Key.TableName, System.StringComparer.Ordinal))
        {
            var (schemaName, tableName) = group.Key;
            var columns = group
                .OrderBy(r => r.OrdinalPosition)
                .Select(r => new Column(
                    Name: r.ColumnName,
                    OrdinalPosition: r.OrdinalPosition,
                    SqlType: r.DataType,
                    IsNullable: r.IsNullable,
                    IsIdentity: identityColumns.Contains((schemaName, tableName, r.ColumnName)),
                    IsComputed: computedColumns.Contains((schemaName, tableName, r.ColumnName)),
                    MaxLength: r.MaxLength,
                    Precision: r.Precision,
                    Scale: r.Scale,
                    DefaultValue: r.DefaultValue))
                .ToList();

            primaryKeys.TryGetValue((schemaName, tableName), out var pk);

            tables.Add(Table.Create(schemaName, tableName, columns, pk));
        }

        return tables;
    }

    private static async Task<List<ColumnRow>> ReadColumnsAsync(
        SqlConnection conn,
        IReadOnlyCollection<string>? schemaFilter,
        CancellationToken ct)
    {
        var sql = $"""
            SELECT
                c.TABLE_SCHEMA, c.TABLE_NAME, c.COLUMN_NAME, c.ORDINAL_POSITION,
                c.DATA_TYPE, c.IS_NULLABLE,
                c.CHARACTER_MAXIMUM_LENGTH, c.NUMERIC_PRECISION, c.NUMERIC_SCALE, c.COLUMN_DEFAULT
            FROM INFORMATION_SCHEMA.COLUMNS c
            INNER JOIN INFORMATION_SCHEMA.TABLES t
              ON t.TABLE_SCHEMA = c.TABLE_SCHEMA AND t.TABLE_NAME = c.TABLE_NAME
            WHERE t.TABLE_TYPE = 'BASE TABLE' AND {SystemSchemas.FilterClause("c.TABLE_SCHEMA")}
            ORDER BY c.TABLE_SCHEMA, c.TABLE_NAME, c.ORDINAL_POSITION;
            """;

        var rows = new List<ColumnRow>();
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var schemaName = reader.GetString(0);
            if (!SchemaFilter.Accepts(schemaFilter, schemaName))
            {
                continue;
            }

            rows.Add(new ColumnRow(
                Schema: schemaName,
                TableName: reader.GetString(1),
                ColumnName: reader.GetString(2),
                OrdinalPosition: reader.GetInt32(3),
                DataType: reader.GetString(4),
                IsNullable: string.Equals(reader.GetString(5), "YES", System.StringComparison.OrdinalIgnoreCase),
                MaxLength: reader.IsDBNull(6) ? null : reader.GetInt32(6),
                Precision: reader.IsDBNull(7) ? null : (int)reader.GetByte(7),
                Scale: reader.IsDBNull(8) ? null : (int)reader.GetByte(8),
                DefaultValue: reader.IsDBNull(9) ? null : reader.GetString(9)));
        }
        return rows;
    }

    private static async Task<Dictionary<(string, string), PrimaryKey>> ReadPrimaryKeysAsync(
        SqlConnection conn,
        IReadOnlyCollection<string>? schemaFilter,
        CancellationToken ct)
    {
        const string sql = """
            SELECT tc.TABLE_SCHEMA, tc.TABLE_NAME, tc.CONSTRAINT_NAME,
                   kcu.COLUMN_NAME, kcu.ORDINAL_POSITION
            FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
              ON tc.CONSTRAINT_SCHEMA = kcu.CONSTRAINT_SCHEMA
             AND tc.CONSTRAINT_NAME   = kcu.CONSTRAINT_NAME
            WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
            ORDER BY tc.TABLE_SCHEMA, tc.TABLE_NAME, kcu.ORDINAL_POSITION;
            """;

        var grouped = new Dictionary<(string, string), (string Name, List<string> Columns)>();
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var schemaName = reader.GetString(0);
            if (!SchemaFilter.Accepts(schemaFilter, schemaName))
            {
                continue;
            }

            var tableName = reader.GetString(1);
            var key = (schemaName, tableName);
            if (!grouped.TryGetValue(key, out var bucket))
            {
                bucket = (reader.GetString(2), new List<string>());
                grouped[key] = bucket;
            }
            bucket.Columns.Add(reader.GetString(3));
        }
        return grouped.ToDictionary(
            kvp => kvp.Key,
            kvp => PrimaryKey.Create(kvp.Value.Name, kvp.Value.Columns));
    }

    private static async Task<HashSet<(string, string, string)>> ReadIdentityAsync(
        SqlConnection conn,
        IReadOnlyCollection<string>? schemaFilter,
        CancellationToken ct)
    {
        const string sql = """
            SELECT s.name, t.name, c.name
            FROM sys.columns c
            INNER JOIN sys.tables t  ON t.object_id = c.object_id
            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE c.is_identity = 1;
            """;

        return await ReadTripleSetAsync(conn, sql, schemaFilter, ct).ConfigureAwait(false);
    }

    private static async Task<HashSet<(string, string, string)>> ReadComputedAsync(
        SqlConnection conn,
        IReadOnlyCollection<string>? schemaFilter,
        CancellationToken ct)
    {
        const string sql = """
            SELECT s.name, t.name, c.name
            FROM sys.columns c
            INNER JOIN sys.tables t  ON t.object_id = c.object_id
            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE c.is_computed = 1;
            """;

        return await ReadTripleSetAsync(conn, sql, schemaFilter, ct).ConfigureAwait(false);
    }

    private static async Task<HashSet<(string, string, string)>> ReadTripleSetAsync(
        SqlConnection conn,
        string sql,
        IReadOnlyCollection<string>? schemaFilter,
        CancellationToken ct)
    {
        var set = new HashSet<(string, string, string)>();
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var schemaName = reader.GetString(0);
            if (!SchemaFilter.Accepts(schemaFilter, schemaName))
            {
                continue;
            }
            set.Add((schemaName, reader.GetString(1), reader.GetString(2)));
        }
        return set;
    }

    private sealed record ColumnRow(
        string Schema,
        string TableName,
        string ColumnName,
        int OrdinalPosition,
        string DataType,
        bool IsNullable,
        int? MaxLength,
        int? Precision,
        int? Scale,
        string? DefaultValue);
}
