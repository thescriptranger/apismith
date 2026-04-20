using ApiSmith.Config;
using ApiSmith.Core.Pipeline;

namespace ApiSmith.Generation.Tests;

public sealed class FkValidationTests
{
    [Fact]
    public void Required_fk_emits_todo_and_default_check_when_flag_enabled()
    {
        var (config, output) = Setup("FkFlag");
        config.ValidateForeignKeyReferences = true;
        var graph = SchemaGraphFixtures.Relational(); // posts table has required user_id FK

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);

            var postValidator = File.ReadAllText(
                Path.Combine(output, "src", "FkFlag", "Validators", "PostDtoValidators.cs"));

            Assert.Contains("// TODO: confirm UserId references an existing User", postValidator);
            Assert.Contains("if (dto.UserId == default)", postValidator);
            Assert.Contains("result.Add(nameof(dto.UserId), \"UserId is required.\");", postValidator);
        }
        finally
        {
            CleanupBestEffort(output);
        }
    }

    [Fact]
    public void Flag_off_produces_no_fk_check()
    {
        var (config, output) = Setup("FkOff");
        config.ValidateForeignKeyReferences = false;
        var graph = SchemaGraphFixtures.Relational();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);

            var postValidator = File.ReadAllText(
                Path.Combine(output, "src", "FkOff", "Validators", "PostDtoValidators.cs"));

            Assert.DoesNotContain("TODO: confirm", postValidator);
            Assert.DoesNotContain("dto.UserId == default", postValidator);
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
