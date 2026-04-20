using ApiSmith.Config;
using ApiSmith.Core.Pipeline;

namespace ApiSmith.Generation.Tests;

public sealed class RepositoryInterfaceTests
{
    [Fact]
    public void Flag_off_does_not_emit_repository_interfaces()
    {
        var (config, output) = Setup("Repo1");
        config.DataAccess = DataAccessStyle.Dapper;
        config.EmitRepositoryInterfaces = false;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var repo = File.ReadAllText(Path.Combine(output, "src", "Repo1", "Data", "PostRepository.cs"));
            Assert.DoesNotContain("public interface IPostRepository", repo);
            Assert.DoesNotContain(": IPostRepository", repo);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void Flag_on_emits_repository_interfaces_and_binds_di()
    {
        var (config, output) = Setup("Repo2");
        config.DataAccess = DataAccessStyle.Dapper;
        config.EmitRepositoryInterfaces = true;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var repo = File.ReadAllText(Path.Combine(output, "src", "Repo2", "Data", "PostRepository.cs"));
            Assert.Contains("public interface IPostRepository", repo);
            Assert.Contains("public sealed partial class PostRepository : IPostRepository", repo);

            var program = File.ReadAllText(Path.Combine(output, "src", "Repo2", "Program.cs"));
            Assert.Contains("AddScoped<IPostRepository, PostRepository>()", program);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void Flag_on_controller_injects_interface()
    {
        var (config, output) = Setup("Repo3");
        config.DataAccess = DataAccessStyle.Dapper;
        config.EmitRepositoryInterfaces = true;
        config.EndpointStyle = EndpointStyle.Controllers;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var ctrl = File.ReadAllText(Path.Combine(output, "src", "Repo3", "Controllers", "PostsController.cs"));
            Assert.Contains("IPostRepository", ctrl);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void Flag_ignored_when_dataaccess_is_efcore()
    {
        // EmitRepositoryInterfaces only applies to Dapper. With EF Core, there are no repositories.
        var (config, output) = Setup("Repo4");
        config.DataAccess = DataAccessStyle.EfCore;
        config.EmitRepositoryInterfaces = true; // flag on, but EF Core — should be a no-op
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            // Should just not fail; no IPostRepository should exist anywhere.
            new Generator(new NullLog()).Generate(config, graph, output);
            var anyRepoInterface = Directory.EnumerateFiles(output, "*.cs", SearchOption.AllDirectories)
                .Any(f => File.ReadAllText(f).Contains("public interface IPostRepository"));
            Assert.False(anyRepoInterface);
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
