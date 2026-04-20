using System.Globalization;
using System.Text;
using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;
using ApiSmith.Generation.Validation;
using ApiSmith.Naming;

namespace ApiSmith.Generation.Emitters;

public static class ValidatorEmitter
{
    public static EmittedFile Emit(ApiSmithConfig config, IArchitectureLayout layout, NamedTable table)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"using {layout.DtoNamespace(config, table.Schema)};");
        if (config.ApiVersion == ApiVersion.V2)
        {
            sb.AppendLine($"using {layout.SharedErrorsNamespace(config)};");
        }
        sb.AppendLine();
        sb.AppendLine($"namespace {layout.ValidatorNamespace(config, table.Schema)};");
        sb.AppendLine();

        EmitValidator(sb, config, table, $"Create{table.EntityName}Dto", $"Create{table.EntityName}DtoValidator");
        sb.AppendLine();
        EmitValidator(sb, config, table, $"Update{table.EntityName}Dto", $"Update{table.EntityName}DtoValidator");

        return new EmittedFile(layout.ValidatorPath(config, table.Schema, table.EntityName), sb.ToString());
    }

    private static void EmitValidator(StringBuilder sb, ApiSmithConfig config, NamedTable table, string dtoName, string validatorName)
    {
        sb.AppendLine($"public sealed partial class {validatorName} : IValidator<{dtoName}>");
        sb.AppendLine("{");
        sb.AppendLine($"    public ValidationResult Validate({dtoName} dto)");
        sb.AppendLine("    {");
        sb.AppendLine("        var result = new ValidationResult();");
        sb.AppendLine();
        sb.AppendLine("        if (dto is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            result.Add(string.Empty, \"DTO must not be null.\");");
        sb.AppendLine("            return result;");
        sb.AppendLine("        }");
        sb.AppendLine();

        foreach (var c in table.Columns)
        {
            if (c.IsIdentity)
            {
                continue;
            }

            if (!c.IsNullable && c.ClrTypeName == "string")
            {
                sb.AppendLine($"        if (string.IsNullOrWhiteSpace(dto.{c.PropertyName}))");
                sb.AppendLine("        {");
                sb.AppendLine($"            result.Add(nameof(dto.{c.PropertyName}), \"{c.PropertyName} is required.\");");
                sb.AppendLine("        }");
            }

            if (c.MaxLength is int max && max > 0 && c.ClrTypeName == "string")
            {
                sb.AppendLine($"        if (dto.{c.PropertyName} is not null && dto.{c.PropertyName}.Length > {max})");
                sb.AppendLine("        {");
                sb.AppendLine($"            result.Add(nameof(dto.{c.PropertyName}), \"{c.PropertyName} must be {max} characters or fewer.\");");
                sb.AppendLine("        }");
            }
        }

        if (config.ValidateForeignKeyReferences)
        {
            foreach (var nav in table.ReferenceNavigations)
            {
                if (nav.IsOptional)
                {
                    continue;
                }

                sb.AppendLine($"        // TODO: confirm {nav.FkPropertyName} references an existing {nav.TargetEntityName}");
                sb.AppendLine($"        if (dto.{nav.FkPropertyName} == default)");
                sb.AppendLine("        {");
                sb.AppendLine($"            result.Add(nameof(dto.{nav.FkPropertyName}), \"{nav.FkPropertyName} is required.\");");
                sb.AppendLine("        }");
            }
        }

        // Views (Source == null) have no check constraints.
        if (table.Source is { } source && source.CheckConstraints.Length > 0)
        {
            var propByColumn = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var col in table.Columns)
            {
                propByColumn[col.DbName] = col.PropertyName;
            }

            foreach (var cc in source.CheckConstraints)
            {
                EmitCheckConstraint(sb, propByColumn, cc.Name, cc.Expression);
            }
        }

        sb.AppendLine();
        sb.AppendLine("        ExtendValidate(dto, result);");
        sb.AppendLine("        return result;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    partial void ExtendValidate({dtoName} dto, ValidationResult result);");
        sb.AppendLine("}");
    }

    private static void EmitCheckConstraint(
        StringBuilder sb,
        IReadOnlyDictionary<string, string> propByColumn,
        string ckName,
        string expression)
    {
        // Collapse CRLF/LF so the emitted single-line comment never wraps.
        var expressionForComment = expression.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');

        var translated = CheckConstraintTranslator.TryTranslate(expression);

        switch (translated)
        {
            case ComparisonRule cmp:
            {
                var prop = PropertyNameFor(propByColumn, cmp.Column);
                // Negate the constraint — the validator fires on violation.
                var errorOp = cmp.Operator switch
                {
                    ">=" => "<",
                    ">"  => "<=",
                    "<=" => ">",
                    "<"  => ">=",
                    _    => null,
                };

                if (errorOp is null)
                {
                    // Unknown operator — fall through to a TODO.
                    sb.AppendLine($"        // TODO: translate check constraint '{ckName}': {expressionForComment}");
                    return;
                }

                var literal = cmp.LiteralValue.ToString(CultureInfo.InvariantCulture);
                sb.AppendLine($"        // From SQL check constraint '{ckName}': {expressionForComment}");
                sb.AppendLine($"        if (dto.{prop} {errorOp} {literal})");
                sb.AppendLine("        {");
                sb.AppendLine($"            result.Add(nameof(dto.{prop}), \"{prop} must be {cmp.Operator} {literal}.\");");
                sb.AppendLine("        }");
                return;
            }

            case BetweenRule between:
            {
                var prop = PropertyNameFor(propByColumn, between.Column);
                var lo = between.LowerInclusive.ToString(CultureInfo.InvariantCulture);
                var hi = between.UpperInclusive.ToString(CultureInfo.InvariantCulture);

                sb.AppendLine($"        // From SQL check constraint '{ckName}': {expressionForComment}");
                sb.AppendLine($"        if (dto.{prop} < {lo} || dto.{prop} > {hi})");
                sb.AppendLine("        {");
                sb.AppendLine($"            result.Add(nameof(dto.{prop}), \"{prop} must be between {lo} and {hi}.\");");
                sb.AppendLine("        }");
                return;
            }

            default:
                sb.AppendLine($"        // TODO: translate check constraint '{ckName}': {expressionForComment}");
                return;
        }
    }

    private static string PropertyNameFor(IReadOnlyDictionary<string, string> propByColumn, string column)
        => propByColumn.TryGetValue(column, out var name)
            ? name
            : Casing.ToPascal(column);
}
