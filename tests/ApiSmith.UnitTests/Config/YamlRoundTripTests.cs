using ApiSmith.Config;

namespace ApiSmith.UnitTests.Config;

public sealed class YamlRoundTripTests
{
    [Fact]
    public void Defaults_round_trip()
    {
        var original = new ApiSmithConfig { ProjectName = "Api1" };
        var yaml = YamlWriter.Write(original);
        var parsed = YamlReader.Read(yaml);

        Assert.Equal(original.ProjectName, parsed.ProjectName);
        Assert.Equal(original.OutputDirectory, parsed.OutputDirectory);
        Assert.Equal(original.TargetFramework, parsed.TargetFramework);
        Assert.Equal(original.EndpointStyle, parsed.EndpointStyle);
        Assert.Equal(original.Architecture, parsed.Architecture);
        Assert.Equal(original.DataAccess, parsed.DataAccess);
        Assert.Equal(original.Auth, parsed.Auth);
        Assert.Equal(original.Versioning, parsed.Versioning);
        Assert.Equal(original.Crud, parsed.Crud);
        Assert.Equal(original.IncludeTestsProject, parsed.IncludeTestsProject);
        Assert.Equal(original.IncludeDockerAssets, parsed.IncludeDockerAssets);
    }

    [Fact]
    public void Non_defaults_round_trip()
    {
        var original = new ApiSmithConfig
        {
            ProjectName = "Bookings",
            OutputDirectory = "./out",
            TargetFramework = "net9.0",
            EndpointStyle = EndpointStyle.MinimalApi,
            Architecture = ArchitectureStyle.Clean,
            DataAccess = DataAccessStyle.Dapper,
            Auth = AuthStyle.JwtBearer,
            Versioning = VersioningStyle.UrlSegment,
            GenerateInitialMigration = true,
            IncludeTestsProject = false,
            IncludeDockerAssets = false,
            Crud = CrudOperations.GetList | CrudOperations.GetById | CrudOperations.Post,
            Schemas = new List<string> { "dbo", "audit" },
        };

        var yaml = YamlWriter.Write(original);
        var parsed = YamlReader.Read(yaml);

        Assert.Equal(original.ProjectName, parsed.ProjectName);
        Assert.Equal(original.EndpointStyle, parsed.EndpointStyle);
        Assert.Equal(original.Architecture, parsed.Architecture);
        Assert.Equal(original.DataAccess, parsed.DataAccess);
        Assert.Equal(original.Auth, parsed.Auth);
        Assert.Equal(original.Versioning, parsed.Versioning);
        Assert.True(parsed.GenerateInitialMigration);
        Assert.False(parsed.IncludeTestsProject);
        Assert.Equal(original.Crud, parsed.Crud);
        Assert.Equal(original.Schemas.OrderBy(s => s, System.StringComparer.Ordinal), parsed.Schemas);
    }

    [Fact]
    public void Connection_string_is_never_written()
    {
        var config = new ApiSmithConfig
        {
            ProjectName = "x",
            ConnectionString = "Server=secret;Database=prod;Password=swordfish;",
        };

        var yaml = YamlWriter.Write(config);
        Assert.DoesNotContain("swordfish", yaml);
        Assert.DoesNotContain("Password", yaml);
    }

    [Fact]
    public void Unknown_keys_are_ignored()
    {
        var yaml = """
            projectName: x
            futureFeature: tomorrow
            endpointStyle: Controllers
            """;

        var parsed = YamlReader.Read(yaml);
        Assert.Equal("x", parsed.ProjectName);
    }

    [Fact]
    public void Comment_line_and_inline_are_ignored()
    {
        var yaml = """
            # top-of-file comment
            projectName: Quux  # trailing comment
            schemas: []
            """;

        var parsed = YamlReader.Read(yaml);
        Assert.Equal("Quux", parsed.ProjectName);
        Assert.Empty(parsed.Schemas);
    }

    [Fact]
    public void Bad_enum_produces_helpful_error()
    {
        var yaml = "endpointStyle: NotReal";
        var ex = Assert.Throws<YamlException>(() => YamlReader.Read(yaml));
        Assert.Contains("endpointStyle", ex.Message);
    }

    [Fact]
    public void ValidateForeignKeyReferences_round_trips()
    {
        var original = new ApiSmithConfig { ValidateForeignKeyReferences = true };
        var yaml = YamlWriter.Write(original);
        var parsed = YamlReader.Read(yaml);
        Assert.True(parsed.ValidateForeignKeyReferences);
    }

    [Fact]
    public void Write_is_deterministic()
    {
        var config = new ApiSmithConfig
        {
            ProjectName = "Stable",
            Schemas = new List<string> { "zeta", "alpha", "mu" },
        };

        var a = YamlWriter.Write(config);
        var b = YamlWriter.Write(config);
        Assert.Equal(a, b);

        Assert.Contains("- alpha", a);
        var alphaIdx = a.IndexOf("- alpha", System.StringComparison.Ordinal);
        var muIdx = a.IndexOf("- mu", System.StringComparison.Ordinal);
        var zetaIdx = a.IndexOf("- zeta", System.StringComparison.Ordinal);
        Assert.True(alphaIdx < muIdx && muIdx < zetaIdx);
    }
}
