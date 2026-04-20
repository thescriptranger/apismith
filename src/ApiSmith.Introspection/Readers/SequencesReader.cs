using ApiSmith.Core.Model;
using Microsoft.Data.SqlClient;

namespace ApiSmith.Introspection.Readers;

public sealed class SequencesReader
{
    public async Task<IReadOnlyList<Sequence>> ReadAsync(
        SqlConnection conn,
        IReadOnlyCollection<string>? schemaFilter,
        CancellationToken ct)
    {
        const string baseSql = """
            SELECT SCHEMA_NAME(s.schema_id)               AS SchemaName,
                   s.name                                 AS SequenceName,
                   TYPE_NAME(s.system_type_id)            AS TypeName,
                   CAST(s.start_value AS bigint)          AS StartValue,
                   CAST(s.increment AS bigint)            AS Increment,
                   CAST(s.minimum_value AS bigint)        AS MinValue,
                   CAST(s.maximum_value AS bigint)        AS MaxValue,
                   s.is_cycling                           AS Cycle
            FROM   sys.sequences s
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
                + $"\nWHERE SCHEMA_NAME(s.schema_id) IN ({string.Join(", ", paramNames)})"
                + "\nORDER BY SchemaName, SequenceName;";
        }
        else
        {
            cmd.CommandText = baseSql + "\nORDER BY SchemaName, SequenceName;";
        }

        var rows = new List<Row>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            rows.Add(new Row(
                SchemaName: reader.GetString(0),
                SequenceName: reader.GetString(1),
                TypeName: reader.GetString(2),
                StartValue: reader.GetInt64(3),
                Increment: reader.GetInt64(4),
                MinValue: reader.GetInt64(5),
                MaxValue: reader.GetInt64(6),
                Cycle: reader.GetBoolean(7)));
        }

        return MapRows(rows);
    }

    internal static IReadOnlyList<Sequence> MapRows(IEnumerable<Row> rows)
    {
        return rows
            .OrderBy(r => r.SchemaName, System.StringComparer.Ordinal)
            .ThenBy(r => r.SequenceName, System.StringComparer.Ordinal)
            .Select(r => new Sequence(
                Schema: r.SchemaName,
                Name: r.SequenceName,
                TypeName: r.TypeName,
                StartValue: r.StartValue,
                Increment: r.Increment,
                MinValue: r.MinValue,
                MaxValue: r.MaxValue,
                Cycle: r.Cycle))
            .ToList();
    }

    internal sealed record Row(
        string SchemaName,
        string SequenceName,
        string TypeName,
        long StartValue,
        long Increment,
        long MinValue,
        long MaxValue,
        bool Cycle);
}
