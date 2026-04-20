using System.Text;
using ApiSmith.Config;
using ApiSmith.Core.Model;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;
using ApiSmith.Introspection.TypeMapping;
using ApiSmith.Naming;

namespace ApiSmith.Generation.Emitters;

/// <summary>FR-23: emits <c>IDbFunctions</c> — scalar UDFs return a value, TVFs return a result-record list.</summary>
public static class DbFunctionsEmitter
{
    public static IEnumerable<EmittedFile> Emit(ApiSmithConfig config, IArchitectureLayout layout, SchemaGraph graph)
    {
        var functions = graph.Schemas.SelectMany(s => s.Functions).ToList();
        if (functions.Count == 0)
        {
            yield break;
        }

        var dataNs = layout.DataNamespace(config);
        var targetDir = config.DataAccess is DataAccessStyle.EfCore
            ? System.IO.Path.GetDirectoryName(layout.DbContextPath(config))!.Replace('\\', '/')
            : System.IO.Path.GetDirectoryName(layout.ConnectionFactoryPath(config))!.Replace('\\', '/');

        var sb = new StringBuilder();
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        if (config.DataAccess is DataAccessStyle.Dapper)
        {
            sb.AppendLine("using Dapper;");
        }
        sb.AppendLine();
        sb.AppendLine($"namespace {dataNs};");
        sb.AppendLine();
        sb.AppendLine("public interface IDbFunctions");
        sb.AppendLine("{");
        foreach (var fn in functions)
        {
            sb.AppendLine($"    {SignatureFor(fn)};");
        }
        sb.AppendLine("}");
        sb.AppendLine();

        foreach (var fn in functions.Where(f => f.Kind != FunctionKind.Scalar))
        {
            sb.AppendLine($"public sealed record {Casing.ToPascal(fn.Name)}Result();");
        }

        var implClass = $"{config.ProjectName}DbFunctions";
        sb.AppendLine();
        sb.AppendLine($"public sealed class {implClass} : IDbFunctions");
        sb.AppendLine("{");
        foreach (var fn in functions)
        {
            sb.AppendLine($"    public {SignatureFor(fn)}");
            sb.AppendLine("    {");
            sb.AppendLine("        // TODO: finalize — the exact invocation depends on whether the host uses Dapper or EF Core.");
            sb.AppendLine("        throw new System.NotImplementedException();");
            sb.AppendLine("    }");
        }
        sb.AppendLine("}");

        yield return new EmittedFile($"{targetDir}/DbFunctions.cs", sb.ToString());
    }

    private static string SignatureFor(DbFunction fn)
    {
        var name = Casing.ToPascal(fn.Name);
        var paramList = string.Join(", ", fn.Parameters.Select(p =>
        {
            var clr = SqlTypeMapper.ToClrTypeName(p.SqlType);
            var nullable = p.IsNullable ? clr + "?" : clr;
            return $"{nullable} {Identifiers.EscapeKeyword(Casing.ToCamel(p.Name))}";
        }));

        if (fn.Kind == FunctionKind.Scalar)
        {
            var returnType = fn.ReturnSqlType is null ? "object?" : SqlTypeMapper.ToClrTypeName(fn.ReturnSqlType);
            return $"Task<{returnType}> {name}Async({(paramList.Length == 0 ? "" : paramList + ", ")}CancellationToken ct = default)";
        }

        var resultType = $"{name}Result";
        return $"Task<IReadOnlyList<{resultType}>> {name}Async({(paramList.Length == 0 ? "" : paramList + ", ")}CancellationToken ct = default)";
    }
}
