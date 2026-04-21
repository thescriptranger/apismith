using ApiSmith.Config;

namespace ApiSmith.UnitTests.Config;

public sealed class IncludeChildCollectionsConfigTests
{
    [Fact]
    public void Defaults_to_false_on_new_config()
    {
        var config = new ApiSmithConfig { ProjectName = "X" };
        Assert.False(config.IncludeChildCollectionsInResponses);
    }

    [Fact]
    public void Yaml_round_trip_preserves_true()
    {
        var config = new ApiSmithConfig
        {
            ProjectName = "X",
            IncludeChildCollectionsInResponses = true,
        };
        var yaml = YamlWriter.Write(config);
        Assert.Contains("includeChildCollectionsInResponses: true", yaml);

        var roundTripped = YamlReader.Read(yaml);
        Assert.True(roundTripped.IncludeChildCollectionsInResponses);
    }

    [Fact]
    public void Yaml_round_trip_preserves_false()
    {
        var config = new ApiSmithConfig { ProjectName = "X" };
        var yaml = YamlWriter.Write(config);
        Assert.Contains("includeChildCollectionsInResponses: false", yaml);

        var roundTripped = YamlReader.Read(yaml);
        Assert.False(roundTripped.IncludeChildCollectionsInResponses);
    }

    [Fact]
    public void Yaml_reader_ignores_missing_field()
    {
        var yaml = "projectName: X\napiVersion: v2\n";
        var config = YamlReader.Read(yaml);
        Assert.False(config.IncludeChildCollectionsInResponses);
    }
}
