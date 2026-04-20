using ApiSmith.Config;
using ApiSmith.Core.Pipeline;

namespace ApiSmith.Generation.Tests;

public sealed class GeneratedCsprojEnforcesStrictModeTests
{
    [Theory]
    [InlineData(ArchitectureStyle.Flat)]
    [InlineData(ArchitectureStyle.Clean)]
    [InlineData(ArchitectureStyle.Layered)]
    [InlineData(ArchitectureStyle.Onion)]
    [InlineData(ArchitectureStyle.VerticalSlice)]
    public void Every_emitted_csproj_enables_strict_mode(ArchitectureStyle arch)
    {
        var (config, output) = Setup($"Strict-{arch}");
        config.Architecture = arch;
        config.IncludeTestsProject = true;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);

            var csprojs = Directory.GetFiles(output, "*.csproj", SearchOption.AllDirectories);
            Assert.NotEmpty(csprojs);

            foreach (var path in csprojs)
            {
                var content = File.ReadAllText(path);
                Assert.Contains("<TreatWarningsAsErrors>true</TreatWarningsAsErrors>", content);
                Assert.Contains("<WarningsAsErrors />", content);
                Assert.Contains("<Nullable>enable</Nullable>", content);
                Assert.Contains("<ImplicitUsings>enable</ImplicitUsings>", content);
                Assert.Contains("<LangVersion>latest</LangVersion>", content);
            }
        }
        finally
        {
            CleanupBestEffort(output);
        }
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
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Don't fail the test because of cleanup; CI temp dirs get reaped anyway.
        }
    }

    private sealed class NullLog : IScaffoldLog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
