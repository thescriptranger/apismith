using ApiSmith.Config;
using ApiSmith.Core.Pipeline;

namespace ApiSmith.Generation.Tests;

public sealed class ApiProblemEmissionTests
{
    [Fact]
    public void V2_scaffold_emits_api_problem_record()
    {
        var (config, output) = Setup("Prob1");
        config.ApiVersion = ApiVersion.V2;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var path = Path.Combine(output, "src", "Prob1.Shared", "Errors", "ApiProblem.cs");
            Assert.True(File.Exists(path), $"Missing {path}");
            var content = File.ReadAllText(path);
            Assert.Contains("public sealed record ApiProblem", content);
            Assert.Contains("namespace Prob1.Shared.Errors", content);
            Assert.Contains("ImmutableArray<ValidationError>", content);
            Assert.Contains("using System.Collections.Immutable;", content);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V2_controller_returns_api_problem_on_400()
    {
        var (config, output) = Setup("Prob2");
        config.ApiVersion = ApiVersion.V2;
        config.EndpointStyle = EndpointStyle.Controllers;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var ctrl = File.ReadAllText(Path.Combine(output, "src", "Prob2", "Controllers", "PostsController.cs"));
            Assert.Contains("new ApiProblem(", ctrl);
            Assert.Contains("BadRequest(new ApiProblem", ctrl);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V2_minimal_api_returns_api_problem_on_400()
    {
        var (config, output) = Setup("Prob3");
        config.ApiVersion = ApiVersion.V2;
        config.EndpointStyle = EndpointStyle.MinimalApi;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var endp = File.ReadAllText(Path.Combine(output, "src", "Prob3", "Endpoints", "PostsEndpoints.cs"));
            Assert.Contains("new ApiProblem(", endp);
            Assert.Contains("Results.BadRequest(new ApiProblem", endp);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V1_controller_returns_raw_errors_on_400_unchanged()
    {
        var (config, output) = Setup("Prob4");
        config.ApiVersion = ApiVersion.V1;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var ctrl = File.ReadAllText(Path.Combine(output, "src", "Prob4", "Controllers", "PostsController.cs"));
            Assert.DoesNotContain("ApiProblem", ctrl);
            Assert.Contains("BadRequest(validation.Errors)", ctrl);
        }
        finally { CleanupBestEffort(output); }
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
