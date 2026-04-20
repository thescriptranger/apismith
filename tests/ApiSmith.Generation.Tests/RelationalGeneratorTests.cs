using System.Diagnostics;
using ApiSmith.Config;
using ApiSmith.Core.Pipeline;

namespace ApiSmith.Generation.Tests;

public sealed class RelationalGeneratorTests
{
    [Fact]
    public void Named_schema_detects_join_table_and_navigations()
    {
        var graph = SchemaGraphFixtures.Relational();
        var named = NamedSchemaModel.Build(graph);

        Assert.Equal(3, named.Tables.Length);
        Assert.Single(named.JoinTables);

        var user = named.Tables.Single(t => t.EntityName == "User");
        var post = named.Tables.Single(t => t.EntityName == "Post");
        var tag  = named.Tables.Single(t => t.EntityName == "Tag");

        Assert.Contains(post.ReferenceNavigations, n => n.Name == "User" && n.TargetEntityName == "User");
        Assert.Contains(user.CollectionNavigations, n => n.Name == "Posts");
        Assert.Contains(post.SkipNavigations, n => n.OtherEntityName == "Tag");
        Assert.Contains(tag.SkipNavigations,  n => n.OtherEntityName == "Post");
    }

    [Fact]
    public void Relational_schema_compiles_as_generated_solution()
    {
        if (System.Environment.GetEnvironmentVariable("APISMITH_SKIP_NESTED_BUILD") is { Length: > 0 })
        {
            return;
        }

        var output = Path.Combine(Path.GetTempPath(), "apismith-tests",
            "RelApi-" + System.Guid.NewGuid().ToString("N")[..8]);
        var config = new ApiSmithConfig
        {
            ProjectName = "RelApi",
            OutputDirectory = output,
            ConnectionString = "Server=x;Database=x;",
        };

        try
        {
            new Generator(new NullLog()).Generate(config, SchemaGraphFixtures.Relational(), output);

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
                $"Generated relational solution failed to build. exit={proc.ExitCode}\n{stdout}\n{stderr}");
        }
        finally
        {
            try { if (Directory.Exists(output)) Directory.Delete(output, recursive: true); } catch { /* ignore */ }
        }
    }

    private sealed class NullLog : IScaffoldLog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
