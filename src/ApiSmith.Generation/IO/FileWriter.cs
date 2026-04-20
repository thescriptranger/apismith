using System.Collections.Immutable;
using System.Text;
using ApiSmith.Core.Pipeline;

namespace ApiSmith.Generation.IO;

public static class FileWriter
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

    /// <summary>LF-normalized, UTF-8 no-BOM (BOM only on <c>.sln</c> per convention), written in path order.</summary>
    public static void Write(string outputRoot, ImmutableArray<EmittedFile> files)
    {
        Directory.CreateDirectory(outputRoot);

        foreach (var file in files.OrderBy(f => f.RelativePath, System.StringComparer.Ordinal))
        {
            var fullPath = Path.Combine(outputRoot, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var encoding = file.RelativePath.EndsWith(".sln", System.StringComparison.OrdinalIgnoreCase)
                ? Utf8WithBom
                : Utf8NoBom;

            File.WriteAllText(fullPath, NewlineNormalizer.ToLf(file.Content), encoding);
        }
    }
}
