using System.Text.RegularExpressions;
using ApiSmith.Config;
using ApiSmith.Core.Pipeline;

namespace ApiSmith.Generation.Tests;

/// <summary>Locks the forward-slash convention for ProjectReference paths (MSBuild accepts both, but backslashes look non-portable).</summary>
public sealed class CsprojPathTests
{
    [Theory]
    [InlineData(ArchitectureStyle.Flat)]
    [InlineData(ArchitectureStyle.Clean)]
    [InlineData(ArchitectureStyle.VerticalSlice)]
    [InlineData(ArchitectureStyle.Layered)]
    [InlineData(ArchitectureStyle.Onion)]
    public void ProjectReference_paths_use_forward_slashes_only(ArchitectureStyle style)
    {
        var (config, output) = Setup($"ForwardSlash{style}");
        config.Architecture = style;
        // covers the historically-bad `..\..\src\Foo\Foo.csproj` path
        config.IncludeTestsProject = true;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);

            var csprojs = Directory.GetFiles(output, "*.csproj", SearchOption.AllDirectories);
            Assert.NotEmpty(csprojs);

            var rx = new Regex("<ProjectReference\\s+Include=\"([^\"]+)\"", RegexOptions.Compiled);
            var offenders = new List<string>();
            foreach (var path in csprojs)
            {
                var content = File.ReadAllText(path);
                foreach (Match m in rx.Matches(content))
                {
                    if (m.Groups[1].Value.Contains('\\'))
                    {
                        offenders.Add($"{path}: {m.Groups[1].Value}");
                    }
                }
            }

            Assert.Empty(offenders);
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
