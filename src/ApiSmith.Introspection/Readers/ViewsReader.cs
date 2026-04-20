using ApiSmith.Core.Model;
using Microsoft.Data.SqlClient;

namespace ApiSmith.Introspection.Readers;

internal sealed class ViewsReader
{
    public async Task<IReadOnlyList<View>> ReadAsync(
        SqlConnection conn,
        IReadOnlyCollection<string>? schemaFilter,
        CancellationToken ct)
    {
        var sql = $"""
            SELECT
                c.TABLE_SCHEMA, c.TABLE_NAME, c.COLUMN_NAME, c.ORDINAL_POSITION,
                c.DATA_TYPE, c.IS_NULLABLE,
                c.CHARACTER_MAXIMUM_LENGTH, c.NUMERIC_PRECISION, c.NUMERIC_SCALE
            FROM INFORMATION_SCHEMA.COLUMNS c
            INNER JOIN INFORMATION_SCHEMA.VIEWS v
              ON v.TABLE_SCHEMA = c.TABLE_SCHEMA AND v.TABLE_NAME = c.TABLE_NAME
            WHERE {SystemSchemas.FilterClause("c.TABLE_SCHEMA")}
            ORDER BY c.TABLE_SCHEMA, c.TABLE_NAME, c.ORDINAL_POSITION;
            """;

        var views = new Dictionary<(string, string), List<Column>>();

        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var schemaName = reader.GetString(0);
            if (!SchemaFilter.Accepts(schemaFilter, schemaName))
            {
                continue;
            }

            var viewName = reader.GetString(1);
            var key = (schemaName, viewName);
            if (!views.TryGetValue(key, out var cols))
            {
                cols = new List<Column>();
                views[key] = cols;
            }

            cols.Add(new Column(
                Name: reader.GetString(2),
                OrdinalPosition: reader.GetInt32(3),
                SqlType: reader.GetString(4),
                IsNullable: string.Equals(reader.GetString(5), "YES", System.StringComparison.OrdinalIgnoreCase),
                IsIdentity: false,
                IsComputed: false,
                MaxLength: reader.IsDBNull(6) ? null : reader.GetInt32(6),
                Precision: reader.IsDBNull(7) ? null : Convert.ToInt32(reader.GetValue(7)),
                Scale: reader.IsDBNull(8) ? null : Convert.ToInt32(reader.GetValue(8)),
                DefaultValue: null));
        }

        return views
            .OrderBy(kvp => kvp.Key.Item1, System.StringComparer.Ordinal)
            .ThenBy(kvp => kvp.Key.Item2, System.StringComparer.Ordinal)
            .Select(kvp => View.Create(kvp.Key.Item1, kvp.Key.Item2, kvp.Value))
            .ToList();
    }
}
