using System.Text;
using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;

namespace ApiSmith.Generation.Emitters;

public static class DtoEmitter
{
    public static EmittedFile Emit(ApiSmithConfig config, IArchitectureLayout layout, NamedTable table)
    {
        if (config.ApiVersion == ApiVersion.V2)
        {
            return EmitV2SingleDto(config, layout, table);
        }
        return EmitV1Aggregate(config, layout, table);
    }

    private static EmittedFile EmitV2SingleDto(ApiSmithConfig config, IArchitectureLayout layout, NamedTable table)
    {
        var sb = new StringBuilder();

        var hasEnumColumn = table.Columns.Any(c => c.EnumTypeName is not null);
        if (hasEnumColumn)
        {
            sb.AppendLine($"using {layout.SharedNamespace(config)}.Enums{SegmentNamespace(config, table.Schema)};");
            sb.AppendLine();
        }

        sb.AppendLine($"namespace {layout.DtoNamespace(config, table.Schema)};");
        sb.AppendLine();

        EmitClass(sb, $"{table.EntityName}Dto", table, includeIdentity: true, isV2: true);

        var fileName = $"{table.EntityName}Dto";
        return new EmittedFile(layout.DtoPath(config, table.Schema, fileName), sb.ToString());
    }

    private static EmittedFile EmitV1Aggregate(ApiSmithConfig config, IArchitectureLayout layout, NamedTable table)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"namespace {layout.DtoNamespace(config, table.Schema)};");
        sb.AppendLine();

        EmitClass(sb, $"{table.EntityName}Dto", table, includeIdentity: true, isV2: false);

        // Views: read-only, no Create/Update DTOs.
        if (!table.IsView)
        {
            sb.AppendLine();
            EmitClass(sb, $"Create{table.EntityName}Dto", table, includeIdentity: false, isV2: false);
            sb.AppendLine();
            EmitClass(sb, $"Update{table.EntityName}Dto", table, includeIdentity: false, isV2: false);
        }

        return new EmittedFile(layout.DtoPath(config, table.Schema, $"{table.EntityName}Dtos"), sb.ToString());
    }

    private static string SegmentNamespace(ApiSmithConfig config, string schema)
    {
        var emit = config.Schemas.Count > 1 ||
                   !string.Equals(schema, "dbo", System.StringComparison.OrdinalIgnoreCase);
        return emit ? "." + ApiSmith.Naming.SchemaSegment.ToPascal(schema) : string.Empty;
    }

    private static void EmitClass(StringBuilder sb, string className, NamedTable table, bool includeIdentity, bool isV2)
    {
        sb.AppendLine($"public sealed class {className}");
        sb.AppendLine("{");

        foreach (var c in table.Columns)
        {
            if (!includeIdentity && c.IsIdentity)
            {
                continue;
            }

            var useEnumType = isV2 && c.EnumTypeName is not null;

            if (useEnumType)
            {
                // Enum — no string.Empty initializer (enum defaults to first member).
                sb.AppendLine($"    public {c.EnumTypeName} {c.PropertyName} {{ get; set; }}");
            }
            else
            {
                var initializer = !c.IsNullable && c.ClrTypeName == "string"
                    ? " = string.Empty;"
                    : string.Empty;

                sb.AppendLine($"    public {c.ClrTypeWithNullability} {c.PropertyName} {{ get; set; }}{initializer}");
            }
        }

        sb.AppendLine("}");
    }
}
