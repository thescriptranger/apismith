using System.Globalization;
using ApiSmith.Naming;

namespace ApiSmith.Templating.Rendering;

/// <summary>Allowlist of <c>{{ path | filter }}</c> names. Unknown names throw at render time.</summary>
internal static class Filters
{
    public static string Apply(string templateName, int line, int column, string value, string filterName)
    {
        return filterName switch
        {
            "pascal"   => Casing.ToPascal(value),
            "camel"    => Casing.ToCamel(value),
            "plural"   => Pluralizer.Pluralize(value),
            "singular" => Pluralizer.Singularize(value),
            "upper"    => value.ToUpper(CultureInfo.InvariantCulture),
            "lower"    => value.ToLower(CultureInfo.InvariantCulture),
            _ => throw TemplateException.At(templateName, line, column,
                     $"Unknown filter '{filterName}'. Allowed: pascal, camel, plural, singular, upper, lower."),
        };
    }
}
