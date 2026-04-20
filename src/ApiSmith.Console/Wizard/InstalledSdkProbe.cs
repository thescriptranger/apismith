using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ApiSmith.Console.Wizard;

/// <summary>Shells <c>dotnet --list-sdks</c> to find installed TFMs; falls back to net9.0/net8.0 on any failure.</summary>
public static class InstalledSdkProbe
{
    private static readonly ImmutableArray<string> FallbackTfms =
        ImmutableArray.Create("net9.0", "net8.0");

    // major.minor.patch with optional "-preview.2" / "-rc.1" suffix, anchored.
    private static readonly Regex SdkLineRegex = new(
        @"^\s*(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?<suffix>-[^\s\[]+)?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>TFMs sorted GA-desc first, previews last; fallback on probe failure.</summary>
    public static ImmutableArray<string> DetectInstalledTfms()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "--list-sdks",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                return FallbackTfms;
            }

            var stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                return FallbackTfms;
            }

            var parsed = ParseListSdks(stdout);
            return parsed.Length == 0 ? FallbackTfms : parsed;
        }
        catch (Exception)
        {
            // no dotnet on PATH, permission denied, etc.
            return FallbackTfms;
        }
    }

    /// <summary>Parses <c>dotnet --list-sdks</c> output. Public for testability.</summary>
    public static ImmutableArray<string> ParseListSdks(string stdout)
    {
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return ImmutableArray<string>.Empty;
        }

        // value = has-GA-sdk; previews-only buckets sort after GA ones.
        var groups = new Dictionary<(int Major, int Minor), bool>();

        var lines = stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var match = SdkLineRegex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            if (!int.TryParse(match.Groups["major"].Value, out var major) ||
                !int.TryParse(match.Groups["minor"].Value, out var minor))
            {
                continue;
            }

            var isGa = !match.Groups["suffix"].Success;
            var key = (major, minor);

            if (groups.TryGetValue(key, out var existingGa))
            {
                groups[key] = existingGa || isGa;
            }
            else
            {
                groups[key] = isGa;
            }
        }

        if (groups.Count == 0)
        {
            return ImmutableArray<string>.Empty;
        }

        var gaOrdered = new List<string>();
        var previewOrdered = new List<string>();

        foreach (var kvp in groups)
        {
            var tfm = $"net{kvp.Key.Major}.{kvp.Key.Minor}";
            if (kvp.Value)
            {
                gaOrdered.Add(tfm);
            }
            else
            {
                previewOrdered.Add(tfm);
            }
        }

        // numeric sort — avoids lexical "net9.0" > "net10.0" bug
        gaOrdered.Sort(CompareTfmDescending);
        previewOrdered.Sort(CompareTfmDescending);

        var result = ImmutableArray.CreateBuilder<string>(gaOrdered.Count + previewOrdered.Count);
        result.AddRange(gaOrdered);
        result.AddRange(previewOrdered);
        return result.ToImmutable();
    }

    private static int CompareTfmDescending(string left, string right)
    {
        var leftKey = ParseTfm(left);
        var rightKey = ParseTfm(right);

        var majorCompare = rightKey.Major.CompareTo(leftKey.Major);
        return majorCompare != 0 ? majorCompare : rightKey.Minor.CompareTo(leftKey.Minor);
    }

    private static (int Major, int Minor) ParseTfm(string tfm)
    {
        // format is guaranteed "net{M}.{N}" — internal callers only
        var dot = tfm.IndexOf('.', StringComparison.Ordinal);
        var major = int.Parse(tfm.AsSpan(3, dot - 3), System.Globalization.CultureInfo.InvariantCulture);
        var minor = int.Parse(tfm.AsSpan(dot + 1), System.Globalization.CultureInfo.InvariantCulture);
        return (major, minor);
    }
}
