using ApiSmith.Core.Model;
using Microsoft.Data.SqlClient;

namespace ApiSmith.Introspection.Readers;

/// <summary>Reads UNIQUE constraints (non-PK). Keys are ordinal; constraints sorted by name, columns by <c>key_ordinal</c>.</summary>
public sealed class UniqueConstraintsReader
{
    public async Task<IReadOnlyDictionary<(string Schema, string Table), IReadOnlyList<UniqueConstraint>>> ReadAsync(
        SqlConnection conn,
        IReadOnlyCollection<string>? schemaFilter,
        CancellationToken ct)
    {
        const string baseSql = """
            SELECT SCHEMA_NAME(t.schema_id) AS SchemaName,
                   t.name                   AS TableName,
                   kc.name                  AS ConstraintName,
                   c.name                   AS ColumnName,
                   ic.key_ordinal           AS KeyOrdinal
            FROM   sys.key_constraints kc
            JOIN   sys.tables          t  ON t.object_id = kc.parent_object_id
            JOIN   sys.index_columns   ic ON ic.object_id = kc.parent_object_id
                                         AND ic.index_id  = kc.unique_index_id
            JOIN   sys.columns         c  ON c.object_id  = ic.object_id
                                         AND c.column_id  = ic.column_id
            WHERE  kc.type = 'UQ'
            """;

        await using var cmd = new SqlCommand { Connection = conn };

        if (schemaFilter is { Count: > 0 })
        {
            var paramNames = new List<string>(schemaFilter.Count);
            var i = 0;
            foreach (var schemaName in schemaFilter)
            {
                var pname = $"@s{i++}";
                paramNames.Add(pname);
                cmd.Parameters.AddWithValue(pname, schemaName);
            }

            cmd.CommandText = baseSql
                + $"\nAND SCHEMA_NAME(t.schema_id) IN ({string.Join(", ", paramNames)})"
                + "\nORDER BY SchemaName, TableName, ConstraintName, KeyOrdinal;";
        }
        else
        {
            cmd.CommandText = baseSql + "\nORDER BY SchemaName, TableName, ConstraintName, KeyOrdinal;";
        }

        var rows = new List<(string, string, string, string, int)>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            rows.Add((
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                Convert.ToByte(reader.GetValue(4))));
        }

        return GroupRows(rows);
    }

    internal static IReadOnlyDictionary<(string Schema, string Table), IReadOnlyList<UniqueConstraint>> GroupRows(
        IEnumerable<(string, string, string, string, int)> rows)
    {
        var byTable = new Dictionary<(string, string), Dictionary<string, List<(int KeyOrdinal, string ColumnName)>>>();

        foreach (var (schemaName, tableName, constraintName, columnName, keyOrdinal) in rows)
        {
            var tableKey = (schemaName, tableName);
            if (!byTable.TryGetValue(tableKey, out var constraints))
            {
                constraints = new Dictionary<string, List<(int, string)>>(System.StringComparer.Ordinal);
                byTable[tableKey] = constraints;
            }

            if (!constraints.TryGetValue(constraintName, out var cols))
            {
                cols = new List<(int, string)>();
                constraints[constraintName] = cols;
            }

            cols.Add((keyOrdinal, columnName));
        }

        var result = new Dictionary<(string Schema, string Table), IReadOnlyList<UniqueConstraint>>();
        foreach (var (tableKey, constraints) in byTable)
        {
            var ordered = constraints
                .OrderBy(kvp => kvp.Key, System.StringComparer.Ordinal)
                .Select(kvp => UniqueConstraint.Create(
                    kvp.Key,
                    kvp.Value
                        .OrderBy(c => c.KeyOrdinal)
                        .Select(c => c.ColumnName)))
                .ToList();

            result[tableKey] = ordered;
        }

        return result;
    }
}
