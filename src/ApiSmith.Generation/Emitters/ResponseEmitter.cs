using System.Text;
using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;

namespace ApiSmith.Generation.Emitters;

public static class ResponseEmitter
{
    public static IEnumerable<EmittedFile> Emit(ApiSmithConfig config, IArchitectureLayout layout, NamedSchemaModel named)
    {
        if (config.ApiVersion != ApiVersion.V2) yield break;

        foreach (var table in named.Tables)
        {
            var fileName = $"{table.EntityName}Response";
            var path = layout.ResponsePath(config, table.Schema, fileName);
            var ns = layout.ResponseNamespace(config, table.Schema);

            var sb = new StringBuilder();
            if (HasAnyEnumColumn(table))
            {
                sb.AppendLine($"using {layout.SharedNamespace(config)}.Enums{SegmentNamespace(config, table.Schema)};");
                sb.AppendLine();
            }
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
            sb.AppendLine($"public sealed class {table.EntityName}Response");
            sb.AppendLine("{");
            foreach (var c in table.Columns)
            {
                if (c.EnumTypeName is not null)
                {
                    sb.AppendLine($"    public {c.EnumTypeName} {c.PropertyName} {{ get; set; }}");
                }
                else
                {
                    var initializer = !c.IsNullable && c.ClrTypeName == "string" ? " = string.Empty;" : string.Empty;
                    sb.AppendLine($"    public {c.ClrTypeWithNullability} {c.PropertyName} {{ get; set; }}{initializer}");
                }
            }
            sb.AppendLine("}");

            yield return new EmittedFile(path, sb.ToString());
        }
    }

    private static string SegmentNamespace(ApiSmithConfig config, string schema)
    {
        var emit = config.Schemas.Count > 1 ||
                   !string.Equals(schema, "dbo", System.StringComparison.OrdinalIgnoreCase);
        return emit ? "." + ApiSmith.Naming.SchemaSegment.ToPascal(schema) : string.Empty;
    }

    private static bool HasAnyEnumColumn(NamedTable table)
    {
        foreach (var c in table.Columns)
        {
            if (c.EnumTypeName is not null) return true;
        }
        return false;
    }
}
