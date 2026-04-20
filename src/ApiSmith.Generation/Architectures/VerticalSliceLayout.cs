using System.Collections.Immutable;
using ApiSmith.Config;

namespace ApiSmith.Generation.Architectures;

/// <summary>Feature folders are already entity-scoped, so DB schema segments aren't applied anywhere here.</summary>
public sealed class VerticalSliceLayout : ArchitectureLayoutBase
{
    public override ArchitectureStyle Style => ArchitectureStyle.VerticalSlice;

    public override string ApiProjectAssemblyName(ApiSmithConfig c) => c.ProjectName;
    public override string ApiProjectFolder(ApiSmithConfig c)        => $"src/{c.ProjectName}";

    public override ImmutableArray<ProjectDefinition> Projects(ApiSmithConfig config)
    {
        var name = ApiProjectAssemblyName(config);
        var csproj = config.DataAccess is DataAccessStyle.EfCore
            ? CsprojTemplates.WebProjectWithEfCore(config, name, name, string.Empty)
            : CsprojTemplates.WebProjectWithDapper(config, name, name, string.Empty);

        return ImmutableArray.Create(new ProjectDefinition(
            AssemblyName: name,
            RelativeCsprojPath: $"{ApiProjectFolder(config)}/{name}.csproj",
            CsprojContent: csproj,
            IsWebProject: true,
            ReferencedAssemblies: ImmutableArray<string>.Empty));
    }

    public override string EntityPath(ApiSmithConfig c, string schema, string entityName) =>
        $"{ApiProjectFolder(c)}/Features/{Naming.Pluralizer.Pluralize(entityName)}/{entityName}.cs";

    public override string DtoPath(ApiSmithConfig c, string schema, string fileName)
    {
        var entity = fileName.EndsWith("Dtos", System.StringComparison.Ordinal) ? fileName[..^4] : fileName;
        return $"{ApiProjectFolder(c)}/Features/{Naming.Pluralizer.Pluralize(entity)}/{fileName}.cs";
    }

    public override string ValidatorPath(ApiSmithConfig c, string schema, string entityName) =>
        $"{ApiProjectFolder(c)}/Features/{Naming.Pluralizer.Pluralize(entityName)}/{entityName}DtoValidators.cs";

    public override string ValidationCorePath(ApiSmithConfig c) =>
        $"{ApiProjectFolder(c)}/Shared/ValidationResult.cs";

    public override string MapperPath(ApiSmithConfig c, string schema, string entityName) =>
        $"{ApiProjectFolder(c)}/Features/{Naming.Pluralizer.Pluralize(entityName)}/{entityName}Mappings.cs";

    public override string ControllerPath(ApiSmithConfig c, string collectionName) =>
        $"{ApiProjectFolder(c)}/Features/{collectionName}/{collectionName}Controller.cs";

    public override string MinimalApiEndpointPath(ApiSmithConfig c, string collectionName) =>
        $"{ApiProjectFolder(c)}/Features/{collectionName}/{collectionName}Endpoints.cs";

    public override string DbContextPath(ApiSmithConfig c) =>
        $"{ApiProjectFolder(c)}/Shared/{c.ProjectName}DbContext.cs";

    public override string RepositoryPath(ApiSmithConfig c, string entityName) =>
        $"{ApiProjectFolder(c)}/Features/{Naming.Pluralizer.Pluralize(entityName)}/{entityName}Repository.cs";

    public override string ConnectionFactoryPath(ApiSmithConfig c) =>
        $"{ApiProjectFolder(c)}/Shared/DbConnectionFactory.cs";

    public override string DispatcherPath(ApiSmithConfig c) =>
        $"{ApiProjectFolder(c)}/Shared/Dispatcher.cs";

    public override string EntityNamespace(ApiSmithConfig c, string schema)    => $"{c.ProjectName}.Features";
    public override string DtoNamespace(ApiSmithConfig c, string schema)       => $"{c.ProjectName}.Features";
    public override string ValidatorNamespace(ApiSmithConfig c, string schema) => $"{c.ProjectName}.Features";
    public override string MapperNamespace(ApiSmithConfig c, string schema)    => $"{c.ProjectName}.Features";
    public override string ValidatorCoreNamespace(ApiSmithConfig c)            => $"{c.ProjectName}.Features";
    public override string ControllerNamespace(ApiSmithConfig c) => $"{c.ProjectName}.Features";
    public override string EndpointNamespace(ApiSmithConfig c)   => $"{c.ProjectName}.Features";
    public override string DataNamespace(ApiSmithConfig c)       => $"{c.ProjectName}.Shared";
    public override string RepositoryNamespace(ApiSmithConfig c) => $"{c.ProjectName}.Features";
    public override string DispatcherNamespace(ApiSmithConfig c) => $"{c.ProjectName}.Shared";

    public override string LayoutDescription(ApiSmithConfig c) => $$"""
        ## Vertical Slice layout

        ```
        src/{{c.ProjectName}}/
        ├── Features/
        │   └── <Entity>/   # entity, DTOs, validator, mapper, controller or endpoints
        └── Shared/         # DbContext / connection factory / dispatcher / ValidationResult
        ```
        """;
}
