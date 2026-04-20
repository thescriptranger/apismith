using System.Collections.Immutable;
using ApiSmith.Config;

namespace ApiSmith.Generation.Architectures;

public sealed class FlatLayout : ArchitectureLayoutBase
{
    public override ArchitectureStyle Style => ArchitectureStyle.Flat;

    public override string ApiProjectAssemblyName(ApiSmithConfig c) => c.ProjectName;
    public override string ApiProjectFolder(ApiSmithConfig c)        => $"src/{c.ProjectName}";

    public override ImmutableArray<ProjectDefinition> Projects(ApiSmithConfig config)
    {
        var name = ApiProjectAssemblyName(config);
        var sharedAsm = SharedProjectAssemblyName(config);
        var projectRefsBlock = config.ApiVersion == ApiVersion.V2
            ? CsprojTemplates.ProjectReferencesBlock($"../{sharedAsm}/{sharedAsm}.csproj")
            : string.Empty;
        var csproj = config.DataAccess is DataAccessStyle.EfCore
            ? CsprojTemplates.WebProjectWithEfCore(config, name, name, projectRefsBlock)
            : CsprojTemplates.WebProjectWithDapper(config, name, name, projectRefsBlock);

        var apiRefs = config.ApiVersion == ApiVersion.V2
            ? ImmutableArray.Create(sharedAsm)
            : ImmutableArray<string>.Empty;

        var builder = ImmutableArray.CreateBuilder<ProjectDefinition>();
        builder.Add(new ProjectDefinition(
            AssemblyName: name,
            RelativeCsprojPath: $"{ApiProjectFolder(config)}/{name}.csproj",
            CsprojContent: csproj,
            IsWebProject: true,
            ReferencedAssemblies: apiRefs));
        if (config.ApiVersion == ApiVersion.V2)
        {
            builder.Add(SharedProject(config));
        }
        return builder.ToImmutable();
    }

    public override string EntityPath(ApiSmithConfig c, string schema, string name)    => $"{ApiProjectFolder(c)}/Entities{SchemaFolderSegment(c, schema)}/{name}.cs";
    public override string DtoPath(ApiSmithConfig c, string schema, string fileName) =>
        c.ApiVersion == ApiVersion.V2
            ? $"{SharedProjectFolder(c)}/Dtos{SchemaFolderSegment(c, schema)}/{fileName}.cs"
            : $"{ApiProjectFolder(c)}/Dtos{SchemaFolderSegment(c, schema)}/{fileName}.cs";
    public override string ValidatorPath(ApiSmithConfig c, string schema, string name) => $"{ApiProjectFolder(c)}/Validators{SchemaFolderSegment(c, schema)}/{name}DtoValidators.cs";
    public override string ValidationCorePath(ApiSmithConfig c)                         => $"{ApiProjectFolder(c)}/Validators/ValidationResult.cs";
    public override string MapperPath(ApiSmithConfig c, string schema, string name)    => $"{ApiProjectFolder(c)}/Mappings{SchemaFolderSegment(c, schema)}/{name}Mappings.cs";
    public override string DbContextPath(ApiSmithConfig c)                => $"{ApiProjectFolder(c)}/Data/{c.ProjectName}DbContext.cs";
    public override string RepositoryPath(ApiSmithConfig c, string name)  => $"{ApiProjectFolder(c)}/Data/{name}Repository.cs";
    public override string ConnectionFactoryPath(ApiSmithConfig c)        => $"{ApiProjectFolder(c)}/Data/DbConnectionFactory.cs";
    public override string DispatcherPath(ApiSmithConfig c)               => $"{ApiProjectFolder(c)}/Shared/Dispatcher.cs";

    public override string EntityNamespace(ApiSmithConfig c, string schema)    => $"{c.ProjectName}.Entities{SchemaNamespaceSegment(c, schema)}";
    public override string DtoNamespace(ApiSmithConfig c, string schema) =>
        c.ApiVersion == ApiVersion.V2
            ? $"{SharedNamespace(c)}.Dtos{SchemaNamespaceSegment(c, schema)}"
            : $"{c.ProjectName}.Dtos{SchemaNamespaceSegment(c, schema)}";
    public override string ValidatorNamespace(ApiSmithConfig c, string schema) => $"{c.ProjectName}.Validators{SchemaNamespaceSegment(c, schema)}";
    public override string MapperNamespace(ApiSmithConfig c, string schema)    => $"{c.ProjectName}.Mappings{SchemaNamespaceSegment(c, schema)}";
    public override string ValidatorCoreNamespace(ApiSmithConfig c)            => $"{c.ProjectName}.Validators";
    public override string ControllerNamespace(ApiSmithConfig c) => $"{c.ProjectName}.Controllers";
    public override string DataNamespace(ApiSmithConfig c)       => $"{c.ProjectName}.Data";
    public override string RepositoryNamespace(ApiSmithConfig c) => $"{c.ProjectName}.Data";
    public override string DispatcherNamespace(ApiSmithConfig c) => $"{c.ProjectName}.Shared";

    public override string LayoutDescription(ApiSmithConfig config) => $$"""
        ## Flat layout

        ```
        src/{{config.ProjectName}}/
        ├── Controllers/ OR Endpoints/
        ├── Data/        # DbContext or repositories
        ├── Dtos/
        ├── Entities/
        ├── Mappings/
        └── Validators/
        ```
        """;
}
