using ApiSmith.Config;
using ApiSmith.Console.Wizard;

namespace ApiSmith.UnitTests.Console;

public sealed class WizardRunnerTests
{
    [Fact]
    public void Gathers_static_choices_with_scripted_input()
    {
        var io = new FakeConsoleIO(
            "Bookings",                          // project name
            "./out",                              // output dir
            "",                                   // tfm → default net9.0
            "2",                                  // endpoint style: MinimalApi
            "2",                                  // architecture: Clean
            "",                                   // data access: default EfCore
            "n",                                  // generate initial migration?
            "",                                   // crud: blank → defaults (all)
            "2",                                  // versioning: UrlSegment
            "2",                                  // auth: JwtBearer
            "y",                                  // include tests
            "n",                                  // include Docker
            "n",                                   // partition sproc interfaces by schema
            "n",                                   // include child collections in responses
            "Server=(local);Database=X;Trusted_Connection=True;");

        var config = new WizardRunner(io).GatherStaticChoices();

        Assert.Equal("Bookings", config.ProjectName);
        Assert.Equal("./out", config.OutputDirectory);
        Assert.Equal("net9.0", config.TargetFramework);
        Assert.Equal(EndpointStyle.MinimalApi, config.EndpointStyle);
        Assert.Equal(ArchitectureStyle.Clean, config.Architecture);
        Assert.Equal(DataAccessStyle.EfCore, config.DataAccess);
        Assert.False(config.GenerateInitialMigration);
        Assert.Equal(CrudOperations.All, config.Crud);
        Assert.Equal(VersioningStyle.UrlSegment, config.Versioning);
        Assert.Equal(AuthStyle.JwtBearer, config.Auth);
        Assert.True(config.IncludeTestsProject);
        Assert.False(config.IncludeDockerAssets);
        Assert.StartsWith("Server=", config.ConnectionString);
    }

    [Fact]
    public void Wizard_sets_apiVersion_to_v2()
    {
        var io = new FakeConsoleIO(
            "Bookings",                          // project name
            "./out",                              // output dir
            "",                                   // tfm → default net9.0
            "2",                                  // endpoint style: MinimalApi
            "2",                                  // architecture: Clean
            "",                                   // data access: default EfCore
            "n",                                  // generate initial migration?
            "",                                   // crud: blank → defaults (all)
            "2",                                  // versioning: UrlSegment
            "2",                                  // auth: JwtBearer
            "y",                                  // include tests
            "n",                                  // include Docker
            "n",                                   // partition sproc interfaces by schema
            "n",                                   // include child collections in responses
            "Server=(local);Database=X;Trusted_Connection=True;");

        var config = new WizardRunner(io).GatherStaticChoices();

        Assert.Equal(ApiVersion.V2, config.ApiVersion);
    }

    [Fact]
    public void Wizard_prompts_for_repository_interfaces_when_dapper_chosen()
    {
        // Walk through the wizard: accept defaults up to Data Access, choose Dapper (option 2),
        // then YES for repository interfaces.
        var inputs = BuildInputsForDapperWithRepoInterfaces();
        var io = new FakeConsoleIO(inputs);
        var runner = new WizardRunner(io);
        var config = runner.GatherStaticChoices();

        Assert.Equal(DataAccessStyle.Dapper, config.DataAccess);
        Assert.True(config.EmitRepositoryInterfaces);
    }

    [Fact]
    public void Wizard_does_not_prompt_for_repository_interfaces_when_efcore()
    {
        // With EF Core chosen, the prompt is skipped — flag remains default false.
        var inputs = BuildInputsForEfCoreDefault();
        var io = new FakeConsoleIO(inputs);
        var runner = new WizardRunner(io);
        var config = runner.GatherStaticChoices();

        Assert.Equal(DataAccessStyle.EfCore, config.DataAccess);
        Assert.False(config.EmitRepositoryInterfaces);
    }

    private static string[] BuildInputsForDapperWithRepoInterfaces()
    {
        return new[]
        {
            "Bookings",                          // project name
            "./out",                              // output dir
            "",                                   // tfm → default net9.0
            "2",                                  // endpoint style: MinimalApi
            "2",                                  // architecture: Clean
            "2",                                   // data access: Dapper
            "y",                                  // emit I<Entity>Repository interfaces?
            "n",                                  // generate initial migration?
            "",                                   // crud: blank → defaults (all)
            "2",                                  // versioning: UrlSegment
            "2",                                  // auth: JwtBearer
            "y",                                  // include tests
            "n",                                  // include Docker
            "n",                                   // partition sproc interfaces by schema
            "n",                                   // include child collections in responses
            "Server=(local);Database=X;Trusted_Connection=True;",
        };
    }

    private static string[] BuildInputsForEfCoreDefault()
    {
        return new[]
        {
            "Bookings",                          // project name
            "./out",                              // output dir
            "",                                   // tfm → default net9.0
            "2",                                  // endpoint style: MinimalApi
            "2",                                  // architecture: Clean
            "",                                   // data access: default EfCore
            "n",                                  // generate initial migration?
            "",                                   // crud: blank → defaults (all)
            "2",                                  // versioning: UrlSegment
            "2",                                  // auth: JwtBearer
            "y",                                  // include tests
            "n",                                  // include Docker
            "n",                                   // partition sproc interfaces by schema
            "n",                                   // include child collections in responses
            "Server=(local);Database=X;Trusted_Connection=True;",
        };
    }

    [Fact]
    public void Wizard_prompts_for_sproc_partition_flag()
    {
        // Accept defaults, choose YES for sproc partition.
        var inputs = BuildInputsWithSprocPartitionYes();
        var io = new FakeConsoleIO(inputs);
        var runner = new WizardRunner(io);
        var config = runner.GatherStaticChoices();

        Assert.True(config.PartitionStoredProceduresBySchema);
    }

    [Fact]
    public void Wizard_default_keeps_sproc_partition_off()
    {
        var inputs = BuildInputsWithAllDefaults();
        var io = new FakeConsoleIO(inputs);
        var runner = new WizardRunner(io);
        var config = runner.GatherStaticChoices();

        Assert.False(config.PartitionStoredProceduresBySchema);
    }

    private static string[] BuildInputsWithSprocPartitionYes()
    {
        return new[]
        {
            "Bookings",                          // project name
            "./out",                              // output dir
            "",                                   // tfm → default net9.0
            "2",                                  // endpoint style: MinimalApi
            "2",                                  // architecture: Clean
            "",                                   // data access: default EfCore
            "n",                                  // generate initial migration?
            "",                                   // crud: blank → defaults (all)
            "2",                                  // versioning: UrlSegment
            "2",                                  // auth: JwtBearer
            "y",                                  // include tests
            "n",                                  // include Docker
            "y",                                  // partition sproc interfaces by schema
            "n",                                   // include child collections in responses
            "Server=(local);Database=X;Trusted_Connection=True;",
        };
    }

    private static string[] BuildInputsWithAllDefaults()
    {
        return new[]
        {
            "Bookings",                          // project name
            "./out",                              // output dir
            "",                                   // tfm → default net9.0
            "2",                                  // endpoint style: MinimalApi
            "2",                                  // architecture: Clean
            "",                                   // data access: default EfCore
            "n",                                  // generate initial migration?
            "",                                   // crud: blank → defaults (all)
            "2",                                  // versioning: UrlSegment
            "2",                                  // auth: JwtBearer
            "y",                                  // include tests
            "n",                                  // include Docker
            "",                                   // partition sproc interfaces by schema → default false
            "",                                   // include child collections in responses → default false
            "Server=(local);Database=X;Trusted_Connection=True;",
        };
    }
}
