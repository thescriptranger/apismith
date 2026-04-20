using System.Diagnostics;
using ApiSmith.Config;
using ApiSmith.Core.Pipeline;

namespace ApiSmith.Generation.Tests;

public sealed class MultiSchemaTests
{
    [Fact]
    public void Emits_schema_scoped_folders_and_namespaces_for_each_schema()
    {
        var (config, output) = Setup("MultiApi");
        config.Schemas = new List<string> { "dbo", "audit" };
        var graph = SchemaGraphFixtures.CrossSchema();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);

            Assert.True(
                File.Exists(Path.Combine(output, "src", "MultiApi", "Entities", "Dbo", "User.cs")),
                "Missing Entities/Dbo/User.cs");
            Assert.True(
                File.Exists(Path.Combine(output, "src", "MultiApi", "Entities", "Audit", "UserAction.cs")),
                "Missing Entities/Audit/UserAction.cs");

            var userCs = File.ReadAllText(Path.Combine(output, "src", "MultiApi", "Entities", "Dbo", "User.cs"));
            var userActionCs = File.ReadAllText(Path.Combine(output, "src", "MultiApi", "Entities", "Audit", "UserAction.cs"));
            Assert.Contains("namespace MultiApi.Entities.Dbo",   userCs);
            Assert.Contains("namespace MultiApi.Entities.Audit", userActionCs);

            Assert.Contains("using MultiApi.Entities.Dbo;", userActionCs);

            Assert.Contains("using MultiApi.Entities.Audit;", userCs);

            if (System.Environment.GetEnvironmentVariable("APISMITH_SKIP_NESTED_BUILD") is { Length: > 0 })
            {
                return;
            }

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
                $"Cross-schema solution failed to build. exit={proc.ExitCode}\n{stdout}\n{stderr}");
        }
        finally
        {
            CleanupBestEffort(output);
        }
    }

    [Fact]
    public void Cross_schema_name_collision_does_not_emit_wrong_schema_using()
    {
        // regression: GroupBy(e).First() picked one schema for both sides and emitted a spurious cross-schema using
        var (config, output) = Setup("CollisionApi");
        config.Schemas = new List<string> { "dbo", "audit" };
        var graph = SchemaGraphFixtures.CrossSchemaNameCollision();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);

            var dboUserCs   = File.ReadAllText(Path.Combine(output, "src", "CollisionApi", "Entities", "Dbo",   "User.cs"));
            var auditUserCs = File.ReadAllText(Path.Combine(output, "src", "CollisionApi", "Entities", "Audit", "User.cs"));

            Assert.DoesNotContain("using CollisionApi.Entities.Audit;", dboUserCs);
            Assert.DoesNotContain("using CollisionApi.Entities.Dbo;", auditUserCs);

            AssertCompiles(output);
        }
        finally
        {
            CleanupBestEffort(output);
        }
    }

    [Fact]
    public void Dbcontext_disambiguates_name_collided_entities_with_fq_types_and_schema_prefixed_properties()
    {
        var (config, output) = Setup("CollisionApi");
        config.Schemas = new List<string> { "dbo", "audit" };
        var graph = SchemaGraphFixtures.CrossSchemaNameCollision();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);

            var dbContextPath = Path.Combine(output, "src", "CollisionApi", "Data", "CollisionApiDbContext.cs");
            Assert.True(File.Exists(dbContextPath), $"Missing DbContext at {dbContextPath}");
            var ctx = File.ReadAllText(dbContextPath);

            Assert.Contains("DbSet<CollisionApi.Entities.Dbo.User>", ctx);
            Assert.Contains("DbSet<CollisionApi.Entities.Audit.User>", ctx);
            Assert.Contains("DbSet<CollisionApi.Entities.Dbo.Group>", ctx);
            Assert.Contains("DbSet<CollisionApi.Entities.Audit.Group>", ctx);

            Assert.Contains("DboUsers", ctx);
            Assert.Contains("AuditUsers", ctx);
            Assert.Contains("DboGroups", ctx);
            Assert.Contains("AuditGroups", ctx);

            Assert.Contains("modelBuilder.Entity<CollisionApi.Entities.Dbo.User>", ctx);
            Assert.Contains("modelBuilder.Entity<CollisionApi.Entities.Audit.User>", ctx);

            AssertCompiles(output);
        }
        finally
        {
            CleanupBestEffort(output);
        }
    }

    /// <summary>Runs <c>dotnet build</c> against the scaffold; honors <c>APISMITH_SKIP_NESTED_BUILD</c>.</summary>
    private static void AssertCompiles(string output)
    {
        if (System.Environment.GetEnvironmentVariable("APISMITH_SKIP_NESTED_BUILD") is { Length: > 0 })
        {
            return;
        }

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
            $"Generated solution failed to build. exit={proc.ExitCode}\n{stdout}\n{stderr}");
    }

    private static (ApiSmithConfig Config, string Output) Setup(string projectName)
    {
        var output = Path.Combine(Path.GetTempPath(), "apismith-tests",
            projectName + "-" + System.Guid.NewGuid().ToString("N")[..8]);
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
