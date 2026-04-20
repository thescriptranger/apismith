using System.Text;
using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;

namespace ApiSmith.Generation.Emitters;

public static class MapperEmitter
{
    public static EmittedFile Emit(ApiSmithConfig config, IArchitectureLayout layout, NamedTable table)
    {
        var sb = new StringBuilder();
        var mapperNs = layout.MapperNamespace(config, table.Schema);
        var dtoNs = layout.DtoNamespace(config, table.Schema);
        var entityNs = layout.EntityNamespace(config, table.Schema);

        var isV2 = config.ApiVersion == ApiVersion.V2;
        var hasEnumColumn = isV2 && table.Columns.Any(c => c.EnumTypeName is not null);
        var enumsNs = hasEnumColumn
            ? $"{layout.SharedNamespace(config)}.Enums{SegmentNamespace(config, table.Schema)}"
            : null;

        // VerticalSlice can collapse entity/DTO/mapper into one namespace (would trip CS0105); List+Contains keeps order stable for replay.
        var usings = new System.Collections.Generic.List<string>();
        if (!string.Equals(dtoNs, mapperNs, System.StringComparison.Ordinal) && !usings.Contains(dtoNs))
        {
            usings.Add(dtoNs);
        }
        if (!string.Equals(entityNs, mapperNs, System.StringComparison.Ordinal) && !usings.Contains(entityNs))
        {
            usings.Add(entityNs);
        }
        if (enumsNs is not null &&
            !string.Equals(enumsNs, mapperNs, System.StringComparison.Ordinal) &&
            !usings.Contains(enumsNs))
        {
            usings.Add(enumsNs);
        }
        foreach (var ns in usings)
        {
            sb.AppendLine($"using {ns};");
        }
        if (usings.Count > 0)
        {
            sb.AppendLine();
        }
        sb.AppendLine($"namespace {mapperNs};");
        sb.AppendLine();
        sb.AppendLine($"public static partial class {table.EntityName}Mappings");
        sb.AppendLine("{");

        sb.AppendLine($"    public static {table.EntityName}Dto ToDto(this {table.EntityName} entity)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var dto = new {table.EntityName}Dto");
        sb.AppendLine("        {");
        foreach (var c in table.Columns)
        {
            if (isV2 && c.EnumTypeName is not null)
            {
                sb.AppendLine($"            {c.PropertyName} = System.Enum.Parse<{c.EnumTypeName}>(entity.{c.PropertyName}, ignoreCase: true),");
            }
            else
            {
                sb.AppendLine($"            {c.PropertyName} = entity.{c.PropertyName},");
            }
        }
        sb.AppendLine("        };");
        sb.AppendLine("        OnMapped(entity, dto);");
        sb.AppendLine("        return dto;");
        sb.AppendLine("    }");

        // Views: read-only, no Create/Update mappers.
        if (!table.IsView)
        {
            sb.AppendLine();
            sb.AppendLine($"    public static {table.EntityName} ToEntity(this Create{table.EntityName}Dto dto) => new()");
            sb.AppendLine("    {");
            foreach (var c in table.Columns)
            {
                if (c.IsIdentity) { continue; }

                if (isV2 && c.EnumTypeName is not null)
                {
                    sb.AppendLine($"        {c.PropertyName} = dto.{c.PropertyName}.ToString(),");
                }
                else
                {
                    sb.AppendLine($"        {c.PropertyName} = dto.{c.PropertyName},");
                }
            }
            sb.AppendLine("    };");
            sb.AppendLine();

            sb.AppendLine($"    public static void UpdateFromDto(this {table.EntityName} entity, Update{table.EntityName}Dto dto)");
            sb.AppendLine("    {");
            foreach (var c in table.Columns)
            {
                if (c.IsIdentity) { continue; }

                if (isV2 && c.EnumTypeName is not null)
                {
                    sb.AppendLine($"        entity.{c.PropertyName} = dto.{c.PropertyName}.ToString();");
                }
                else
                {
                    sb.AppendLine($"        entity.{c.PropertyName} = dto.{c.PropertyName};");
                }
            }
            sb.AppendLine("    }");
        }

        sb.AppendLine();
        sb.AppendLine($"    static partial void OnMapped({table.EntityName} entity, {table.EntityName}Dto dto);");
        sb.AppendLine("}");

        return new EmittedFile(layout.MapperPath(config, table.Schema, table.EntityName), sb.ToString());
    }

    private static string SegmentNamespace(ApiSmithConfig config, string schema)
    {
        var emit = config.Schemas.Count > 1 ||
                   !string.Equals(schema, "dbo", System.StringComparison.OrdinalIgnoreCase);
        return emit ? "." + ApiSmith.Naming.SchemaSegment.ToPascal(schema) : string.Empty;
    }
}
