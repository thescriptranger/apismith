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
}
