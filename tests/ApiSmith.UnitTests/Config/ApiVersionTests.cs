using ApiSmith.Config;

namespace ApiSmith.UnitTests.Config;

public sealed class ApiVersionTests
{
    [Fact]
    public void Default_is_v1()
    {
        var config = new ApiSmithConfig();
        Assert.Equal(ApiVersion.V1, config.ApiVersion);
    }

    [Fact]
    public void Missing_field_defaults_to_v1()
    {
        var config = YamlReader.Read("projectName: X\n");
        Assert.Equal(ApiVersion.V1, config.ApiVersion);
    }

    [Fact]
    public void Parses_v1_literal()
    {
        var config = YamlReader.Read("apiVersion: v1\nprojectName: X\n");
        Assert.Equal(ApiVersion.V1, config.ApiVersion);
    }

    [Fact]
    public void Parses_v2_literal()
    {
        var config = YamlReader.Read("apiVersion: v2\nprojectName: X\n");
        Assert.Equal(ApiVersion.V2, config.ApiVersion);
    }

    [Fact]
    public void Unknown_version_throws_with_valid_values_in_message()
    {
        var ex = Assert.Throws<YamlException>(() => YamlReader.Read("apiVersion: v99\nprojectName: X\n"));
        Assert.Contains("v1", ex.Message);
        Assert.Contains("v2", ex.Message);
    }

    [Fact]
    public void Writer_emits_apiVersion_as_first_key()
    {
        var config = new ApiSmithConfig { ApiVersion = ApiVersion.V2, ProjectName = "Foo" };
        var yaml = YamlWriter.Write(config);
        var firstLine = yaml.Split('\n').First(l => !l.StartsWith('#') && l.Trim().Length > 0);
        Assert.Equal("apiVersion: v2", firstLine.Trim());
    }

    [Fact]
    public void Round_trip_preserves_v1_and_v2()
    {
        foreach (var ver in new[] { ApiVersion.V1, ApiVersion.V2 })
        {
            var original = new ApiSmithConfig { ApiVersion = ver, ProjectName = "Foo" };
            var yaml = YamlWriter.Write(original);
            var parsed = YamlReader.Read(yaml);
            Assert.Equal(ver, parsed.ApiVersion);
        }
    }
}
