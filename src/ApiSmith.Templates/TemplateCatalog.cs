using System.Reflection;
using ApiSmith.Templating;

namespace ApiSmith.Templates;

/// <summary>
/// Loads <c>.apismith</c> templates bundled as embedded resources inside this
/// assembly. Template names use forward slashes (e.g. <c>"Entity/Entity.apismith"</c>)
/// and are mapped to resource names by replacing slashes with dots and prefixing
/// with the assembly's default namespace + <c>.Files</c>.
/// </summary>
public sealed class TemplateCatalog : ITemplateSource
{
    private const string FilesPrefix = "ApiSmith.Templates.Files.";

    private readonly Assembly _assembly;

    public TemplateCatalog()
    {
        _assembly = typeof(TemplateCatalog).Assembly;
    }

    public bool TryLoad(string templateName, out string source)
    {
        var resourceName = FilesPrefix + templateName.Replace('/', '.');

        using var stream = _assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            source = string.Empty;
            return false;
        }

        using var reader = new StreamReader(stream);
        source = reader.ReadToEnd();
        return true;
    }

    /// <summary>
    /// Enumerates every embedded <c>.apismith</c> template name (logical path,
    /// forward slashes). Useful for sanity tests.
    /// </summary>
    public IEnumerable<string> EnumerateTemplateNames()
    {
        foreach (var name in _assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(FilesPrefix, System.StringComparison.Ordinal))
            {
                continue;
            }

            var tail = name[FilesPrefix.Length..];
            // Resource names use dots between path segments; the last dot precedes the extension.
            // We emit the logical template path as "<segments-joined-by-slash>.<extension>".
            var lastDot = tail.LastIndexOf('.');
            if (lastDot <= 0)
            {
                continue;
            }

            var head = tail[..lastDot].Replace('.', '/');
            var ext = tail[lastDot..];
            yield return head + ext;
        }
    }
}
