using System.Diagnostics;
using ApiSmith.Config;
using ApiSmith.Core.Pipeline;

namespace ApiSmith.Generation.Tests;

public sealed class GeneratorSmokeTests
{
    [Fact]
    public void Generates_expected_files_for_small_schema()
    {
        var (config, output) = Setup("BlogApi");
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            var generator = new Generator(new NullLog());
            var report = generator.Generate(config, graph, output);

            Assert.True(report.FileCount > 0);
            Assert.Equal(3, report.TableCount);

            Assert.True(File.Exists(Path.Combine(output, "BlogApi.sln")));
            Assert.True(File.Exists(Path.Combine(output, "src", "BlogApi", "BlogApi.csproj")));
            Assert.True(File.Exists(Path.Combine(output, "src", "BlogApi", "Program.cs")));
            Assert.True(File.Exists(Path.Combine(output, "apismith.yaml")));

            foreach (var entity in new[] { "User", "Post", "AuditLog" })
            {
                Assert.True(File.Exists(Path.Combine(output, "src", "BlogApi", "Entities", $"{entity}.cs")), $"Missing entity {entity}");
                Assert.True(File.Exists(Path.Combine(output, "src", "BlogApi", "Dtos", $"{entity}Dtos.cs")));
                Assert.True(File.Exists(Path.Combine(output, "src", "BlogApi", "Validators", $"{entity}DtoValidators.cs")));
                Assert.True(File.Exists(Path.Combine(output, "src", "BlogApi", "Mappings", $"{entity}Mappings.cs")));
            }
        }
        finally
        {
            CleanupBestEffort(output);
        }
    }

    [Fact]
    public void Replay_is_byte_identical()
    {
        // same configured OutputDirectory (apismith.yaml identical), two physical write locations
        var (config1, output1) = Setup("ReplayApi");
        var (config2, output2) = Setup("ReplayApi");
        config1.OutputDirectory = "./ReplayApi";
        config2.OutputDirectory = "./ReplayApi";
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config1, graph, output1);
            new Generator(new NullLog()).Generate(config2, graph, output2);

            var files1 = Directory.GetFiles(output1, "*", SearchOption.AllDirectories)
                .Select(p => Path.GetRelativePath(output1, p).Replace('\\', '/'))
                .OrderBy(p => p, System.StringComparer.Ordinal)
                .ToArray();

            var files2 = Directory.GetFiles(output2, "*", SearchOption.AllDirectories)
                .Select(p => Path.GetRelativePath(output2, p).Replace('\\', '/'))
                .OrderBy(p => p, System.StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(files1, files2);

            foreach (var rel in files1)
            {
                var b1 = File.ReadAllBytes(Path.Combine(output1, rel.Replace('/', Path.DirectorySeparatorChar)));
                var b2 = File.ReadAllBytes(Path.Combine(output2, rel.Replace('/', Path.DirectorySeparatorChar)));
                Assert.True(b1.SequenceEqual(b2), $"File differs across runs: {rel}");
            }
        }
        finally
        {
            CleanupBestEffort(output1);
            CleanupBestEffort(output2);
        }
    }

    [Fact]
    public void Generated_solution_compiles()
    {
        // env escape hatch for CI hosts without headroom for a nested dotnet build
        if (System.Environment.GetEnvironmentVariable("APISMITH_SKIP_NESTED_BUILD") is { Length: > 0 })
        {
            return;
        }

        var (config, output) = Setup("CompileApi");
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);

            var psi = new ProcessStartInfo("dotnet", $"build \"{output}\" --nologo -clp:NoSummary")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            using var proc = Process.Start(psi)!;
            var stdout = proc.StandardOutput.ReadToEnd();
            var stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            Assert.True(
                proc.ExitCode == 0,
                $"dotnet build of generated solution failed with exit code {proc.ExitCode}.\n--- stdout ---\n{stdout}\n--- stderr ---\n{stderr}");
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
