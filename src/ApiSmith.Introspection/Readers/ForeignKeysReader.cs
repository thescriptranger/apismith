using ApiSmith.Core.Model;
using Microsoft.Data.SqlClient;

namespace ApiSmith.Introspection.Readers;

internal sealed class ForeignKeysReader
{
    public async Task<IReadOnlyList<ForeignKey>> ReadAsync(
        SqlConnection conn,
        IReadOnlyCollection<string>? schemaFilter,
        CancellationToken ct)
    {
        const string sql = """
            SELECT
                fk.name                                        AS fk_name,
                sch_from.name                                  AS from_schema,
                t_from.name                                    AS from_table,
                c_from.name                                    AS from_column,
                sch_to.name                                    AS to_schema,
                t_to.name                                      AS to_table,
                c_to.name                                      AS to_column,
                fkc.constraint_column_id                       AS ordinal,
                fk.delete_referential_action                   AS on_delete,
                fk.update_referential_action                   AS on_update
            FROM sys.foreign_keys fk
            INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
            INNER JOIN sys.tables   t_from   ON t_from.object_id   = fk.parent_object_id
            INNER JOIN sys.schemas  sch_from ON sch_from.schema_id = t_from.schema_id
            INNER JOIN sys.columns  c_from   ON c_from.object_id   = fkc.parent_object_id
                                             AND c_from.column_id   = fkc.parent_column_id
            INNER JOIN sys.tables   t_to     ON t_to.object_id     = fk.referenced_object_id
            INNER JOIN sys.schemas  sch_to   ON sch_to.schema_id   = t_to.schema_id
            INNER JOIN sys.columns  c_to     ON c_to.object_id     = fkc.referenced_object_id
                                             AND c_to.column_id     = fkc.referenced_column_id
            ORDER BY sch_from.name, t_from.name, fk.name, fkc.constraint_column_id;
            """;

        var grouped = new Dictionary<string, Row>();
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var fkName     = reader.GetString(0);
            var fromSchema = reader.GetString(1);
            var toSchema   = reader.GetString(4);

            if (!SchemaFilter.Accepts(schemaFilter, fromSchema) || !SchemaFilter.Accepts(schemaFilter, toSchema))
            {
                continue;
            }

            if (!grouped.TryGetValue(fkName, out var row))
            {
                row = new Row(
                    fkName,
                    fromSchema, reader.GetString(2),
                    toSchema,   reader.GetString(5),
                    reader.GetByte(8),
                    reader.GetByte(9));
                grouped[fkName] = row;
            }

            row.FromColumns.Add(reader.GetString(3));
            row.ToColumns.Add(reader.GetString(6));
        }

        return grouped.Values
            .OrderBy(r => r.FromSchema, System.StringComparer.Ordinal)
            .ThenBy(r => r.FromTable, System.StringComparer.Ordinal)
            .ThenBy(r => r.FkName, System.StringComparer.Ordinal)
            .Select(r => ForeignKey.Create(
                r.FkName,
                r.FromSchema, r.FromTable, r.FromColumns,
                r.ToSchema,   r.ToTable,   r.ToColumns,
                MapAction(r.OnDelete),
                MapAction(r.OnUpdate)))
            .ToList();
    }

    private static ReferentialAction MapAction(byte code) => code switch
    {
        0 => ReferentialAction.NoAction,
        1 => ReferentialAction.Cascade,
        2 => ReferentialAction.SetNull,
        3 => ReferentialAction.SetDefault,
        _ => ReferentialAction.NoAction,
    };

    private sealed record Row(
        string FkName,
        string FromSchema,
        string FromTable,
        string ToSchema,
        string ToTable,
        byte OnDelete,
        byte OnUpdate)
    {
        public List<string> FromColumns { get; } = new();
        public List<string> ToColumns { get; } = new();
    }
}
