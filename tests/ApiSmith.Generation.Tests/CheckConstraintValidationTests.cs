using ApiSmith.Config;
using ApiSmith.Core.Model;
using ApiSmith.Core.Pipeline;

namespace ApiSmith.Generation.Tests;

public sealed class CheckConstraintValidationTests
{
    [Fact]
    public void Translatable_numeric_comparison_emits_typed_rule()
    {
        var (config, output) = Setup("CheckA");
        var graph = BuildGraphWithCheck("users", "age", "ck_users_age_nonneg", "([Age] >= 0)");

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var v = File.ReadAllText(Path.Combine(output, "src", "CheckA", "Validators", "UserDtoValidators.cs"));

            Assert.Contains("// From SQL check constraint 'ck_users_age_nonneg': ([Age] >= 0)", v);
            Assert.Contains("if (dto.Age < 0)", v);
            Assert.Contains("result.Add(nameof(dto.Age), \"Age must be >= 0.\");", v);
        }
        finally
        {
            CleanupBestEffort(output);
        }
    }

    [Fact]
    public void Untranslatable_expression_emits_todo_comment()
    {
        var (config, output) = Setup("CheckB");
        var graph = BuildGraphWithCheck("users", "status", "ck_users_status",
            "([Status] IN ('draft','published'))");

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var v = File.ReadAllText(Path.Combine(output, "src", "CheckB", "Validators", "UserDtoValidators.cs"));

            Assert.Contains("// TODO: translate check constraint 'ck_users_status': ([Status] IN ('draft','published'))", v);
            Assert.DoesNotContain("if (dto.Status <", v);
        }
        finally
        {
            CleanupBestEffort(output);
        }
    }

    [Fact]
    public void Between_emits_range_check()
    {
        var (config, output) = Setup("CheckC");
        var graph = BuildGraphWithCheck("users", "age", "ck_users_age_range", "([Age] BETWEEN 0 AND 120)");

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var v = File.ReadAllText(Path.Combine(output, "src", "CheckC", "Validators", "UserDtoValidators.cs"));

            Assert.Contains("if (dto.Age < 0 || dto.Age > 120)", v);
            Assert.Contains("result.Add(nameof(dto.Age), \"Age must be between 0 and 120.\");", v);
        }
        finally
        {
            CleanupBestEffort(output);
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
