using System.Collections.Immutable;
using ApiSmith.Config;

namespace ApiSmith.Generation.Architectures;

public sealed class LayeredLayout : ArchitectureLayoutBase
{
    public override ArchitectureStyle Style => ArchitectureStyle.Layered;

    private static string Api(ApiSmithConfig c)           => $"{c.ProjectName}.Api";
    private static string BusinessLogic(ApiSmithConfig c) => $"{c.ProjectName}.BusinessLogic";
    private static string DataAccess(ApiSmithConfig c)    => $"{c.ProjectName}.DataAccess";

    public override string ApiProjectAssemblyName(ApiSmithConfig c) => Api(c);
    public override string ApiProjectFolder(ApiSmithConfig c)        => $"src/{Api(c)}";

    public override ImmutableArray<ProjectDefinition> Projects(ApiSmithConfig config)
    {
        var api = Api(config); var bl = BusinessLogic(config); var da = DataAccess(config);
        var isDapper = config.DataAccess is DataAccessStyle.Dapper;

        var apiRefs = CsprojTemplates.ProjectReferencesBlock(
            $"../{bl}/{bl}.csproj", $"../{da}/{da}.csproj");
        var blRefs = CsprojTemplates.ProjectReferencesBlock($"../{da}/{da}.csproj");

        return ImmutableArray.Create(
            new ProjectDefinition(api, $"src/{api}/{api}.csproj",
                CsprojTemplates.WebProject(config, api, api, apiRefs),
                IsWebProject: true, ImmutableArray.Create(bl, da)),
            new ProjectDefinition(bl, $"src/{bl}/{bl}.csproj",
                CsprojTemplates.ClassLibrary(config, bl, bl, blRefs),
                IsWebProject: false, ImmutableArray.Create(da)),
            new ProjectDefinition(da, $"src/{da}/{da}.csproj",
                CsprojTemplates.ClassLibrary(config, da, da, string.Empty, withEfCore: !isDapper, withDapper: isDapper),
                IsWebProject: false, ImmutableArray<string>.Empty));
    }

    public override string EntityPath(ApiSmithConfig c, string schema, string name)    => $"src/{DataAccess(c)}/Entities{SchemaFolderSegment(c, schema)}/{name}.cs";
    public override string DbContextPath(ApiSmithConfig c)                              => $"src/{DataAccess(c)}/{c.ProjectName}DbContext.cs";
    public override string RepositoryPath(ApiSmithConfig c, string name)                => $"src/{DataAccess(c)}/Repositories/{name}Repository.cs";
    public override string ConnectionFactoryPath(ApiSmithConfig c)                      => $"src/{DataAccess(c)}/DbConnectionFactory.cs";
    public override string DispatcherPath(ApiSmithConfig c)                             => $"src/{Api(c)}/Shared/Dispatcher.cs";

    public override string DtoPath(ApiSmithConfig c, string schema, string fileName)   => $"src/{BusinessLogic(c)}/Dtos{SchemaFolderSegment(c, schema)}/{fileName}.cs";
    public override string ValidatorPath(ApiSmithConfig c, string schema, string name) => $"src/{BusinessLogic(c)}/Validators{SchemaFolderSegment(c, schema)}/{name}DtoValidators.cs";
    public override string ValidationCorePath(ApiSmithConfig c)                         => $"src/{BusinessLogic(c)}/Validators/ValidationResult.cs";
    public override string MapperPath(ApiSmithConfig c, string schema, string name)    => $"src/{BusinessLogic(c)}/Mappings{SchemaFolderSegment(c, schema)}/{name}Mappings.cs";

    public override string EntityNamespace(ApiSmithConfig c, string schema)    => $"{DataAccess(c)}.Entities{SchemaNamespaceSegment(c, schema)}";
    public override string DtoNamespace(ApiSmithConfig c, string schema)       => $"{BusinessLogic(c)}.Dtos{SchemaNamespaceSegment(c, schema)}";
    public override string ValidatorNamespace(ApiSmithConfig c, string schema) => $"{BusinessLogic(c)}.Validators{SchemaNamespaceSegment(c, schema)}";
    public override string MapperNamespace(ApiSmithConfig c, string schema)    => $"{BusinessLogic(c)}.Mappings{SchemaNamespaceSegment(c, schema)}";
    public override string ValidatorCoreNamespace(ApiSmithConfig c)            => $"{BusinessLogic(c)}.Validators";
    public override string ControllerNamespace(ApiSmithConfig c) => $"{Api(c)}.Controllers";
    public override string DataNamespace(ApiSmithConfig c)       => DataAccess(c);
    public override string RepositoryNamespace(ApiSmithConfig c) => $"{DataAccess(c)}.Repositories";
    public override string DispatcherNamespace(ApiSmithConfig c) => $"{Api(c)}.Shared";

    public override string LayoutDescription(ApiSmithConfig c) => $$"""
        ## Layered (N-tier) layout

        ```
        src/
        ├── {{c.ProjectName}}.Api/
        ├── {{c.ProjectName}}.BusinessLogic/
        └── {{c.ProjectName}}.DataAccess/
        ```
        """;
}
