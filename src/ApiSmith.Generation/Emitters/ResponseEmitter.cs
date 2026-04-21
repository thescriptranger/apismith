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
            // Collect any extra usings (enums + nested-child response namespaces).
            var extraUsings = new SortedSet<string>(System.StringComparer.Ordinal);
            if (HasAnyEnumColumn(table))
            {
                extraUsings.Add($"{layout.SharedNamespace(config)}.Enums{SegmentNamespace(config, table.Schema)}");
            }
            if (ShouldEmitChildCollections(config, table))
            {
                foreach (var nav in table.CollectionNavigations)
                {
                    var childNs = layout.ResponseNamespace(config, nav.SourceSchema);
                    if (!string.Equals(childNs, ns, System.StringComparison.Ordinal))
                    {
                        extraUsings.Add(childNs);
                    }
                }
            }
            foreach (var u in extraUsings)
            {
                sb.AppendLine($"using {u};");
            }
            if (extraUsings.Count > 0) sb.AppendLine();
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
            if (ShouldEmitChildCollections(config, table))
            {
                foreach (var nav in table.CollectionNavigations)
                {
                    sb.AppendLine($"    public IReadOnlyList<{nav.SourceEntityName}Response> {nav.Name} {{ get; init; }} = System.Array.Empty<{nav.SourceEntityName}Response>();");
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

    private static bool ShouldEmitChildCollections(ApiSmithConfig config, NamedTable table) =>
        config.IncludeChildCollectionsInResponses
        && !table.IsView
        && !table.IsJoinTable
        && table.CollectionNavigations.Length > 0;
}
