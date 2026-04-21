using ApiSmith.Config;
using ApiSmith.Core.Pipeline;

namespace ApiSmith.Generation.Tests;

public sealed class ListQueryExtensionPointTests
{
    [Fact]
    public void V2_efcore_controller_emits_configure_list_query_hook()
    {
        var (config, output) = Setup("Ext1");
        config.ApiVersion = ApiVersion.V2;
        config.EndpointStyle = EndpointStyle.Controllers;
        config.DataAccess = DataAccessStyle.EfCore;
        var graph = SchemaGraphFixtures.SmallBlog();
        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var ctrl = File.ReadAllText(Path.Combine(output, "src", "Ext1", "Controllers", "PostsController.cs"));
            Assert.Contains("ConfigureListQuery(ref query);", ctrl);
            Assert.Contains("static partial void ConfigureListQuery(ref IQueryable<Post> query);", ctrl);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V2_efcore_controller_is_partial_class()
    {
        var (config, output) = Setup("Ext2");
        config.ApiVersion = ApiVersion.V2;
        config.EndpointStyle = EndpointStyle.Controllers;
        config.DataAccess = DataAccessStyle.EfCore;
        var graph = SchemaGraphFixtures.SmallBlog();
        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var ctrl = File.ReadAllText(Path.Combine(output, "src", "Ext2", "Controllers", "PostsController.cs"));
            Assert.Contains("public sealed partial class PostsController", ctrl);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V1_controller_does_not_emit_hook()
    {
        var (config, output) = Setup("Ext3");
        config.ApiVersion = ApiVersion.V1;
        config.EndpointStyle = EndpointStyle.Controllers;
        config.DataAccess = DataAccessStyle.EfCore;
        var graph = SchemaGraphFixtures.SmallBlog();
        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var ctrl = File.ReadAllText(Path.Combine(output, "src", "Ext3", "Controllers", "PostsController.cs"));
            Assert.DoesNotContain("ConfigureListQuery", ctrl);
            Assert.DoesNotContain("partial class PostsController", ctrl);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V2_efcore_minimal_api_emits_configure_list_query_hook()
    {
        var (config, output) = Setup("Ext4");
        config.ApiVersion = ApiVersion.V2;
        config.EndpointStyle = EndpointStyle.MinimalApi;
        config.DataAccess = DataAccessStyle.EfCore;
        var graph = SchemaGraphFixtures.SmallBlog();
        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var endp = File.ReadAllText(Path.Combine(output, "src", "Ext4", "Endpoints", "PostsEndpoints.cs"));
            Assert.Contains("ConfigureListQuery(ref query);", endp);
            Assert.Contains("static partial void ConfigureListQuery(ref IQueryable<Post> query);", endp);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V2_efcore_minimal_api_class_is_static_partial()
    {
        var (config, output) = Setup("Ext5");
        config.ApiVersion = ApiVersion.V2;
        config.EndpointStyle = EndpointStyle.MinimalApi;
        config.DataAccess = DataAccessStyle.EfCore;
        var graph = SchemaGraphFixtures.SmallBlog();
        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var endp = File.ReadAllText(Path.Combine(output, "src", "Ext5", "Endpoints", "PostsEndpoints.cs"));
            Assert.Contains("public static partial class PostsEndpoints", endp);
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
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { }
    }

    private sealed class NullLog : IScaffoldLog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
