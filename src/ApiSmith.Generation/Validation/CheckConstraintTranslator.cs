using System.Globalization;
using System.Text.RegularExpressions;

namespace ApiSmith.Generation.Validation;

public sealed record ComparisonRule(string Column, string Operator, long LiteralValue);

public sealed record BetweenRule(string Column, long LowerInclusive, long UpperInclusive);

/// <summary>Parses simple SQL Server check-constraints into typed rules; unknown shapes return null.</summary>
public static class CheckConstraintTranslator
{
    // Bare or bracket-quoted, captured as 'col'.
    private const string Identifier = @"\[?(?<col>[A-Za-z_][A-Za-z0-9_]*)\]?";

    // Signed int optionally wrapped in parens (e.g. ((-7))); captured as 'val'.
    private const string IntLiteral = @"\(*\s*(?<val>-?\d+)\s*\)*";

    private static readonly Regex ComparisonPattern = new(
        pattern: @"^\s*\(?\s*" + Identifier + @"\s*(?<op>>=|<=|>|<)\s*" + IntLiteral + @"\s*\)?\s*$",
        options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BetweenPattern = new(
        pattern: @"^\s*\(?\s*" + Identifier + @"\s+between\s+\(*\s*(?<lo>-?\d+)\s*\)*\s+and\s+\(*\s*(?<hi>-?\d+)\s*\)*\s*\)?\s*$",
        options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Returns a <see cref="ComparisonRule"/>/<see cref="BetweenRule"/>, or null for unsupported shapes (IN, OR, IS NULL, fn calls, multi-column, computed).</summary>
    public static object? TryTranslate(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        var m = ComparisonPattern.Match(expression);
        if (m.Success)
        {
            if (!long.TryParse(m.Groups["val"].Value, NumberStyles.Integer, CultureInvariant, out var literal))
            {
                return null;
            }

            return new ComparisonRule(
                Column: m.Groups["col"].Value,
                Operator: m.Groups["op"].Value,
                LiteralValue: literal);
        }

        m = BetweenPattern.Match(expression);
        if (m.Success)
        {
            if (!long.TryParse(m.Groups["lo"].Value, NumberStyles.Integer, CultureInvariant, out var lo) ||
                !long.TryParse(m.Groups["hi"].Value, NumberStyles.Integer, CultureInvariant, out var hi))
            {
                return null;
            }

            return new BetweenRule(
                Column: m.Groups["col"].Value,
                LowerInclusive: lo,
                UpperInclusive: hi);
        }

        return null;
    }

    private static readonly CultureInfo CultureInvariant = CultureInfo.InvariantCulture;
}
