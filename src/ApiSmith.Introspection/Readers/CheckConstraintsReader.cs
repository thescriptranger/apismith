using ApiSmith.Core.Model;
using Microsoft.Data.SqlClient;

namespace ApiSmith.Introspection.Readers;

/// <summary>Reads CHECK constraints verbatim (e.g. <c>([Age] &gt;= (0))</c>); translation happens downstream.</summary>
public sealed class CheckConstraintsReader
{
    public async Task<IReadOnlyDictionary<(string Schema, string Table), IReadOnlyList<CheckConstraint>>> ReadAsync(
        SqlConnection conn,
        IReadOnlyCollection<string>? schemaFilter,
        CancellationToken ct)
    {
        const string baseSql = """
            SELECT SCHEMA_NAME(t.schema_id)  AS SchemaName,
                   t.name                    AS TableName,
                   cc.name                   AS ConstraintName,
                   cc.definition             AS Expression
            FROM   sys.check_constraints cc
            JOIN   sys.tables            t ON t.object_id = cc.parent_object_id
            """;

        var hasFilter = schemaFilter is { Count: > 0 };
        string sql;
        if (hasFilter)
        {
            var paramNames = new string[schemaFilter!.Count];
            for (var i = 0; i < paramNames.Length; i++)
            {
                paramNames[i] = "@schema" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            sql = baseSql
                  + "\nWHERE SCHEMA_NAME(t.schema_id) IN (" + string.Join(", ", paramNames) + ")"
                  + "\nORDER BY SchemaName, TableName, ConstraintName;";
        }
        else
        {
            sql = baseSql + "\nORDER BY SchemaName, TableName, ConstraintName;";
        }

        var rows = new List<(string Schema, string Table, string Name, string Expression)>();
        await using var cmd = new SqlCommand(sql, conn);
        if (hasFilter)
        {
            var i = 0;
            foreach (var schemaName in schemaFilter!)
            {
                cmd.Parameters.AddWithValue(
                    "@schema" + i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    schemaName);
                i++;
            }
        }

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            rows.Add((
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? string.Empty : reader.GetString(3)));
        }

        return GroupRows(rows);
    }

    public static IReadOnlyDictionary<(string Schema, string Table), IReadOnlyList<CheckConstraint>> GroupRows(
        IEnumerable<(string Schema, string Table, string Name, string Expression)> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var grouped = new Dictionary<(string Schema, string Table), List<CheckConstraint>>();
        foreach (var (schema, table, name, expression) in rows)
        {
            var key = (schema, table);
            if (!grouped.TryGetValue(key, out var list))
            {
                list = new List<CheckConstraint>();
                grouped[key] = list;
            }

            list.Add(new CheckConstraint(name, expression));
        }

        var result = new Dictionary<(string Schema, string Table), IReadOnlyList<CheckConstraint>>(grouped.Count);
        foreach (var kvp in grouped)
        {
            var sorted = kvp.Value
                .OrderBy(c => c.Name, System.StringComparer.Ordinal)
                .ToList();
            result[kvp.Key] = sorted;
        }

        return result;
    }
}
