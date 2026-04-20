using System.Text;
using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;

namespace ApiSmith.Generation.Emitters;

public static class DtoEmitter
{
    public static EmittedFile Emit(ApiSmithConfig config, IArchitectureLayout layout, NamedTable table)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"namespace {layout.DtoNamespace(config, table.Schema)};");
        sb.AppendLine();

        EmitClass(sb, $"{table.EntityName}Dto", table, includeIdentity: true);

        // Views: read-only, no Create/Update DTOs.
        if (!table.IsView)
        {
            sb.AppendLine();
            EmitClass(sb, $"Create{table.EntityName}Dto", table, includeIdentity: false);
            sb.AppendLine();
            EmitClass(sb, $"Update{table.EntityName}Dto", table, includeIdentity: false);
        }

        return new EmittedFile(layout.DtoPath(config, table.Schema, $"{table.EntityName}Dtos"), sb.ToString());
    }

    private static void EmitClass(StringBuilder sb, string className, NamedTable table, bool includeIdentity)
    {
        sb.AppendLine($"public sealed class {className}");
        sb.AppendLine("{");

        foreach (var c in table.Columns)
        {
            if (!includeIdentity && c.IsIdentity)
            {
                continue;
            }

            var initializer = !c.IsNullable && c.ClrTypeName == "string"
                ? " = string.Empty;"
                : string.Empty;

            sb.AppendLine($"    public {c.ClrTypeWithNullability} {c.PropertyName} {{ get; set; }}{initializer}");
        }

        sb.AppendLine("}");
    }
}
