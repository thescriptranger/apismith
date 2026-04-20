using System.Globalization;
using System.Text;
using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;
using ApiSmith.Generation.Validation;

namespace ApiSmith.Generation.Emitters;

public static class DtoEmitter
{
    public static EmittedFile Emit(ApiSmithConfig config, IArchitectureLayout layout, NamedTable table)
    {
        var sb = new StringBuilder();

        var isV2 = config.ApiVersion == ApiVersion.V2;
        var hasEnumColumn = isV2 && table.Columns.Any(c => c.EnumTypeName is not null);
        if (isV2)
        {
            sb.AppendLine("using System.ComponentModel.DataAnnotations;");
            if (hasEnumColumn)
            {
                sb.AppendLine($"using {layout.SharedNamespace(config)}.Enums{SegmentNamespace(config, table.Schema)};");
            }
            sb.AppendLine();
        }

        sb.AppendLine($"namespace {layout.DtoNamespace(config, table.Schema)};");
        sb.AppendLine();

        EmitClass(sb, $"{table.EntityName}Dto", table, includeIdentity: true, isWriteDto: false, emitAttributes: false, isV2: isV2);

        // Views: read-only, no Create/Update DTOs.
        if (!table.IsView)
        {
            sb.AppendLine();
            EmitClass(sb, $"Create{table.EntityName}Dto", table, includeIdentity: false, isWriteDto: true, emitAttributes: isV2, isV2: isV2);
            sb.AppendLine();
            EmitClass(sb, $"Update{table.EntityName}Dto", table, includeIdentity: false, isWriteDto: true, emitAttributes: isV2, isV2: isV2);
        }

        return new EmittedFile(layout.DtoPath(config, table.Schema, $"{table.EntityName}Dtos"), sb.ToString());
    }

    private static string SegmentNamespace(ApiSmithConfig config, string schema)
    {
        var emit = config.Schemas.Count > 1 ||
                   !string.Equals(schema, "dbo", System.StringComparison.OrdinalIgnoreCase);
        return emit ? "." + ApiSmith.Naming.SchemaSegment.ToPascal(schema) : string.Empty;
    }

    private static void EmitClass(StringBuilder sb, string className, NamedTable table, bool includeIdentity, bool isWriteDto, bool emitAttributes, bool isV2)
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

            if (emitAttributes && isWriteDto)
            {
                if (!c.IsNullable && c.ClrTypeName == "string" && !useEnumType)
                {
                    sb.AppendLine("    [Required]");
                }

                if (c.ClrTypeName == "string" && c.MaxLength.HasValue && !useEnumType)
                {
                    sb.AppendLine($"    [StringLength({c.MaxLength.Value})]");
                }

                if (table.Source is { } src)
                {
                    foreach (var ck in src.CheckConstraints)
                    {
                        var translated = CheckConstraintTranslator.TryTranslate(ck.Expression);
                        string? lo = null;
                        string? hi = null;

                        if (translated is ComparisonRule cmp &&
                            string.Equals(cmp.Column, c.DbName, System.StringComparison.OrdinalIgnoreCase))
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
                        else if (translated is BetweenRule btw &&
                            string.Equals(btw.Column, c.DbName, System.StringComparison.OrdinalIgnoreCase))
                        {
                            lo = btw.LowerInclusive.ToString(CultureInfo.InvariantCulture);
                            hi = btw.UpperInclusive.ToString(CultureInfo.InvariantCulture);
                        }

                        if (lo is not null && hi is not null)
                        {
                            sb.AppendLine($"    [Range({lo}, {hi})]");
                            // At most one [Range] per property even if multiple CHECKs target this column.
                            break;
                        }
                    }
                }
            }

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
