using ApiSmith.Config;
using ApiSmith.Core.Model;
using ApiSmith.Core.Pipeline;

namespace ApiSmith.Generation.Tests;

public sealed class TestsProjectFkAndCheckTests
{
    [Fact]
    public void Fk_flag_on_emits_rejects_default_foreign_key_and_supplies_non_default_fk_value()
    {
        var (config, output) = Setup("FkTests");
        config.ValidateForeignKeyReferences = true;
        config.IncludeTestsProject = true;
        var graph = SchemaGraphFixtures.Relational(); // posts table has required user_id FK to users

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);

            var testFile = File.ReadAllText(
                Path.Combine(output, "tests", $"{config.ProjectName}.IntegrationTests", "Validators", "PostValidatorTests.cs"));

            Assert.Contains("public void Rejects_default_foreign_key()", testFile);
            Assert.Contains("e.PropertyName == \"UserId\"", testFile);
            Assert.Contains("UserId = 1,", testFile);

            Assert.Contains("public void Accepts_minimally_valid_dto()", testFile);
            Assert.Contains("public void Rejects_null_dto()", testFile);
        }
        finally
        {
            CleanupBestEffort(output);
        }
    }

    [Fact]
    public void Check_constraint_greater_or_equal_emits_rejects_method_and_supplies_passing_boundary_value()
    {
        var (config, output) = Setup("CheckTestsGE");
        config.IncludeTestsProject = true;
        var graph = BuildGraphWithCheck("users", "age", "ck_users_age_nonneg", "([Age] >= 0)");

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);

            var testFile = File.ReadAllText(
                Path.Combine(output, "tests", $"{config.ProjectName}.IntegrationTests", "Validators", "UserValidatorTests.cs"));

            Assert.Contains("public void Rejects_value_outside_check_constraint_Age()", testFile);
            Assert.Contains("Age = -1,", testFile);
            Assert.Contains("Age = 0,", testFile);
            Assert.Contains("e.PropertyName == \"Age\"", testFile);
        }
        finally
        {
            CleanupBestEffort(output);
        }
    }

    [Fact]
    public void Check_constraint_between_supplies_lo_as_pass_and_lo_minus_one_as_violate()
    {
        var (config, output) = Setup("CheckTestsBetween");
        config.IncludeTestsProject = true;
        var graph = BuildGraphWithCheck("users", "age", "ck_users_age_range", "([Age] BETWEEN 10 AND 20)");

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);

            var testFile = File.ReadAllText(
                Path.Combine(output, "tests", $"{config.ProjectName}.IntegrationTests", "Validators", "UserValidatorTests.cs"));

            Assert.Contains("Age = 10,", testFile);
            Assert.Contains("public void Rejects_value_outside_check_constraint_Age()", testFile);
            Assert.Contains("Age = 9,", testFile);
        }
        finally
        {
            CleanupBestEffort(output);
        }
    }

    [Fact]
    public void Baseline_tests_project_output_is_unchanged_when_no_phase3_rules_fire()
    {
        var (configA, outputA) = Setup("BaselineA");
        configA.IncludeTestsProject = true;
        configA.ValidateForeignKeyReferences = false;
        var graphA = SchemaGraphFixtures.Relational();

        var (configB, outputB) = Setup("BaselineB");
        configB.IncludeTestsProject = true;
        configB.ValidateForeignKeyReferences = false;
        var graphB = SchemaGraphFixtures.Relational();

        try
        {
            new Generator(new NullLog()).Generate(configA, graphA, outputA);
            new Generator(new NullLog()).Generate(configB, graphB, outputB);

            var testsRelativeA = Path.Combine("tests", $"{configA.ProjectName}.IntegrationTests", "Validators", "PostValidatorTests.cs");
            var testsRelativeB = Path.Combine("tests", $"{configB.ProjectName}.IntegrationTests", "Validators", "PostValidatorTests.cs");

            var contentA = File.ReadAllText(Path.Combine(outputA, testsRelativeA));
            var contentB = File.ReadAllText(Path.Combine(outputB, testsRelativeB));

            // normalize project name (only legit cross-run difference)
            var normalizedA = contentA.Replace(configA.ProjectName, "PROJECT");
            var normalizedB = contentB.Replace(configB.ProjectName, "PROJECT");
            Assert.Equal(normalizedA, normalizedB);

            Assert.DoesNotContain("Rejects_default_foreign_key", contentA);
            Assert.DoesNotContain("Rejects_value_outside_check_constraint_", contentA);
        }
        finally
        {
            CleanupBestEffort(outputA);
            CleanupBestEffort(outputB);
        }
    }

    private static SchemaGraph BuildGraphWithCheck(string tableName, string columnName, string ckName, string expr)
    {
        var table = Table.Create(
            schema: "dbo",
            name: tableName,
            columns: new[]
            {
                new Column("id",       1, "int", IsNullable: false, IsIdentity: true,  IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column(columnName, 2, "int", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
            },
            primaryKey: PrimaryKey.Create($"PK_{tableName}", new[] { "id" }),
            checkConstraints: new[] { new CheckConstraint(ckName, expr) });

        return SchemaGraph.Create(new[] { DbSchema.Create("dbo", new[] { table }) });
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
