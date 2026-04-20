namespace ApiSmith.Templating;

/// <summary>Loads raw template text by logical path (e.g. <c>"Entity/Entity.apismith"</c>).</summary>
public interface ITemplateSource
{
    bool TryLoad(string templateName, out string source);
}
