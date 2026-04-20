using ApiSmith.Core.Model;
using Microsoft.Data.SqlClient;

namespace ApiSmith.Introspection.Readers;

internal sealed class FunctionsReader
{
    public async Task<IReadOnlyList<DbFunction>> ReadAsync(
        SqlConnection conn,
        IReadOnlyCollection<string>? schemaFilter,
        CancellationToken ct)
    {
        const string sql = """
            SELECT
                s.name, o.name, o.type,
                par.name, par.parameter_id,
                TYPE_NAME(par.user_type_id) AS sql_type,
                par.is_output, par.is_nullable, par.max_length, par.precision, par.scale
            FROM sys.objects o
            INNER JOIN sys.schemas s ON s.schema_id = o.schema_id
            LEFT JOIN  sys.parameters par ON par.object_id = o.object_id
            WHERE o.type IN ('FN', 'IF', 'TF')
            ORDER BY s.name, o.name, par.parameter_id;
            """;

        var grouped = new Dictionary<(string Schema, string Name),
                                     (FunctionKind Kind, List<SprocParameter> Params, string? ReturnType)>();

        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var schemaName = reader.GetString(0);
            if (!SchemaFilter.Accepts(schemaFilter, schemaName))
            {
                continue;
            }

            var name = reader.GetString(1);
            var kind = reader.GetString(2).Trim() switch
            {
                "FN" => FunctionKind.Scalar,
                "IF" => FunctionKind.InlineTableValued,
                "TF" => FunctionKind.MultiStatementTableValued,
                _ => FunctionKind.Scalar,
            };

            var key = (schemaName, name);
            if (!grouped.TryGetValue(key, out var entry))
            {
                entry = (kind, new List<SprocParameter>(), null);
                grouped[key] = entry;
            }

            if (reader.IsDBNull(3))
            {
                continue;
            }

            var paramName = reader.GetString(3);
            var ordinal = reader.GetInt32(4);
            var sqlType = reader.IsDBNull(5) ? "sql_variant" : reader.GetString(5);

            if (paramName.Length == 0)
            {
                // Scalar return type arrives as an unnamed parameter row.
                entry = entry with { ReturnType = sqlType };
                grouped[key] = entry;
                continue;
            }

            entry.Params.Add(new SprocParameter(
                Name: paramName.TrimStart('@'),
                OrdinalPosition: ordinal,
                SqlType: sqlType,
                IsNullable: reader.GetBoolean(7),
                Direction: reader.GetBoolean(6) ? ParameterDirection.InOut : ParameterDirection.In,
                MaxLength: reader.IsDBNull(8) ? null : (int)reader.GetInt16(8),
                Precision: reader.IsDBNull(9) ? null : (int)reader.GetByte(9),
                Scale: reader.IsDBNull(10) ? null : (int)reader.GetByte(10)));
        }

        return grouped
            .OrderBy(kvp => kvp.Key.Schema, System.StringComparer.Ordinal)
            .ThenBy(kvp => kvp.Key.Name, System.StringComparer.Ordinal)
            .Select(kvp => DbFunction.Create(
                kvp.Key.Schema,
                kvp.Key.Name,
                kvp.Value.Kind,
                kvp.Value.Params,
                returnSqlType: kvp.Value.ReturnType))
            .ToList();
    }
}
