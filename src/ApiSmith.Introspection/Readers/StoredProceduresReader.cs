using ApiSmith.Core.Model;
using Microsoft.Data.SqlClient;

namespace ApiSmith.Introspection.Readers;

/// <summary>Reads sprocs + params. Result shape comes from <c>sp_describe_first_result_set</c> best-effort.</summary>
internal sealed class StoredProceduresReader
{
    public async Task<IReadOnlyList<StoredProcedure>> ReadAsync(
        SqlConnection conn,
        IReadOnlyCollection<string>? schemaFilter,
        CancellationToken ct)
    {
        var sprocs = await ReadSprocsAndParamsAsync(conn, schemaFilter, ct).ConfigureAwait(false);

        var result = new List<StoredProcedure>();
        foreach (var (schemaName, sprocName, parameters) in sprocs)
        {
            var (columns, indeterminate, reason) = await DescribeFirstResultSetAsync(
                conn, schemaName, sprocName, ct).ConfigureAwait(false);

            result.Add(StoredProcedure.Create(
                schemaName,
                sprocName,
                parameters,
                resultColumns: columns,
                resultIsIndeterminate: indeterminate,
                indeterminateReason: reason));
        }

        return result;
    }

    private static async Task<List<(string Schema, string Name, List<SprocParameter> Params)>> ReadSprocsAndParamsAsync(
        SqlConnection conn,
        IReadOnlyCollection<string>? schemaFilter,
        CancellationToken ct)
    {
        const string sql = """
            SELECT
                s.name                              AS schema_name,
                p.name                              AS proc_name,
                par.name                            AS param_name,
                par.parameter_id                    AS ordinal,
                TYPE_NAME(par.user_type_id)         AS sql_type,
                par.is_output                       AS is_output,
                par.is_nullable                     AS is_nullable,
                par.max_length                      AS max_length,
                par.precision                       AS num_precision,
                par.scale                           AS num_scale
            FROM sys.procedures p
            INNER JOIN sys.schemas s ON s.schema_id = p.schema_id
            LEFT JOIN  sys.parameters par ON par.object_id = p.object_id
            ORDER BY s.name, p.name, par.parameter_id;
            """;

        var grouped = new Dictionary<(string, string), List<SprocParameter>>();
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var schemaName = reader.GetString(0);
            if (!SchemaFilter.Accepts(schemaFilter, schemaName))
            {
                continue;
            }

            var sprocName = reader.GetString(1);
            var key = (schemaName, sprocName);
            if (!grouped.TryGetValue(key, out var list))
            {
                list = new List<SprocParameter>();
                grouped[key] = list;
            }

            if (reader.IsDBNull(2))
            {
                continue;
            }

            list.Add(new SprocParameter(
                Name: reader.GetString(2).TrimStart('@'),
                OrdinalPosition: reader.GetInt32(3),
                SqlType: reader.IsDBNull(4) ? "sql_variant" : reader.GetString(4),
                IsNullable: reader.GetBoolean(6),
                Direction: reader.GetBoolean(5) ? ParameterDirection.InOut : ParameterDirection.In,
                MaxLength: reader.IsDBNull(7) ? null : (int)reader.GetInt16(7),
                Precision: reader.IsDBNull(8) ? null : (int)reader.GetByte(8),
                Scale: reader.IsDBNull(9) ? null : (int)reader.GetByte(9)));
        }

        return grouped
            .OrderBy(kvp => kvp.Key.Item1, System.StringComparer.Ordinal)
            .ThenBy(kvp => kvp.Key.Item2, System.StringComparer.Ordinal)
            .Select(kvp => (kvp.Key.Item1, kvp.Key.Item2, kvp.Value))
            .ToList();
    }

    private static async Task<(List<ResultColumn>? Columns, bool Indeterminate, string? Reason)> DescribeFirstResultSetAsync(
        SqlConnection conn,
        string schemaName,
        string sprocName,
        CancellationToken ct)
    {
        const string sql = """
            SELECT name, column_ordinal, system_type_name, is_nullable
            FROM sys.dm_exec_describe_first_result_set(@proc, NULL, 0);
            """;

        var qualified = $"[{schemaName}].[{sprocName}]";
        try
        {
            var columns = new List<ResultColumn>();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@proc", qualified);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var name = reader.IsDBNull(0) ? $"Column{columns.Count + 1}" : reader.GetString(0);
                var ordinal = reader.IsDBNull(1) ? columns.Count + 1 : reader.GetInt32(1);
                var sqlType = reader.IsDBNull(2) ? "sql_variant" : reader.GetString(2);
                var nullable = !reader.IsDBNull(3) && reader.GetBoolean(3);

                columns.Add(new ResultColumn(name, ordinal, StripTypeParens(sqlType), nullable));
            }

            if (columns.Count == 0)
            {
                return (null, true, "sp_describe_first_result_set returned no rows (non-query or indeterminate).");
            }

            return (columns, false, null);
        }
        catch (SqlException ex)
        {
            // Fails on dynamic SQL, conditional branches, temp tables.
            return (null, true, $"sp_describe_first_result_set failed: {ex.Message}");
        }
    }

    private static string StripTypeParens(string typeName)
    {
        var paren = typeName.IndexOf('(', System.StringComparison.Ordinal);
        return paren >= 0 ? typeName[..paren] : typeName;
    }
}
