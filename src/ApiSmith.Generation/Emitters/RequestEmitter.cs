using System.Globalization;
using System.Text;
using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;
using ApiSmith.Generation.Validation;

namespace ApiSmith.Generation.Emitters;

public static class RequestEmitter
{
    public static IEnumerable<EmittedFile> Emit(ApiSmithConfig config, IArchitectureLayout layout, NamedSchemaModel named)
    {
        if (config.ApiVersion != ApiVersion.V2) yield break;

        foreach (var table in named.Tables)
        {
            if (table.PrimaryKey is null) continue; // views — no Create/Update, skip

            var fileName = $"{table.EntityName}Requests";
            var path = layout.RequestPath(config, table.Schema, fileName);
            var ns = layout.RequestNamespace(config, table.Schema);

            var sb = new StringBuilder();
            sb.AppendLine("using System.ComponentModel.DataAnnotations;");
            if (HasAnyEnumColumn(table))
            {
                sb.AppendLine($"using {layout.SharedNamespace(config)}.Enums{SegmentNamespace(config, table.Schema)};");
            }
            sb.AppendLine();
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();

            EmitRequestClass(sb, table, $"Create{table.EntityName}Request");
            sb.AppendLine();
            EmitRequestClass(sb, table, $"Update{table.EntityName}Request");

            yield return new EmittedFile(path, sb.ToString());
        }
    }

    private static string SegmentNamespace(ApiSmithConfig config, string schema)
    {
        var emit = config.Schemas.Count > 1 ||
                   !string.Equals(schema, "dbo", System.StringComparison.OrdinalIgnoreCase);
        return emit ? "." + ApiSmith.Naming.SchemaSegment.ToPascal(schema) : string.Empty;
    }

    private static void EmitRequestClass(StringBuilder sb, NamedTable table, string className)
    {
        sb.AppendLine($"public sealed class {className}");
        sb.AppendLine("{");
        foreach (var c in table.Columns)
        {
            if (c.IsServerGenerated) continue;

            var useEnumType = c.EnumTypeName is not null;

            // [Required] for NOT NULL strings (not applied when we emit the enum type)
            if (!c.IsNullable && c.ClrTypeName == "string" && !useEnumType)
            {
                sb.AppendLine("    [Required]");
            }
            // [StringLength] for strings with MaxLength
            if (c.ClrTypeName == "string" && c.MaxLength is int max && max > 0 && !useEnumType)
            {
                sb.AppendLine($"    [StringLength({max})]");
            }

            // [Range] for translatable numeric check constraints (ComparisonRule / BetweenRule)
            if (table.Source is { } src)
            {
                foreach (var ck in src.CheckConstraints)
                {
                    var rule = CheckConstraintTranslator.TryTranslate(ck.Expression);
                    string? lo = null;
                    string? hi = null;
                    if (rule is ComparisonRule cmp && string.Equals(cmp.Column, c.DbName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        (lo, hi) = cmp.Operator switch
                        {
                            ">=" => (cmp.LiteralValue.ToString(CultureInfo.InvariantCulture), "long.MaxValue"),
                            ">"  => ((cmp.LiteralValue + 1).ToString(CultureInfo.InvariantCulture), "long.MaxValue"),
                            "<=" => ("long.MinValue", cmp.LiteralValue.ToString(CultureInfo.InvariantCulture)),
                            "<"  => ("long.MinValue", (cmp.LiteralValue - 1).ToString(CultureInfo.InvariantCulture)),
                            _    => ((string?)null, (string?)null),
                        };
                    }
                    else if (rule is BetweenRule btw && string.Equals(btw.Column, c.DbName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        lo = btw.LowerInclusive.ToString(CultureInfo.InvariantCulture);
                        hi = btw.UpperInclusive.ToString(CultureInfo.InvariantCulture);
                    }
                    if (lo is not null && hi is not null)
                    {
                        sb.AppendLine($"    [Range({lo}, {hi})]");
                        break;
                    }
                }
            }

            if (useEnumType)
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
