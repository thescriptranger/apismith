using System.Text;
using ApiSmith.Templating.Parsing;
using ApiSmith.Templating.Rendering;

namespace ApiSmith.Templating;

/// <summary>Renders a template by name: loads via <see cref="ITemplateSource"/>, parses (cached), renders into a <see cref="TemplateContext"/>.</summary>
public sealed class TemplateEngine
{
    private readonly ITemplateSource _sources;
    private readonly Dictionary<string, TemplateAst> _cache = new(System.StringComparer.Ordinal);
    private readonly System.Threading.Lock _cacheLock = new();

    public TemplateEngine(ITemplateSource sources)
    {
        _sources = sources;
    }

    public string Render(string templateName, object? root)
    {
        return Render(templateName, new TemplateContext(root));
    }

    public string Render(string templateName, TemplateContext ctx)
    {
        var ast = GetAst(templateName);
        var sb = new StringBuilder();
        Renderer.Render(ast, ctx, _sources, sb, includeDepth: 0);
        return sb.ToString();
    }

    private TemplateAst GetAst(string templateName)
    {
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(templateName, out var cached))
            {
                return cached;
            }
        }

        if (!_sources.TryLoad(templateName, out var source))
        {
            throw new TemplateException($"Template '{templateName}' not found in the registered template source.");
        }

        var ast = TemplateParser.Parse(templateName, source);

        lock (_cacheLock)
        {
            _cache[templateName] = ast;
        }

        return ast;
    }
}
