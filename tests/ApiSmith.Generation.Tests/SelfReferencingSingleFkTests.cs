using ApiSmith.Config;
using ApiSmith.Core.Pipeline;

namespace ApiSmith.Generation.Tests;

public sealed class SelfReferencingSingleFkTests
{
    [Fact]
    public void Entity_reference_nav_renamed_to_avoid_class_name_collision()
    {
        var (config, output) = Setup("SelfRefSingle1");
        config.ApiVersion = ApiVersion.V2;
        config.DataAccess = DataAccessStyle.EfCore;
        var graph = SchemaGraphFixtures.SelfReferencingWithOneFk();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var entityPath = Path.Combine(output, "src", "SelfRefSingle1", "Entities", "TacticCategory.cs");
            Assert.True(File.Exists(entityPath), $"Missing {entityPath}");
            var content = File.ReadAllText(entityPath);

            // The disambiguated reference-nav property.
            Assert.Contains("public TacticCategory? Parent", content);
            // The broken CS0542-inducing name must NOT appear.
            Assert.DoesNotContain("public TacticCategory? TacticCategory", content);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void DbContext_uses_renamed_reference_nav()
    {
        var (config, output) = Setup("SelfRefSingle2");
        config.ApiVersion = ApiVersion.V2;
        config.DataAccess = DataAccessStyle.EfCore;
        var graph = SchemaGraphFixtures.SelfReferencingWithOneFk();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var dbctxPath = Path.Combine(output, "src", "SelfRefSingle2", "Data", "SelfRefSingle2DbContext.cs");
            Assert.True(File.Exists(dbctxPath), $"Missing {dbctxPath}");
            var content = File.ReadAllText(dbctxPath);

            // HasOne uses the renamed reference nav; WithMany uses the (already-correct) collection nav.
            Assert.Contains("b.HasOne(e => e.Parent)", content);
            Assert.Contains(".WithMany(x => x.TacticCategories)", content);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void Collection_nav_side_unchanged()
    {
        var (config, output) = Setup("SelfRefSingle3");
        config.ApiVersion = ApiVersion.V2;
        config.DataAccess = DataAccessStyle.EfCore;
        var graph = SchemaGraphFixtures.SelfReferencingWithOneFk();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var entityPath = Path.Combine(output, "src", "SelfRefSingle3", "Entities", "TacticCategory.cs");
            var content = File.ReadAllText(entityPath);

            // The inverse collection-nav side already produces a non-colliding plural name — documents that only the ref-nav was renamed.
            Assert.Contains("TacticCategories", content);
        }
        finally { CleanupBestEffort(output); }
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
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { }
    }

    private sealed class NullLog : IScaffoldLog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
