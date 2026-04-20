using System.Text;
using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;
using ApiSmith.Naming;

namespace ApiSmith.Generation.Emitters;

public static class EnumEmitter
{
    public static IEnumerable<EmittedFile> Emit(ApiSmithConfig config, IArchitectureLayout layout, NamedSchemaModel named)
    {
        if (config.ApiVersion != ApiVersion.V2) yield break;

        var emitted = new HashSet<(string schema, string enumName)>();

        foreach (var table in named.Tables.Concat(named.JoinTables))
        {
            if (table.Source is not { } src) continue;

            foreach (var ck in src.CheckConstraints)
            {
                var parsed = EnumCandidates.TryParseInList(ck.Expression);
                if (parsed is null) continue;

                // Only emit if a DTO property would reference this column —
                // identity/computed columns are excluded from DTOs.
                var col = src.Columns.FirstOrDefault(c =>
                    string.Equals(c.Name, parsed.Column, System.StringComparison.OrdinalIgnoreCase));
                if (col is null || col.IsIdentity || col.IsComputed) continue;

                var enumName = Casing.ToPascal(parsed.Column);
                var dedup = (table.Schema, enumName);
                if (!emitted.Add(dedup)) continue;

                var folderSegment = SegmentFolder(config, table.Schema);
                var namespaceSegment = SegmentNamespace(config, table.Schema);
                var path = $"{layout.SharedProjectFolder(config)}/Enums{folderSegment}/{enumName}.cs";
                var ns = $"{layout.SharedNamespace(config)}.Enums{namespaceSegment}";

                var sb = new StringBuilder();
                sb.AppendLine($"namespace {ns};");
                sb.AppendLine();
                sb.AppendLine($"public enum {enumName}");
                sb.AppendLine("{");
                foreach (var v in parsed.Values)
                {
                    sb.AppendLine($"    {Casing.ToPascal(v)},");
                }
                sb.AppendLine("}");

                yield return new EmittedFile(path, sb.ToString());
            }
        }
    }

    private static string SegmentFolder(ApiSmithConfig c, string schema)
    {
        var emit = c.Schemas.Count > 1 ||
                   !string.Equals(schema, "dbo", System.StringComparison.OrdinalIgnoreCase);
        return emit ? "/" + SchemaSegment.ToPascal(schema) : string.Empty;
    }

    private static string SegmentNamespace(ApiSmithConfig c, string schema)
    {
        var emit = c.Schemas.Count > 1 ||
                   !string.Equals(schema, "dbo", System.StringComparison.OrdinalIgnoreCase);
        return emit ? "." + SchemaSegment.ToPascal(schema) : string.Empty;
    }
}
