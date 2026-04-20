using System.Collections.Immutable;
using ApiSmith.Config;
using ApiSmith.Naming;

namespace ApiSmith.Generation.Architectures;

/// <summary>Defaults for paths that don't vary across architectures; concrete layouts override the rest.</summary>
public abstract class ArchitectureLayoutBase : IArchitectureLayout
{
    public abstract ArchitectureStyle Style { get; }

    public abstract ImmutableArray<ProjectDefinition> Projects(ApiSmithConfig config);
    public abstract string ApiProjectAssemblyName(ApiSmithConfig config);
    public abstract string ApiProjectFolder(ApiSmithConfig config);

    public string SolutionPath(ApiSmithConfig config) => $"{config.ProjectName}.sln";

    public string ProgramPath(ApiSmithConfig c) => $"{ApiProjectFolder(c)}/Program.cs";
    public string AppSettingsPath(ApiSmithConfig c) => $"{ApiProjectFolder(c)}/appsettings.json";
    public string AppSettingsDevPath(ApiSmithConfig c) => $"{ApiProjectFolder(c)}/appsettings.Development.json";
    public virtual string ControllerPath(ApiSmithConfig c, string collectionName) => $"{ApiProjectFolder(c)}/Controllers/{collectionName}Controller.cs";
    public virtual string MinimalApiEndpointPath(ApiSmithConfig c, string collectionName) => $"{ApiProjectFolder(c)}/Endpoints/{collectionName}Endpoints.cs";

    public abstract string EntityPath(ApiSmithConfig config, string schema, string entityName);
    public abstract string DtoPath(ApiSmithConfig config, string schema, string fileName);
    public abstract string ValidatorPath(ApiSmithConfig config, string schema, string entityName);
    public abstract string ValidationCorePath(ApiSmithConfig config);
    public abstract string MapperPath(ApiSmithConfig config, string schema, string entityName);

    /// <summary>Segment schema folders/namespaces except for the single-schema <c>dbo</c> fallback.</summary>
    protected bool EmitSchemaSegment(ApiSmithConfig config, string schema) =>
        config.Schemas.Count > 1 ||
        !string.Equals(schema, "dbo", System.StringComparison.OrdinalIgnoreCase);

    protected string SchemaFolderSegment(ApiSmithConfig config, string schema) =>
        EmitSchemaSegment(config, schema) ? "/" + SchemaSegment.ToPascal(schema) : string.Empty;

    protected string SchemaNamespaceSegment(ApiSmithConfig config, string schema) =>
        EmitSchemaSegment(config, schema) ? "." + SchemaSegment.ToPascal(schema) : string.Empty;
    public abstract string DbContextPath(ApiSmithConfig config);
    public abstract string RepositoryPath(ApiSmithConfig config, string entityName);
    public abstract string ConnectionFactoryPath(ApiSmithConfig config);
    public abstract string DispatcherPath(ApiSmithConfig config);

    public string GitignorePath()       => ".gitignore";
    public string EditorconfigPath()    => ".editorconfig";
    public string ReadmePath()          => "README.md";
    public string ApismithConfigPath()  => "apismith.yaml";
    public string DockerfilePath()      => "Dockerfile";
    public string DockerComposePath()   => "docker-compose.yml";

    public string TestsProjectFolder(ApiSmithConfig c) => $"tests/{c.ProjectName}.IntegrationTests";
    public string TestsProjectFile(ApiSmithConfig c) => $"{TestsProjectFolder(c)}/{c.ProjectName}.IntegrationTests.csproj";
    public string TestsEndpointPath(ApiSmithConfig c, string collectionName) =>
        $"{TestsProjectFolder(c)}/Endpoints/{collectionName}EndpointTests.cs";
    public string TestsValidatorPath(ApiSmithConfig c, string entityName) =>
        $"{TestsProjectFolder(c)}/Validators/{entityName}ValidatorTests.cs";

    public virtual ProjectDefinition TestsProject(ApiSmithConfig config)
    {
        var testsName = $"{config.ProjectName}.IntegrationTests";
        var apiProject = ApiProjectAssemblyName(config);
        var apiCsprojRelative = $"../../src/{apiProject}/{apiProject}.csproj";

        var csproj = $"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <TargetFramework>{config.TargetFramework}</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <LangVersion>latest</LangVersion>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                <WarningsAsErrors />
                <NoWarn>$(NoWarn);CS1591</NoWarn>
                <IsPackable>false</IsPackable>
                <RootNamespace>{testsName}</RootNamespace>
                <AssemblyName>{testsName}</AssemblyName>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
                <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="{CsprojTemplates.AspNetCoreOpenApiVersion}" />
                <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="{CsprojTemplates.EfCoreVersion}" />
                <PackageReference Include="xunit" Version="2.9.2" />
                <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
              </ItemGroup>

              <ItemGroup>
                <Using Include="Xunit" />
              </ItemGroup>

              <ItemGroup>
                <ProjectReference Include="{apiCsprojRelative}" />
              </ItemGroup>

            </Project>
            """;

        return new ProjectDefinition(
            AssemblyName: testsName,
            RelativeCsprojPath: TestsProjectFile(config),
            CsprojContent: csproj,
            IsWebProject: false,
            ReferencedAssemblies: ImmutableArray.Create(apiProject));
    }

    public abstract string EntityNamespace(ApiSmithConfig config, string schema);
    public abstract string DtoNamespace(ApiSmithConfig config, string schema);
    public abstract string ValidatorNamespace(ApiSmithConfig config, string schema);
    public abstract string MapperNamespace(ApiSmithConfig config, string schema);
    public abstract string ValidatorCoreNamespace(ApiSmithConfig config);
    public abstract string ControllerNamespace(ApiSmithConfig config);
    public virtual string EndpointNamespace(ApiSmithConfig c) => $"{ApiProjectAssemblyName(c)}.Endpoints";
    public abstract string DataNamespace(ApiSmithConfig config);
    public abstract string RepositoryNamespace(ApiSmithConfig config);
    public abstract string DispatcherNamespace(ApiSmithConfig config);
    public string ApiNamespace(ApiSmithConfig c) => ApiProjectAssemblyName(c);
    public string TestsNamespace(ApiSmithConfig c) => $"{c.ProjectName}.IntegrationTests";

    public abstract string LayoutDescription(ApiSmithConfig config);

    public virtual string SharedProjectAssemblyName(ApiSmithConfig c) => $"{c.ProjectName}.Shared";
    public virtual string SharedProjectFolder(ApiSmithConfig c) => $"src/{c.ProjectName}.Shared";
    public virtual string SharedNamespace(ApiSmithConfig c) => $"{c.ProjectName}.Shared";
    public virtual string SharedErrorsNamespace(ApiSmithConfig c) => $"{c.ProjectName}.Shared.Errors";

    public virtual ProjectDefinition SharedProject(ApiSmithConfig config)
    {
        var name = SharedProjectAssemblyName(config);
        var csproj = CsprojTemplates.SharedClassLibrary(config);
        return new ProjectDefinition(
            AssemblyName: name,
            RelativeCsprojPath: $"{SharedProjectFolder(config)}/{name}.csproj",
            CsprojContent: csproj,
            IsWebProject: false,
            ReferencedAssemblies: ImmutableArray<string>.Empty);
    }
}
