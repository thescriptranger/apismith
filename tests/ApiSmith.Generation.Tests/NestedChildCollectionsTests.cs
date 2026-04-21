using ApiSmith.Config;
using ApiSmith.Core.Pipeline;

namespace ApiSmith.Generation.Tests;

public sealed class NestedChildCollectionsTests
{
    [Fact]
    public void V2_response_includes_child_collection_when_flag_on()
    {
        var (config, output) = Setup("Nest1");
        config.ApiVersion = ApiVersion.V2;
        config.DataAccess = DataAccessStyle.EfCore;
        config.IncludeChildCollectionsInResponses = true;
        var graph = SchemaGraphFixtures.Relational();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var userResponse = File.ReadAllText(Path.Combine(output, "src", "Nest1.Shared", "Responses", "UserResponse.cs"));
            Assert.Contains("public IReadOnlyList<PostResponse> Posts", userResponse);
            Assert.Contains("System.Array.Empty<PostResponse>()", userResponse);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V2_response_omits_child_collection_when_flag_off()
    {
        var (config, output) = Setup("Nest2");
        config.ApiVersion = ApiVersion.V2;
        config.DataAccess = DataAccessStyle.EfCore;
        // IncludeChildCollectionsInResponses defaults to false
        var graph = SchemaGraphFixtures.Relational();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var userResponse = File.ReadAllText(Path.Combine(output, "src", "Nest2.Shared", "Responses", "UserResponse.cs"));
            Assert.DoesNotContain("IReadOnlyList<PostResponse>", userResponse);
            Assert.DoesNotContain("Posts", userResponse);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V2_mapper_populates_child_collection_when_flag_on()
    {
        var (config, output) = Setup("Nest3");
        config.ApiVersion = ApiVersion.V2;
        config.DataAccess = DataAccessStyle.EfCore;
        config.IncludeChildCollectionsInResponses = true;
        var graph = SchemaGraphFixtures.Relational();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var mappings = File.ReadAllText(Path.Combine(output, "src", "Nest3", "Mappings", "UserMappings.cs"));
            // The ToResponse(this User entity) method should populate Posts from entity.Posts.
            Assert.Contains("Posts = (entity.Posts ?? System.Linq.Enumerable.Empty<Post>()).Select(x => x.ToResponse()).ToList()", mappings);
            // When flag on, the entity-accepting ToResponse is a full method body, not expression-bodied.
            Assert.Contains("public static UserResponse ToResponse(this User entity)\n    {", mappings);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V2_mapper_omits_child_collection_when_flag_off()
    {
        var (config, output) = Setup("Nest4");
        config.ApiVersion = ApiVersion.V2;
        config.DataAccess = DataAccessStyle.EfCore;
        var graph = SchemaGraphFixtures.Relational();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var mappings = File.ReadAllText(Path.Combine(output, "src", "Nest4", "Mappings", "UserMappings.cs"));
            // Flag off: existing expression-bodied forward is emitted, no Posts assignment.
            Assert.Contains("public static UserResponse ToResponse(this User entity) => entity.ToDto().ToResponse();", mappings);
            Assert.DoesNotContain("entity.Posts", mappings);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V2_controller_list_includes_child_collection_when_flag_on()
    {
        var (config, output) = Setup("Nest5");
        config.ApiVersion = ApiVersion.V2;
        config.DataAccess = DataAccessStyle.EfCore;
        config.EndpointStyle = EndpointStyle.Controllers;
        config.IncludeChildCollectionsInResponses = true;
        var graph = SchemaGraphFixtures.Relational();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var ctrl = File.ReadAllText(Path.Combine(output, "src", "Nest5", "Controllers", "UsersController.cs"));
            // Include appears in both List and GetById.
            var includeCount = System.Text.RegularExpressions.Regex.Matches(ctrl, @"\.Include\(x => x\.Posts\)").Count;
            Assert.Equal(2, includeCount);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V2_controller_omits_include_when_flag_off()
    {
        var (config, output) = Setup("Nest6");
        config.ApiVersion = ApiVersion.V2;
        config.DataAccess = DataAccessStyle.EfCore;
        config.EndpointStyle = EndpointStyle.Controllers;
        var graph = SchemaGraphFixtures.Relational();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var ctrl = File.ReadAllText(Path.Combine(output, "src", "Nest6", "Controllers", "UsersController.cs"));
            Assert.DoesNotContain(".Include(x => x.Posts)", ctrl);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V2_minimal_api_includes_child_collection_when_flag_on()
    {
        var (config, output) = Setup("Nest7");
        config.ApiVersion = ApiVersion.V2;
        config.DataAccess = DataAccessStyle.EfCore;
        config.EndpointStyle = EndpointStyle.MinimalApi;
        config.IncludeChildCollectionsInResponses = true;
        var graph = SchemaGraphFixtures.Relational();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var endp = File.ReadAllText(Path.Combine(output, "src", "Nest7", "Endpoints", "UsersEndpoints.cs"));
            var includeCount = System.Text.RegularExpressions.Regex.Matches(endp, @"\.Include\(x => x\.Posts\)").Count;
            Assert.Equal(2, includeCount);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V2_minimal_api_omits_include_when_flag_off()
    {
        var (config, output) = Setup("Nest8");
        config.ApiVersion = ApiVersion.V2;
        config.DataAccess = DataAccessStyle.EfCore;
        config.EndpointStyle = EndpointStyle.MinimalApi;
        var graph = SchemaGraphFixtures.Relational();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var endp = File.ReadAllText(Path.Combine(output, "src", "Nest8", "Endpoints", "UsersEndpoints.cs"));
            Assert.DoesNotContain(".Include(x => x.Posts)", endp);
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
