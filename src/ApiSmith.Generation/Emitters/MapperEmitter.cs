using System.Text;
using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;

namespace ApiSmith.Generation.Emitters;

public static class MapperEmitter
{
    public static EmittedFile Emit(ApiSmithConfig config, IArchitectureLayout layout, NamedTable table)
    {
        if (config.ApiVersion == ApiVersion.V2)
        {
            return EmitV2(config, layout, table);
        }
        return EmitV1(config, layout, table);
    }

    private static EmittedFile EmitV1(ApiSmithConfig config, IArchitectureLayout layout, NamedTable table)
    {
        var sb = new StringBuilder();
        var mapperNs = layout.MapperNamespace(config, table.Schema);
        var dtoNs = layout.DtoNamespace(config, table.Schema);
        var entityNs = layout.EntityNamespace(config, table.Schema);

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
            sb.AppendLine($"            {c.PropertyName} = entity.{c.PropertyName},");
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
                if (c.IsServerGenerated) { continue; }

                sb.AppendLine($"        {c.PropertyName} = dto.{c.PropertyName},");
            }
            sb.AppendLine("    };");
            sb.AppendLine();

            sb.AppendLine($"    public static void UpdateFromDto(this {table.EntityName} entity, Update{table.EntityName}Dto dto)");
            sb.AppendLine("    {");
            foreach (var c in table.Columns)
            {
                if (c.IsServerGenerated) { continue; }

                sb.AppendLine($"        entity.{c.PropertyName} = dto.{c.PropertyName};");
            }
            sb.AppendLine("    }");
        }

        sb.AppendLine();
        sb.AppendLine($"    static partial void OnMapped({table.EntityName} entity, {table.EntityName}Dto dto);");
        sb.AppendLine("}");

        return new EmittedFile(layout.MapperPath(config, table.Schema, table.EntityName), sb.ToString());
    }

    private static EmittedFile EmitV2(ApiSmithConfig config, IArchitectureLayout layout, NamedTable table)
    {
        var mapperNs = layout.MapperNamespace(config, table.Schema);
        var dtoNs = layout.DtoNamespace(config, table.Schema);
        var entityNs = layout.EntityNamespace(config, table.Schema);
        var requestNs = layout.RequestNamespace(config, table.Schema);
        var responseNs = layout.ResponseNamespace(config, table.Schema);

        var hasEnumColumn = table.Columns.Any(c => c.EnumTypeName is not null);
        var enumsNs = hasEnumColumn
            ? $"{layout.SharedNamespace(config)}.Enums{SegmentNamespace(config, table.Schema)}"
            : null;

        var sb = new StringBuilder();

        // Deduped usings — skip any that equal mapperNs (avoid self-using, would trip CS0105 on VerticalSlice).
        // Views have no Request methods generated, so skip the Request using to avoid an unused-using warning.
        var usings = new System.Collections.Generic.List<string>();
        void Add(string ns)
        {
            if (!string.Equals(ns, mapperNs, System.StringComparison.Ordinal) && !usings.Contains(ns))
            {
                usings.Add(ns);
            }
        }
        Add(dtoNs);
        Add(entityNs);
        if (!table.IsView)
        {
            Add(requestNs);
        }
        Add(responseNs);
        if (enumsNs is not null) { Add(enumsNs); }

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

        var entity = table.EntityName;

        // For views: skip write methods (ToEntity, UpdateFromRequest).
        if (!table.IsView)
        {
            // 1. Request → Entity (new)
            sb.AppendLine($"    public static {entity} ToEntity(this Create{entity}Request request) => new()");
            sb.AppendLine("    {");
            foreach (var c in table.Columns)
            {
                if (c.IsServerGenerated) { continue; }

                var rhs = c.EnumTypeName is not null
                    ? $"request.{c.PropertyName}.ToString()"
                    : $"request.{c.PropertyName}";
                sb.AppendLine($"        {c.PropertyName} = {rhs},");
            }
            sb.AppendLine("    };");
            sb.AppendLine();

            // 2. Request → Entity (in-place)
            sb.AppendLine($"    public static void UpdateFromRequest(this {entity} entity, Update{entity}Request request)");
            sb.AppendLine("    {");
            foreach (var c in table.Columns)
            {
                if (c.IsServerGenerated) { continue; }

                var rhs = c.EnumTypeName is not null
                    ? $"request.{c.PropertyName}.ToString()"
                    : $"request.{c.PropertyName}";
                sb.AppendLine($"        entity.{c.PropertyName} = {rhs};");
            }
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        // 3. Entity → Dto
        sb.AppendLine($"    public static {entity}Dto ToDto(this {entity} entity)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var dto = new {entity}Dto");
        sb.AppendLine("        {");
        foreach (var c in table.Columns)
        {
            var rhs = c.EnumTypeName is not null
                ? $"System.Enum.Parse<{c.EnumTypeName}>(entity.{c.PropertyName}, ignoreCase: true)"
                : $"entity.{c.PropertyName}";
            sb.AppendLine($"            {c.PropertyName} = {rhs},");
        }
        sb.AppendLine("        };");
        sb.AppendLine("        OnMapped(entity, dto);");
        sb.AppendLine("        return dto;");
        sb.AppendLine("    }");
        sb.AppendLine();

        // 4. Dto → Response
        sb.AppendLine($"    public static {entity}Response ToResponse(this {entity}Dto dto)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var response = new {entity}Response");
        sb.AppendLine("        {");
        foreach (var c in table.Columns)
        {
            sb.AppendLine($"            {c.PropertyName} = dto.{c.PropertyName},");
        }
        sb.AppendLine("        };");
        sb.AppendLine("        OnMapped(dto, response);");
        sb.AppendLine("        return response;");
        sb.AppendLine("    }");
        sb.AppendLine();

        // 5. Convenience — Entity → Response
        sb.AppendLine($"    public static {entity}Response ToResponse(this {entity} entity) => entity.ToDto().ToResponse();");
        sb.AppendLine();

        sb.AppendLine($"    static partial void OnMapped({entity} entity, {entity}Dto dto);");
        sb.AppendLine($"    static partial void OnMapped({entity}Dto dto, {entity}Response response);");
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
