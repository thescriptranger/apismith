using System.Text.RegularExpressions;

namespace ApiSmith.Generation;

/// <summary>Parsed CHECK IN constraint: a column and its allowed string values.</summary>
public sealed record EnumInList(string Column, IReadOnlyList<string> Values);

public static class EnumCandidates
{
    private static readonly Regex InListPattern = new(
        @"^\s*\(?\s*\[?(?<col>[A-Za-z_][A-Za-z_0-9]*)\]?\s+IN\s*\(\s*(?<vals>'[^']*'(\s*,\s*'[^']*')*)\s*\)\s*\)?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ValuePattern = new("'([^']*)'", RegexOptions.Compiled);

    /// <summary>
    /// Parses <c>CHECK IN ('a','b','c')</c> expressions. Returns null for anything else,
    /// including case-colliding values that would yield duplicate enum members.
    /// </summary>
    public static EnumInList? TryParseInList(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return null;
        var m = InListPattern.Match(expression);
        if (!m.Success) return null;

        var values = ValuePattern.Matches(m.Groups["vals"].Value)
            .Select(mm => mm.Groups[1].Value)
            .ToList();

        // Reject case-colliding values — they'd pascalize to the same enum member.
        var caseFolded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in values)
        {
            if (!caseFolded.Add(v)) return null;
        }

        return new EnumInList(m.Groups["col"].Value, values);
    }
}
