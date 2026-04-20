using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;
using ApiSmith.Generation.Views;
using ApiSmith.Templates;
using ApiSmith.Templating;

namespace ApiSmith.Generation.Emitters;

/// <summary>Renders an entity via the <c>.apismith</c> template engine.</summary>
public static class EntityEmitter
{
    private static readonly TemplateEngine Engine = new(new TemplateCatalog());

    public static EmittedFile Emit(ApiSmithConfig config, IArchitectureLayout layout, NamedSchemaModel named, NamedTable table)
    {
        var view = EntityView.Build(config, layout, named, table);
        var content = Engine.Render("Flat/Entity.apismith", view);
        return new EmittedFile(layout.EntityPath(config, table.Schema, table.EntityName), content);
    }
}
