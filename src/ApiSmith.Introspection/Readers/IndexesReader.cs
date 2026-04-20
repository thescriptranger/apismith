using ApiSmith.Core.Model;
using Microsoft.Data.SqlClient;

namespace ApiSmith.Introspection.Readers;

/// <summary>Reads secondary indexes. PK indexes come from <see cref="TablesReader"/>, UQ indexes from <see cref="UniqueConstraintsReader"/>. INCLUDE columns and heaps skipped.</summary>
public sealed class IndexesReader
{
    public async Task<IReadOnlyDictionary<(string Schema, string Table), IReadOnlyList<TableIndex>>> ReadAsync(
        SqlConnection conn,
        IReadOnlyCollection<string>? schemaFilter,
        CancellationToken ct)
    {
        const string baseSql = """
            SELECT SCHEMA_NAME(t.schema_id) AS SchemaName,
                   t.name                   AS TableName,
                   i.name                   AS IndexName,
                   i.is_unique              AS IsUnique,
                   c.name                   AS ColumnName,
                   ic.key_ordinal           AS KeyOrdinal
            FROM   sys.indexes       i
            JOIN   sys.tables        t  ON t.object_id = i.object_id
            JOIN   sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN   sys.columns       c  ON c.object_id  = ic.object_id AND c.column_id = ic.column_id
            WHERE  i.is_primary_key       = 0
               AND i.is_unique_constraint = 0
               AND i.type                <> 0
               AND ic.is_included_column  = 0
            """;

        await using var cmd = new SqlCommand { Connection = conn };

        if (schemaFilter is { Count: > 0 })
        {
            var paramNames = new List<string>(schemaFilter.Count);
            var i = 0;
            foreach (var schemaName in schemaFilter)
            {
                var pname = $"@schema{i++}";
                paramNames.Add(pname);
                cmd.Parameters.AddWithValue(pname, schemaName);
            }

            cmd.CommandText = baseSql
                + $"\n   AND SCHEMA_NAME(t.schema_id) IN ({string.Join(", ", paramNames)})"
                + "\nORDER BY SchemaName, TableName, IndexName, KeyOrdinal;";
        }
        else
        {
            cmd.CommandText = baseSql + "\nORDER BY SchemaName, TableName, IndexName, KeyOrdinal;";
        }

        var rows = new List<Row>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var schemaName = reader.GetString(0);
            if (!SchemaFilter.Accepts(schemaFilter, schemaName))
            {
                continue;
            }

            rows.Add(new Row(
                SchemaName: schemaName,
                TableName: reader.GetString(1),
                IndexName: reader.GetString(2),
                IsUnique: reader.GetBoolean(3),
                ColumnName: reader.GetString(4),
                KeyOrdinal: Convert.ToByte(reader.GetValue(5))));
        }

        return GroupRows(rows);
    }

    internal static IReadOnlyDictionary<(string Schema, string Table), IReadOnlyList<TableIndex>> GroupRows(
        IEnumerable<Row> rows)
    {
        var byTable = new Dictionary<(string Schema, string Table), Dictionary<string, IndexBuilder>>();

        foreach (var row in rows)
        {
            var tableKey = (row.SchemaName, row.TableName);
            if (!byTable.TryGetValue(tableKey, out var indexMap))
            {
                indexMap = new Dictionary<string, IndexBuilder>(System.StringComparer.Ordinal);
                byTable[tableKey] = indexMap;
            }

            if (!indexMap.TryGetValue(row.IndexName, out var builder))
            {
                builder = new IndexBuilder(row.IndexName, row.IsUnique);
                indexMap[row.IndexName] = builder;
            }

            builder.Columns.Add((row.KeyOrdinal, row.ColumnName));
        }

        var result = new Dictionary<(string Schema, string Table), IReadOnlyList<TableIndex>>();
        foreach (var (tableKey, indexMap) in byTable)
        {
            var indexes = indexMap.Values
                .OrderBy(b => b.Name, System.StringComparer.Ordinal)
                .Select(b => TableIndex.Create(
                    b.Name,
                    b.IsUnique,
                    b.Columns
                        .OrderBy(c => c.KeyOrdinal)
                        .Select(c => c.ColumnName)))
                .ToList();

            result[tableKey] = indexes;
        }

        return result;
    }

    internal sealed record Row(
        string SchemaName,
        string TableName,
        string IndexName,
        bool IsUnique,
        string ColumnName,
        byte KeyOrdinal);

    private sealed class IndexBuilder
    {
        public IndexBuilder(string name, bool isUnique)
        {
            Name = name;
            IsUnique = isUnique;
        }

        public string Name { get; }
        public bool IsUnique { get; }
        public List<(byte KeyOrdinal, string ColumnName)> Columns { get; } = new();
    }
}
