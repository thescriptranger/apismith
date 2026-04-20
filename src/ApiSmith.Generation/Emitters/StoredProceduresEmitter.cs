using System.Text;
using ApiSmith.Config;
using ApiSmith.Core.Model;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;
using ApiSmith.Introspection.TypeMapping;
using ApiSmith.Naming;

namespace ApiSmith.Generation.Emitters;

/// <summary>FR-21: emits <c>IStoredProcedures</c> surfacing each sproc as a typed method; indeterminate result shapes get a TODO stub and a warning.</summary>
public static class StoredProceduresEmitter
{
    public static IEnumerable<EmittedFile> Emit(ApiSmithConfig config, IArchitectureLayout layout, SchemaGraph graph, IScaffoldLog log)
    {
        var sprocs = graph.Schemas.SelectMany(s => s.Procedures).ToList();
        if (sprocs.Count == 0)
        {
            yield break;
        }

        var dataNs = layout.DataNamespace(config);
        var sprocs_folder = $"{System.IO.Path.GetDirectoryName(layout.ConnectionFactoryPath(config))!.Replace('\\', '/')}";
        var targetDir = config.DataAccess is DataAccessStyle.EfCore
            ? System.IO.Path.GetDirectoryName(layout.DbContextPath(config))!.Replace('\\', '/')
            : sprocs_folder;

        var sb = new StringBuilder();
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        if (config.DataAccess is DataAccessStyle.Dapper)
        {
            sb.AppendLine("using Dapper;");
        }
        else
        {
            sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        }
        sb.AppendLine();
        sb.AppendLine($"namespace {dataNs};");
        sb.AppendLine();

        sb.AppendLine("public interface IStoredProcedures");
        sb.AppendLine("{");
        foreach (var sp in sprocs)
        {
            var methodName = Casing.ToPascal(sp.Name);
            var resultType = ResultTypeName(sp);
            var paramList = BuildParamList(sp);
            sb.AppendLine($"    Task<IReadOnlyList<{resultType}>> {methodName}Async({paramList}CancellationToken ct = default);");
        }
        sb.AppendLine("}");
        sb.AppendLine();

        foreach (var sp in sprocs)
        {
            if (sp.ResultIsIndeterminate)
            {
                log.Warn($"Sproc [{sp.Schema}].[{sp.Name}] has an indeterminate result set. Emitted a stub; finish it manually. Reason: {sp.IndeterminateReason}");
                sb.AppendLine($"/// <summary>TODO: fill in real columns — {sp.IndeterminateReason}</summary>");
                sb.AppendLine($"public sealed record {Casing.ToPascal(sp.Name)}Result();");
            }
            else if (sp.ResultColumns.Length > 0)
            {
                var columns = string.Join(", ", sp.ResultColumns.Select(c =>
                {
                    var clr = SqlTypeMapper.ToClrTypeName(c.SqlType);
                    var nullable = c.IsNullable ? clr + "?" : clr;
                    return $"{nullable} {Casing.ToPascal(c.Name)}";
                }));
                sb.AppendLine($"public sealed record {Casing.ToPascal(sp.Name)}Result({columns});");
            }
            else
            {
                sb.AppendLine($"public sealed record {Casing.ToPascal(sp.Name)}Result();");
            }
        }
        sb.AppendLine();

        var implClass = $"{config.ProjectName}StoredProcedures";
        sb.AppendLine($"public sealed class {implClass} : IStoredProcedures");
        sb.AppendLine("{");

        if (config.DataAccess is DataAccessStyle.Dapper)
        {
            sb.AppendLine("    private readonly IDbConnectionFactory _connections;");
            sb.AppendLine();
            sb.AppendLine($"    public {implClass}(IDbConnectionFactory connections)");
            sb.AppendLine("    {");
            sb.AppendLine("        _connections = connections;");
            sb.AppendLine("    }");
        }
        else
        {
            var dbCtx = $"{config.ProjectName}DbContext";
            sb.AppendLine($"    private readonly {dbCtx} _db;");
            sb.AppendLine();
            sb.AppendLine($"    public {implClass}({dbCtx} db)");
            sb.AppendLine("    {");
            sb.AppendLine("        _db = db;");
            sb.AppendLine("    }");
        }

        foreach (var sp in sprocs)
        {
            var methodName = Casing.ToPascal(sp.Name);
            var resultType = ResultTypeName(sp);
            var paramList = BuildParamList(sp);
            sb.AppendLine();
            sb.AppendLine($"    public async Task<IReadOnlyList<{resultType}>> {methodName}Async({paramList}CancellationToken ct = default)");
            sb.AppendLine("    {");

            if (config.DataAccess is DataAccessStyle.Dapper)
            {
                sb.AppendLine($"        var sql = \"[{sp.Schema}].[{sp.Name}]\";");
                if (sp.Parameters.Length > 0)
                {
                    sb.AppendLine("        var parameters = new DynamicParameters();");
                    foreach (var p in sp.Parameters)
                    {
                        var pn = Casing.ToCamel(p.Name);
                        sb.AppendLine($"        parameters.Add(\"@{p.Name}\", {pn});");
                    }
                }

                sb.AppendLine("        using var conn = await _connections.OpenAsync(ct).ConfigureAwait(false);");
                if (sp.Parameters.Length > 0)
                {
                    sb.AppendLine($"        var rows = await conn.QueryAsync<{resultType}>(sql, parameters, commandType: System.Data.CommandType.StoredProcedure).ConfigureAwait(false);");
                }
                else
                {
                    sb.AppendLine($"        var rows = await conn.QueryAsync<{resultType}>(sql, commandType: System.Data.CommandType.StoredProcedure).ConfigureAwait(false);");
                }
                sb.AppendLine("        return rows.AsList();");
            }
            else
            {
                // EF Core placeholder — developer finalizes via SqlQueryRaw or keyless projection (FR-21).
                sb.AppendLine($"        await Task.Yield();");
                sb.AppendLine($"        return System.Array.Empty<{resultType}>();");
            }

            sb.AppendLine("    }");
        }

        sb.AppendLine("}");

        yield return new EmittedFile($"{targetDir}/StoredProcedures.cs", sb.ToString());
    }

    private static string ResultTypeName(StoredProcedure sp) => $"{Casing.ToPascal(sp.Name)}Result";

    private static string BuildParamList(StoredProcedure sp)
    {
        if (sp.Parameters.Length == 0)
        {
            return string.Empty;
        }

        var parts = sp.Parameters
            .Select(p =>
            {
                var clr = SqlTypeMapper.ToClrTypeName(p.SqlType);
                var nullable = p.IsNullable ? clr + "?" : clr;
                return $"{nullable} {Identifiers.EscapeKeyword(Casing.ToCamel(p.Name))}";
            });

        return string.Join(", ", parts) + ", ";
    }
}
