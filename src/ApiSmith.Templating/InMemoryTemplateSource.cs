namespace ApiSmith.Templating;

public sealed class InMemoryTemplateSource : ITemplateSource
{
    private readonly Dictionary<string, string> _sources = new(System.StringComparer.Ordinal);

    public InMemoryTemplateSource Add(string name, string source)
    {
        _sources[name] = source;
        return this;
    }

    public bool TryLoad(string templateName, out string source) =>
        _sources.TryGetValue(templateName, out source!);
}
