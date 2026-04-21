namespace ApiSmith.Config;

public enum EndpointStyle { Controllers, MinimalApi }

public enum ArchitectureStyle { Flat, Clean, VerticalSlice, Layered, Onion }

public enum DataAccessStyle { EfCore, Dapper }

public enum AuthStyle { None, JwtBearer, Auth0, AzureAd, ApiKey }

public enum VersioningStyle { None, UrlSegment, Header, QueryString }

[System.Flags]
public enum CrudOperations
{
    None    = 0,
    GetList = 1 << 0,
    GetById = 1 << 1,
    Post    = 1 << 2,
    Put     = 1 << 3,
    Patch   = 1 << 4,
    Delete  = 1 << 5,
    All     = GetList | GetById | Post | Put | Patch | Delete,
}

public sealed class ApiSmithConfig
{
    /// <summary>sln, csproj, root namespace.</summary>
    public string ProjectName { get; set; } = "MyApi";

    /// <summary>Schema version of apismith.yaml. Missing field defaults to V1.</summary>
    public ApiVersion ApiVersion { get; set; } = ApiVersion.V1;

    /// <summary>Relative paths resolve against cwd.</summary>
    public string OutputDirectory { get; set; } = "./MyApi";

    /// <summary>TFM, e.g. <c>net9.0</c>.</summary>
    public string TargetFramework { get; set; } = "net9.0";

    /// <summary>Used at scaffold time AND emitted as DefaultConnection.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    public EndpointStyle EndpointStyle { get; set; } = EndpointStyle.Controllers;

    public ArchitectureStyle Architecture { get; set; } = ArchitectureStyle.Flat;

    public DataAccessStyle DataAccess { get; set; } = DataAccessStyle.EfCore;

    public bool GenerateInitialMigration { get; set; } = false;

    public CrudOperations Crud { get; set; } = CrudOperations.All;

    public VersioningStyle Versioning { get; set; } = VersioningStyle.None;

    public AuthStyle Auth { get; set; } = AuthStyle.None;

    public bool IncludeTestsProject { get; set; } = false;

    public bool IncludeDockerAssets { get; set; } = false;

    /// <summary>Empty = all non-system schemas.</summary>
    public List<string> Schemas { get; set; } = new();

    /// <summary>Opt-in: emit FK-not-default checks in DTO validators with a TODO stub for existence verification.</summary>
    public bool ValidateForeignKeyReferences { get; set; } = false;

    /// <summary>When true (and DataAccess=Dapper), emits an I&lt;Entity&gt;Repository interface per entity and binds it in DI.</summary>
    public bool EmitRepositoryInterfaces { get; set; } = false;

    /// <summary>When true, emits I&lt;Schema&gt;StoredProcedures / I&lt;Schema&gt;DbFunctions per schema instead of one fat interface.</summary>
    public bool PartitionStoredProceduresBySchema { get; set; } = false;

    /// <summary>When true, GET read endpoints and their DTOs include one-to-many child collections (depth = 1).</summary>
    public bool IncludeChildCollectionsInResponses { get; set; } = false;
}
