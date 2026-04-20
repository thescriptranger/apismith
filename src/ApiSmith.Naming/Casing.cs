using System.Globalization;
using System.Text;

namespace ApiSmith.Naming;

public static class Casing
{
    /// <summary>Any case to PascalCase. Leading digits get <c>_</c> prefix.</summary>
    public static string ToPascal(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return raw;

        var words = SplitToWords(raw);
        var sb = new StringBuilder(raw.Length);

        foreach (var word in words)
        {
            if (word.Length == 0)
                continue;

            sb.Append(char.ToUpper(word[0], CultureInfo.InvariantCulture));
            if (word.Length > 1)
                sb.Append(word.AsSpan(1).ToString().ToLower(CultureInfo.InvariantCulture));
        }

        if (sb.Length == 0)
            return string.Empty;

        if (char.IsDigit(sb[0]))
            sb.Insert(0, '_');

        return sb.ToString();
    }

    public static string ToCamel(string raw)
    {
        var pascal = ToPascal(raw);
        if (pascal.Length == 0)
            return pascal;

        return char.ToLower(pascal[0], CultureInfo.InvariantCulture) + pascal[1..];
    }

    private static string[] SplitToWords(string raw)
    {
        var pieces = new System.Collections.Generic.List<string>();
        var current = new StringBuilder();

        for (var i = 0; i < raw.Length; i++)
        {
            var c = raw[i];

            if (c is '_' or '-' or ' ' or '.')
            {
                Flush();
                continue;
            }

            // split on lower→upper for existing Pascal/camel input
            if (current.Length > 0 && char.IsUpper(c) && char.IsLower(current[^1]))
                Flush();

            current.Append(c);
        }

        Flush();
        return pieces.ToArray();

        void Flush()
        {
            if (current.Length > 0)
            {
                pieces.Add(current.ToString());
                current.Clear();
            }
        }
    }
}
