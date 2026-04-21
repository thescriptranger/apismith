using System.Collections.Immutable;
using ApiSmith.Config;

namespace ApiSmith.Generation.Architectures;

public sealed class CleanLayout : ArchitectureLayoutBase
{
    public override ArchitectureStyle Style => ArchitectureStyle.Clean;

    private static string Api(ApiSmithConfig c)            => $"{c.ProjectName}.Api";
    private static string Application(ApiSmithConfig c)    => $"{c.ProjectName}.Application";
    private static string Domain(ApiSmithConfig c)         => $"{c.ProjectName}.Domain";
    private static string Infrastructure(ApiSmithConfig c) => $"{c.ProjectName}.Infrastructure";

    public override string ApiProjectAssemblyName(ApiSmithConfig c) => Api(c);
    public override string ApiProjectFolder(ApiSmithConfig c)        => $"src/{Api(c)}";

    public override ImmutableArray<ProjectDefinition> Projects(ApiSmithConfig config)
    {
        var api = Api(config); var app = Application(config);
        var domain = Domain(config); var infra = Infrastructure(config);
        var isDapper = config.DataAccess is DataAccessStyle.Dapper;
        var isV2 = config.ApiVersion == ApiVersion.V2;
        var shared = SharedProjectAssemblyName(config);

        var appCsprojRefs = isV2
            ? CsprojTemplates.ProjectReferencesBlock(
                $"../{domain}/{domain}.csproj",
                $"../{shared}/{shared}.csproj")
            : CsprojTemplates.ProjectReferencesBlock($"../{domain}/{domain}.csproj");

        var apiRefs = CsprojTemplates.ProjectReferencesBlock(
            $"../{app}/{app}.csproj",
            $"../{infra}/{infra}.csproj");
        var infraRefs = CsprojTemplates.ProjectReferencesBlock($"../{domain}/{domain}.csproj");

        var appAssemblyRefs = isV2
            ? ImmutableArray.Create(domain, shared)
            : ImmutableArray.Create(domain);

        var builder = ImmutableArray.CreateBuilder<ProjectDefinition>();
        builder.Add(new ProjectDefinition(api, $"src/{api}/{api}.csproj",
            CsprojTemplates.WebProject(config, api, api, apiRefs),
            IsWebProject: true, ImmutableArray.Create(app, infra)));
        builder.Add(new ProjectDefinition(app, $"src/{app}/{app}.csproj",
            CsprojTemplates.ClassLibrary(config, app, app, appCsprojRefs),
            IsWebProject: false, appAssemblyRefs));
        builder.Add(new ProjectDefinition(domain, $"src/{domain}/{domain}.csproj",
            CsprojTemplates.ClassLibrary(config, domain, domain, string.Empty),
            IsWebProject: false, ImmutableArray<string>.Empty));
        builder.Add(new ProjectDefinition(infra, $"src/{infra}/{infra}.csproj",
            CsprojTemplates.ClassLibrary(config, infra, infra, infraRefs, withEfCore: !isDapper, withDapper: isDapper),
            IsWebProject: false, ImmutableArray.Create(domain)));
        if (isV2)
        {
            builder.Add(SharedProject(config));
        }
        return builder.ToImmutable();
    }

    public override string EntityPath(ApiSmithConfig c, string schema, string name)    => $"src/{Domain(c)}/Entities{SchemaFolderSegment(c, schema)}/{name}.cs";
    public override string DtoPath(ApiSmithConfig c, string schema, string fileName) =>
        $"src/{Application(c)}/Dtos{SchemaFolderSegment(c, schema)}/{fileName}.cs";
    public override string ValidatorPath(ApiSmithConfig c, string schema, string name)
    {
        var suffix = c.ApiVersion == ApiVersion.V2 ? "Validators" : "DtoValidators";
        return $"src/{Application(c)}/Validators{SchemaFolderSegment(c, schema)}/{name}{suffix}.cs";
    }
    public override string ValidationCorePath(ApiSmithConfig c)                         => $"src/{Application(c)}/Validators/ValidationResult.cs";
    public override string MapperPath(ApiSmithConfig c, string schema, string name)    => $"src/{Application(c)}/Mappings{SchemaFolderSegment(c, schema)}/{name}Mappings.cs";
    public override string DbContextPath(ApiSmithConfig c)               => $"src/{Infrastructure(c)}/Data/{c.ProjectName}DbContext.cs";
    public override string RepositoryPath(ApiSmithConfig c, string name) => $"src/{Infrastructure(c)}/Repositories/{name}Repository.cs";
    public override string ConnectionFactoryPath(ApiSmithConfig c)       => $"src/{Infrastructure(c)}/Data/DbConnectionFactory.cs";
    public override string DispatcherPath(ApiSmithConfig c)              => $"src/{Api(c)}/Shared/Dispatcher.cs";

    public override string EntityNamespace(ApiSmithConfig c, string schema)    => $"{Domain(c)}.Entities{SchemaNamespaceSegment(c, schema)}";
    public override string DtoNamespace(ApiSmithConfig c, string schema) =>
        $"{Application(c)}.Dtos{SchemaNamespaceSegment(c, schema)}";
    public override string ValidatorNamespace(ApiSmithConfig c, string schema) => $"{Application(c)}.Validators{SchemaNamespaceSegment(c, schema)}";
    public override string MapperNamespace(ApiSmithConfig c, string schema)    => $"{Application(c)}.Mappings{SchemaNamespaceSegment(c, schema)}";
    public override string ValidatorCoreNamespace(ApiSmithConfig c)            => $"{Application(c)}.Validators";
    public override string ControllerNamespace(ApiSmithConfig c) => $"{Api(c)}.Controllers";
    public override string DataNamespace(ApiSmithConfig c)       => $"{Infrastructure(c)}.Data";
    public override string RepositoryNamespace(ApiSmithConfig c) => $"{Infrastructure(c)}.Repositories";
    public override string DispatcherNamespace(ApiSmithConfig c) => $"{Api(c)}.Shared";

    public override string LayoutDescription(ApiSmithConfig c) => $$"""
        ## Clean Architecture layout

        ```
        src/
        ├── {{c.ProjectName}}.Api/              # endpoints, DI, middleware
        ├── {{c.ProjectName}}.Application/      # DTOs, validators, mappers
        ├── {{c.ProjectName}}.Domain/           # entities
        └── {{c.ProjectName}}.Infrastructure/   # DbContext / repositories
        ```
        """;
}
