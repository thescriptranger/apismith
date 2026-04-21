using ApiSmith.Config;
using ApiSmith.Core.Pipeline;

namespace ApiSmith.Generation.Tests;

public sealed class LaunchSettingsTests
{
    [Fact]
    public void LaunchSettings_opens_scalar_ui_on_run()
    {
        var (config, output) = Setup("Launch1");
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var path = Path.Combine(output, "src", "Launch1", "Properties", "launchSettings.json");
            Assert.True(File.Exists(path), $"Missing {path}");
            var content = File.ReadAllText(path);
            Assert.Contains("\"launchBrowser\": true", content);
            Assert.Contains("\"launchUrl\": \"scalar/v1\"", content);
            Assert.Contains("\"ASPNETCORE_ENVIRONMENT\": \"Development\"", content);
        }
        finally { CleanupBestEffort(output); }
    }

    private static (ApiSmithConfig Config, string Output) Setup(string projectName)
    {
        var output = Path.Combine(Path.GetTempPath(), "apismith-tests", projectName + "-" + System.Guid.NewGuid().ToString("N")[..8]);
        var config = new ApiSmithConfig
        {
            ProjectName = projectName,
            OutputDirectory = output,
            ConnectionString = "Server=test;Database=test;Trusted_Connection=True;",
        };
        return (config, output);
    }

    private static void CleanupBestEffort(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch { }
    }

    private sealed class NullLog : IScaffoldLog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
