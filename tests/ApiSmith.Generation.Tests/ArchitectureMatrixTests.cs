using System.Diagnostics;
using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;

namespace ApiSmith.Generation.Tests;

public sealed class ArchitectureMatrixTests
{
    public static TheoryData<ArchitectureStyle> AllArchitectures() =>
        new()
        {
            ArchitectureStyle.Flat,
            ArchitectureStyle.Clean,
            ArchitectureStyle.VerticalSlice,
            ArchitectureStyle.Layered,
            ArchitectureStyle.Onion,
        };

    [Theory]
    [MemberData(nameof(AllArchitectures))]
    public void Layout_project_list_matches_spec(ArchitectureStyle style)
    {
        var config = new ApiSmithConfig { ProjectName = "Acme", Architecture = style };
        var layout = LayoutFactory.Create(style);
        var projects = layout.Projects(config);

        var expectedCount = style switch
        {
            ArchitectureStyle.Flat          => 1,
            ArchitectureStyle.VerticalSlice => 1,
            ArchitectureStyle.Layered       => 3,
            ArchitectureStyle.Clean         => 4,
            ArchitectureStyle.Onion         => 4,
            _ => throw new System.NotSupportedException(),
        };

        Assert.Equal(expectedCount, projects.Length);
        Assert.Contains(projects, p => p.IsWebProject);

        foreach (var p in projects)
        {
            Assert.StartsWith("src/", p.RelativeCsprojPath);
            Assert.EndsWith(".csproj", p.RelativeCsprojPath);
        }
    }

    [Theory]
    [MemberData(nameof(AllArchitectures))]
    public void Generated_solution_compiles(ArchitectureStyle style)
    {
        if (System.Environment.GetEnvironmentVariable("APISMITH_SKIP_NESTED_BUILD") is { Length: > 0 })
        {
            return;
        }

        var output = Path.Combine(Path.GetTempPath(), "apismith-tests",
            $"Matrix-{style}-{System.Guid.NewGuid().ToString("N")[..8]}");

        var config = new ApiSmithConfig
        {
            ProjectName = "Acme",
            OutputDirectory = output,
            Architecture = style,
            ConnectionString = "Server=x;Database=x;Trusted_Connection=True;",
        };

        try
        {
            new Generator(new NullLog()).Generate(config, SchemaGraphFixtures.Relational(), output);
            AssertCompiles(output, style.ToString());
        }
        finally
        {
            CleanupBestEffort(output);
        }
    }

    [Theory]
    [InlineData(ArchitectureStyle.Flat)]
    [InlineData(ArchitectureStyle.Clean)]
    [InlineData(ArchitectureStyle.Layered)]
    [InlineData(ArchitectureStyle.Onion)]
    [InlineData(ArchitectureStyle.VerticalSlice)]
    public void V2_generated_solution_compiles(ArchitectureStyle arch)
    {
        if (System.Environment.GetEnvironmentVariable("APISMITH_SKIP_NESTED_BUILD") is { Length: > 0 })
        {
            return;
        }

        var (config, output) = Setup($"V2{arch}");
        config.ApiVersion = ApiVersion.V2;
        config.Architecture = arch;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            AssertCompiles(output, $"V2-{arch}");
        }
        finally
        {
            CleanupBestEffort(output);
        }
    }

    private static (ApiSmithConfig Config, string Output) Setup(string projectName)
    {
        var output = Path.Combine(
            Path.GetTempPath(),
            "apismith-tests",
            projectName + "-" + System.Guid.NewGuid().ToString("N")[..8]);
        var config = new ApiSmithConfig
        {
            ProjectName = projectName.Replace("-", ""),
            OutputDirectory = output,
            ConnectionString = "Server=x;Database=x;Trusted_Connection=True;",
        };
        return (config, output);
    }

    private static void AssertCompiles(string output, string label)
    {
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
            $"{label} solution failed to build. exit={proc.ExitCode}\n{stdout}\n{stderr}");
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
            // ignore
        }
    }

    private sealed class NullLog : IScaffoldLog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
