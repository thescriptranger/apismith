using System.Globalization;

namespace ApiSmith.Naming;

/// <summary>English pluralizer/singularizer. Irregulars table + fallback rules.</summary>
public static class Pluralizer
{
    private static readonly Dictionary<string, string> Irregulars = new(System.StringComparer.OrdinalIgnoreCase)
    {
        ["person"]    = "people",
        ["man"]       = "men",
        ["woman"]     = "women",
        ["child"]     = "children",
        ["tooth"]     = "teeth",
        ["foot"]      = "feet",
        ["mouse"]     = "mice",
        ["goose"]     = "geese",
        ["ox"]        = "oxen",
        ["datum"]     = "data",
        ["criterion"] = "criteria",
        ["analysis"]  = "analyses",
        ["matrix"]    = "matrices",
        ["index"]     = "indexes",
        ["vertex"]    = "vertices",
        ["status"]    = "statuses",
        ["quiz"]      = "quizzes",
        ["knife"]     = "knives",
        ["wife"]      = "wives",
        ["life"]      = "lives",
    };

    private static readonly HashSet<string> Uncountables = new(System.StringComparer.OrdinalIgnoreCase)
    {
        "equipment", "information", "rice", "money", "species", "series",
        "fish", "sheep", "deer", "metadata",
    };

    public static string Pluralize(string word)
    {
        if (string.IsNullOrEmpty(word))
            return word;

        if (Uncountables.Contains(word))
            return word;

        if (Irregulars.TryGetValue(word, out var plural))
            return MatchCase(word, plural);

        var lower = word.ToLower(CultureInfo.InvariantCulture);

        if (EndsWith(lower, "s") || EndsWith(lower, "x") || EndsWith(lower, "z") ||
            EndsWith(lower, "ch") || EndsWith(lower, "sh"))
        {
            return word + "es";
        }

        if (lower.Length >= 2 && lower[^1] == 'y' && !IsVowel(lower[^2]))
            return word[..^1] + "ies";

        if (EndsWith(lower, "fe"))
            return word[..^2] + "ves";
        if (EndsWith(lower, "f"))
            return word[..^1] + "ves";

        return word + "s";
    }

    public static string Singularize(string word)
    {
        if (string.IsNullOrEmpty(word))
            return word;

        if (Uncountables.Contains(word))
            return word;

        foreach (var (single, plural) in Irregulars)
        {
            if (string.Equals(word, plural, System.StringComparison.OrdinalIgnoreCase))
                return MatchCase(word, single);
        }

        var lower = word.ToLower(CultureInfo.InvariantCulture);

        if (EndsWith(lower, "ies") && lower.Length > 3)
            return word[..^3] + "y";
        if (EndsWith(lower, "ves") && lower.Length > 3)
            return word[..^3] + "f";
        if (EndsWith(lower, "ses") || EndsWith(lower, "xes") || EndsWith(lower, "zes") ||
            EndsWith(lower, "ches") || EndsWith(lower, "shes"))
        {
            return word[..^2];
        }
        if (EndsWith(lower, "s") && !EndsWith(lower, "ss"))
            return word[..^1];

        return word;
    }

    private static bool EndsWith(string s, string suffix) =>
        s.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsVowel(char c) =>
        c is 'a' or 'e' or 'i' or 'o' or 'u';

    private static string MatchCase(string source, string target)
    {
        if (source.Length == 0)
            return target;

        if (char.IsUpper(source[0]))
            return char.ToUpper(target[0], CultureInfo.InvariantCulture) + target[1..];

        return target;
    }
}
