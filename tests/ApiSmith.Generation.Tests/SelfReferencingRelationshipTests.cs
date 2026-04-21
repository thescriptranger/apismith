using ApiSmith.Config;
using ApiSmith.Core.Pipeline;

namespace ApiSmith.Generation.Tests;

public sealed class SelfReferencingRelationshipTests
{
    [Fact]
    public void DbContext_emits_distinct_with_many_per_fk_for_self_referencing_table()
    {
        var (config, output) = Setup("SelfRef1");
        config.ApiVersion = ApiVersion.V2;
        config.DataAccess = DataAccessStyle.EfCore;
        var graph = SchemaGraphFixtures.SelfReferencingWithTwoFks();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var dbContextPath = Path.Combine(output, "src", "SelfRef1", "Data", "SelfRef1DbContext.cs");
            Assert.True(File.Exists(dbContextPath), $"Missing {dbContextPath}");
            var content = File.ReadAllText(dbContextPath);

            // Two distinct HasOne calls — one per reference nav.
            Assert.Contains("b.HasOne(e => e.ChargesBillToProfile)", content);
            Assert.Contains("b.HasOne(e => e.DuesBillToProfile)", content);

            // Two distinct WithMany calls — disambiguated target-side collection.
            Assert.Contains(".WithMany(x => x.BillingProfiles)", content);
            Assert.Contains(".WithMany(x => x.BillingProfiles2)", content);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void Entity_emits_distinct_reference_nav_properties_per_fk()
    {
        var (config, output) = Setup("SelfRef2");
        config.ApiVersion = ApiVersion.V2;
        config.DataAccess = DataAccessStyle.EfCore;
        var graph = SchemaGraphFixtures.SelfReferencingWithTwoFks();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var entityPath = Path.Combine(output, "src", "SelfRef2", "Entities", "BillingProfile.cs");
            Assert.True(File.Exists(entityPath), $"Missing {entityPath}");
            var content = File.ReadAllText(entityPath);

            Assert.Contains("ChargesBillToProfile", content);
            Assert.Contains("DuesBillToProfile", content);
            Assert.Contains("BillingProfiles", content);
            Assert.Contains("BillingProfiles2", content);
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
