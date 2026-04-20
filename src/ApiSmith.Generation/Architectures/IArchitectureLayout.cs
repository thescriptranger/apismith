using System.Collections.Immutable;
using ApiSmith.Config;

namespace ApiSmith.Generation.Architectures;

public interface IArchitectureLayout
{
    ArchitectureStyle Style { get; }

    ImmutableArray<ProjectDefinition> Projects(ApiSmithConfig config);

    // Solution / host project
    string SolutionPath(ApiSmithConfig config);
    string ProgramPath(ApiSmithConfig config);
    string AppSettingsPath(ApiSmithConfig config);
    string AppSettingsDevPath(ApiSmithConfig config);

    // Per-entity files
    string EntityPath(ApiSmithConfig config, string schema, string entityName);
    string DtoPath(ApiSmithConfig config, string schema, string fileName);
    string ValidatorPath(ApiSmithConfig config, string schema, string entityName);
    string ValidationCorePath(ApiSmithConfig config);
    string MapperPath(ApiSmithConfig config, string schema, string entityName);

    // Endpoints: Controllers xor Minimal API.
    string ControllerPath(ApiSmithConfig config, string collectionName);
    string MinimalApiEndpointPath(ApiSmithConfig config, string collectionName);

    // Data access: EF Core xor Dapper.
    string DbContextPath(ApiSmithConfig config);
    string RepositoryPath(ApiSmithConfig config, string entityName);
    string ConnectionFactoryPath(ApiSmithConfig config);

    string DispatcherPath(ApiSmithConfig config);

    // Repo-root
    string GitignorePath();
    string EditorconfigPath();
    string ReadmePath();
    string ApismithConfigPath();
    string DockerfilePath();
    string DockerComposePath();

    // Tests
    string TestsProjectFolder(ApiSmithConfig config);
    string TestsProjectFile(ApiSmithConfig config);
    string TestsEndpointPath(ApiSmithConfig config, string collectionName);
    string TestsValidatorPath(ApiSmithConfig config, string entityName);
    ProjectDefinition TestsProject(ApiSmithConfig config);

    // Namespaces
    string EntityNamespace(ApiSmithConfig config, string schema);
    string DtoNamespace(ApiSmithConfig config, string schema);
    string ValidatorNamespace(ApiSmithConfig config, string schema);
    string MapperNamespace(ApiSmithConfig config, string schema);

    /// <summary>Root namespace of the shared <c>ValidationResult</c> — unsegmented, matches <see cref="ValidationCorePath"/>.</summary>
    string ValidatorCoreNamespace(ApiSmithConfig config);
    string ControllerNamespace(ApiSmithConfig config);
    string EndpointNamespace(ApiSmithConfig config);
    string DataNamespace(ApiSmithConfig config);
    string RepositoryNamespace(ApiSmithConfig config);
    string DispatcherNamespace(ApiSmithConfig config);
    string ApiNamespace(ApiSmithConfig config);
    string TestsNamespace(ApiSmithConfig config);

    string LayoutDescription(ApiSmithConfig config);

    /// <summary>API host assembly name (tests ref, Docker COPY, etc.).</summary>
    string ApiProjectAssemblyName(ApiSmithConfig config);

    /// <summary>API host folder, e.g. <c>src/MyApi.Api</c>.</summary>
    string ApiProjectFolder(ApiSmithConfig config);
}
