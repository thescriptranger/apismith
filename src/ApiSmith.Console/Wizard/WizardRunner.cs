using System.Collections.Immutable;
using ApiSmith.Config;
using ApiSmith.Console.Prompts;

namespace ApiSmith.Console.Wizard;

/// <summary>Interactive wizard (PRD §4.2). Static prompts first, then connection, then schema multi-select.</summary>
public sealed class WizardRunner
{
    private readonly IConsoleIO _io;

    public WizardRunner(IConsoleIO io)
    {
        _io = io;
    }

    /// <summary>Everything except schemas; caller then introspects and feeds results into <see cref="SelectSchemas"/>.</summary>
    public ApiSmithConfig GatherStaticChoices()
    {
        _io.WriteLine(string.Empty);
        _io.WriteLine($"{Ansi.Bold}apismith — scaffold wizard{Ansi.Reset}");
        _io.WriteLine($"{Ansi.Dim}Answers are written to apismith.yaml so you can replay.{Ansi.Reset}");
        _io.WriteLine(string.Empty);

        var name = new TextPrompt
        {
            Label = "Project name",
            Default = "MyApi",
            Validate = v => string.IsNullOrWhiteSpace(v) ? "Project name is required." : null,
        }.Ask(_io);

        var outDir = new TextPrompt
        {
            Label = "Output directory",
            Default = $"./{name}",
        }.Ask(_io);

        var tfmOptions = InstalledSdkProbe.DetectInstalledTfms();
        var tfm = new SelectPrompt<string>
        {
            Label = "Target framework",
            Options = tfmOptions,
        }.Ask(_io);

        var endpoint = new SelectPrompt<EndpointStyle>
        {
            Label = "Endpoint style",
            Options = ImmutableArray.Create(EndpointStyle.Controllers, EndpointStyle.MinimalApi),
        }.Ask(_io);

        var architecture = new SelectPrompt<ArchitectureStyle>
        {
            Label = "Architecture",
            Options = ImmutableArray.Create(
                ArchitectureStyle.Flat,
                ArchitectureStyle.Clean,
                ArchitectureStyle.VerticalSlice,
                ArchitectureStyle.Layered,
                ArchitectureStyle.Onion),
        }.Ask(_io);

        var dataAccess = new SelectPrompt<DataAccessStyle>
        {
            Label = "Data access",
            Options = ImmutableArray.Create(DataAccessStyle.EfCore, DataAccessStyle.Dapper),
        }.Ask(_io);

        var emitRepoIfaces = false;
        if (dataAccess == DataAccessStyle.Dapper)
        {
            emitRepoIfaces = new ConfirmPrompt
            {
                Label = "Emit I<Entity>Repository interfaces for DI/testability?",
                Default = false,
            }.Ask(_io);
        }

        var initialMigration = new ConfirmPrompt
        {
            Label = "Generate initial EF Core migration from the existing DB?",
            Default = false,
        }.Ask(_io);

        var crud = SelectCrud();

        var versioning = new SelectPrompt<VersioningStyle>
        {
            Label = "API versioning",
            Options = ImmutableArray.Create(
                VersioningStyle.None,
                VersioningStyle.UrlSegment,
                VersioningStyle.Header,
                VersioningStyle.QueryString),
            Describe = v => v switch
            {
                VersioningStyle.None        => "None",
                VersioningStyle.UrlSegment  => "URL segment (/api/v1/...)",
                VersioningStyle.Header      => "Header",
                VersioningStyle.QueryString => "Query string",
                _                            => v.ToString(),
            },
        }.Ask(_io);

        var auth = new SelectPrompt<AuthStyle>
        {
            Label = "Authentication",
            Options = ImmutableArray.Create(
                AuthStyle.None,
                AuthStyle.JwtBearer,
                AuthStyle.Auth0,
                AuthStyle.AzureAd,
                AuthStyle.ApiKey),
            Describe = a => a switch
            {
                AuthStyle.JwtBearer => "JWT bearer stub",
                AuthStyle.AzureAd   => "Azure AD",
                _                   => a.ToString(),
            },
        }.Ask(_io);

        var tests  = new ConfirmPrompt { Label = "Include tests project?",  Default = true  }.Ask(_io);
        var docker = new ConfirmPrompt { Label = "Include Docker assets?", Default = true  }.Ask(_io);

        var partitionSprocs = new ConfirmPrompt
        {
            Label = "Partition stored-procedure interfaces by schema?",
            Default = false,
        }.Ask(_io);

        var includeChildCollections = new ConfirmPrompt
        {
            Label = "Include one-to-many child collections in response payloads? (depth = 1)",
            Default = false,
        }.Ask(_io);

        var connection = new TextPrompt
        {
            Label = "SQL Server connection string",
            Validate = v => string.IsNullOrWhiteSpace(v) ? "A connection string is required." : null,
        }.Ask(_io);

        return new ApiSmithConfig
        {
            ApiVersion = ApiVersion.V2,
            ProjectName = name,
            OutputDirectory = outDir,
            TargetFramework = tfm,
            EndpointStyle = endpoint,
            Architecture = architecture,
            DataAccess = dataAccess,
            EmitRepositoryInterfaces = emitRepoIfaces,
            GenerateInitialMigration = initialMigration,
            Crud = crud,
            Versioning = versioning,
            Auth = auth,
            IncludeTestsProject = tests,
            IncludeDockerAssets = docker,
            PartitionStoredProceduresBySchema = partitionSprocs,
            IncludeChildCollectionsInResponses = includeChildCollections,
            ConnectionString = connection,
        };
    }

    public ImmutableArray<string> SelectSchemas(ImmutableArray<string> discovered)
    {
        if (discovered.Length == 0)
        {
            _io.WriteLine($"{Ansi.Yellow}No non-system schemas were found in the target database.{Ansi.Reset}");
            return ImmutableArray<string>.Empty;
        }

        return new MultiSelectPrompt<string>
        {
            Label = "Schemas to include",
            Options = discovered,
            DefaultSelection = System.Linq.Enumerable.Range(0, discovered.Length).ToImmutableArray(),
        }.Ask(_io);
    }

    private CrudOperations SelectCrud()
    {
        var ops = ImmutableArray.Create(
            CrudOperations.GetList,
            CrudOperations.GetById,
            CrudOperations.Post,
            CrudOperations.Put,
            CrudOperations.Patch,
            CrudOperations.Delete);

        var picks = new MultiSelectPrompt<CrudOperations>
        {
            Label = "CRUD operations per entity",
            Options = ops,
            DefaultSelection = System.Linq.Enumerable.Range(0, ops.Length).ToImmutableArray(),
            Describe = op => op switch
            {
                CrudOperations.GetList => "GET /resource (list)",
                CrudOperations.GetById => "GET /resource/{id}",
                CrudOperations.Post    => "POST /resource",
                CrudOperations.Put     => "PUT /resource/{id}",
                CrudOperations.Patch   => "PATCH /resource/{id}",
                CrudOperations.Delete  => "DELETE /resource/{id}",
                _                       => op.ToString(),
            },
        }.Ask(_io);

        var combined = CrudOperations.None;
        foreach (var op in picks)
        {
            combined |= op;
        }

        return combined == CrudOperations.None ? CrudOperations.All : combined;
    }
}
