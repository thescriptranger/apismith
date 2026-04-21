using System.Collections.Immutable;
using ApiSmith.Config;

namespace ApiSmith.Generation.Architectures;

public sealed class OnionLayout : ArchitectureLayoutBase
{
    public override ArchitectureStyle Style => ArchitectureStyle.Onion;

    private static string Api(ApiSmithConfig c)            => $"{c.ProjectName}.Api";
    private static string Services(ApiSmithConfig c)       => $"{c.ProjectName}.Services";
    private static string Domain(ApiSmithConfig c)         => $"{c.ProjectName}.Domain";
    private static string Infrastructure(ApiSmithConfig c) => $"{c.ProjectName}.Infrastructure";

    public override string ApiProjectAssemblyName(ApiSmithConfig c) => Api(c);
    public override string ApiProjectFolder(ApiSmithConfig c)        => $"src/{Api(c)}";

    public override ImmutableArray<ProjectDefinition> Projects(ApiSmithConfig config)
    {
        var api = Api(config); var services = Services(config);
        var domain = Domain(config); var infra = Infrastructure(config);
        var isDapper = config.DataAccess is DataAccessStyle.Dapper;
        var isV2 = config.ApiVersion == ApiVersion.V2;
        var shared = SharedProjectAssemblyName(config);

        var apiRefs = CsprojTemplates.ProjectReferencesBlock(
            $"../{services}/{services}.csproj", $"../{infra}/{infra}.csproj");
        var servicesCsprojRefs = isV2
            ? CsprojTemplates.ProjectReferencesBlock(
                $"../{domain}/{domain}.csproj",
                $"../{shared}/{shared}.csproj")
            : CsprojTemplates.ProjectReferencesBlock($"../{domain}/{domain}.csproj");
        var infraRefs = CsprojTemplates.ProjectReferencesBlock($"../{domain}/{domain}.csproj");

        var servicesAssemblyRefs = isV2
            ? ImmutableArray.Create(domain, shared)
            : ImmutableArray.Create(domain);

        var builder = ImmutableArray.CreateBuilder<ProjectDefinition>();
        builder.Add(new ProjectDefinition(api, $"src/{api}/{api}.csproj",
            CsprojTemplates.WebProject(config, api, api, apiRefs),
            IsWebProject: true, ImmutableArray.Create(services, infra)));
        builder.Add(new ProjectDefinition(services, $"src/{services}/{services}.csproj",
            CsprojTemplates.ClassLibrary(config, services, services, servicesCsprojRefs),
            IsWebProject: false, servicesAssemblyRefs));
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
        $"src/{Services(c)}/Dtos{SchemaFolderSegment(c, schema)}/{fileName}.cs";
    public override string ValidatorPath(ApiSmithConfig c, string schema, string name)
    {
        var suffix = c.ApiVersion == ApiVersion.V2 ? "Validators" : "DtoValidators";
        return $"src/{Services(c)}/Validators{SchemaFolderSegment(c, schema)}/{name}{suffix}.cs";
    }
    public override string ValidationCorePath(ApiSmithConfig c)                         => $"src/{Services(c)}/Validators/ValidationResult.cs";
    public override string MapperPath(ApiSmithConfig c, string schema, string name)    => $"src/{Services(c)}/Mappings{SchemaFolderSegment(c, schema)}/{name}Mappings.cs";
    public override string DbContextPath(ApiSmithConfig c)               => $"src/{Infrastructure(c)}/Data/{c.ProjectName}DbContext.cs";
    public override string RepositoryPath(ApiSmithConfig c, string name) => $"src/{Infrastructure(c)}/Repositories/{name}Repository.cs";
    public override string ConnectionFactoryPath(ApiSmithConfig c)       => $"src/{Infrastructure(c)}/Data/DbConnectionFactory.cs";
    public override string DispatcherPath(ApiSmithConfig c)              => $"src/{Api(c)}/Shared/Dispatcher.cs";

    public override string EntityNamespace(ApiSmithConfig c, string schema)    => $"{Domain(c)}.Entities{SchemaNamespaceSegment(c, schema)}";
    public override string DtoNamespace(ApiSmithConfig c, string schema) =>
        $"{Services(c)}.Dtos{SchemaNamespaceSegment(c, schema)}";
    public override string ValidatorNamespace(ApiSmithConfig c, string schema) => $"{Services(c)}.Validators{SchemaNamespaceSegment(c, schema)}";
    public override string MapperNamespace(ApiSmithConfig c, string schema)    => $"{Services(c)}.Mappings{SchemaNamespaceSegment(c, schema)}";
    public override string ValidatorCoreNamespace(ApiSmithConfig c)            => $"{Services(c)}.Validators";
    public override string ControllerNamespace(ApiSmithConfig c) => $"{Api(c)}.Controllers";
    public override string DataNamespace(ApiSmithConfig c)       => $"{Infrastructure(c)}.Data";
    public override string RepositoryNamespace(ApiSmithConfig c) => $"{Infrastructure(c)}.Repositories";
    public override string DispatcherNamespace(ApiSmithConfig c) => $"{Api(c)}.Shared";

    public override string LayoutDescription(ApiSmithConfig c) => $$"""
        ## Onion layout

        ```
        src/
        ├── {{c.ProjectName}}.Api/
        ├── {{c.ProjectName}}.Services/
        ├── {{c.ProjectName}}.Domain/
        └── {{c.ProjectName}}.Infrastructure/
        ```
        """;
}
