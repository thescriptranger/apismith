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
        sb.AppendLine($"public static class {table.EntityName}Mappings");
        sb.AppendLine("{");

        sb.AppendLine($"    public static {table.EntityName}Dto ToDto(this {table.EntityName} entity) => new()");
        sb.AppendLine("    {");
        foreach (var c in table.Columns)
        {
            sb.AppendLine($"        {c.PropertyName} = entity.{c.PropertyName},");
        }
        sb.AppendLine("    };");

        // Views: read-only, no Create/Update mappers.
        if (!table.IsView)
        {
            sb.AppendLine();
            sb.AppendLine($"    public static {table.EntityName} ToEntity(this Create{table.EntityName}Dto dto) => new()");
            sb.AppendLine("    {");
            foreach (var c in table.Columns)
            {
                if (c.IsIdentity) { continue; }

                sb.AppendLine($"        {c.PropertyName} = dto.{c.PropertyName},");
            }
            sb.AppendLine("    };");
            sb.AppendLine();

            sb.AppendLine($"    public static void UpdateFromDto(this {table.EntityName} entity, Update{table.EntityName}Dto dto)");
            sb.AppendLine("    {");
            foreach (var c in table.Columns)
            {
                if (c.IsIdentity) { continue; }

                sb.AppendLine($"        entity.{c.PropertyName} = dto.{c.PropertyName};");
            }
            sb.AppendLine("    }");
        }

        sb.AppendLine("}");

        return new EmittedFile(layout.MapperPath(config, table.Schema, table.EntityName), sb.ToString());
    }
}
