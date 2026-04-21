using ApiSmith.Config;
using ApiSmith.Core.Pipeline;

namespace ApiSmith.Generation.Tests;

public sealed class RequestResponseShapeTests
{
    [Fact]
    public void V2_emits_paged_response_once()
    {
        var (config, output) = Setup("Paged1");
        config.ApiVersion = ApiVersion.V2;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var path = Path.Combine(output, "src", "Paged1.Shared", "Responses", "PagedResponse.cs");
            Assert.True(File.Exists(path), $"Missing {path}");
            var content = File.ReadAllText(path);
            Assert.Contains("namespace Paged1.Shared.Responses", content);
            Assert.Contains("public sealed class PagedResponse<T>", content);
            Assert.Contains("public IReadOnlyList<T> Items", content);
            Assert.Contains("public int TotalCount", content);
            Assert.Contains("public int TotalPages", content);
            Assert.Contains("public bool HasPreviousPage", content);
            Assert.Contains("public bool HasNextPage", content);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V1_does_not_emit_paged_response()
    {
        var (config, output) = Setup("Paged2");
        config.ApiVersion = ApiVersion.V1;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var path = Path.Combine(output, "src", "Paged2.Shared", "Responses", "PagedResponse.cs");
            Assert.False(File.Exists(path));
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V2_emits_requests_with_data_annotations()
    {
        var (config, output) = Setup("Req1");
        config.ApiVersion = ApiVersion.V2;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var path = Path.Combine(output, "src", "Req1.Shared", "Requests", "PostRequests.cs");
            Assert.True(File.Exists(path), $"Missing {path}");
            var content = File.ReadAllText(path);
            Assert.Contains("namespace Req1.Shared.Requests", content);
            Assert.Contains("using System.ComponentModel.DataAnnotations;", content);
            Assert.Contains("public sealed class CreatePostRequest", content);
            Assert.Contains("public sealed class UpdatePostRequest", content);
            // SmallBlog: posts.title is nvarchar(200) NOT NULL -> [Required] + [StringLength(200)]
            Assert.Contains("[Required]", content);
            Assert.Contains("[StringLength(200)]", content);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V1_does_not_emit_request_types()
    {
        var (config, output) = Setup("Req2");
        config.ApiVersion = ApiVersion.V1;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var requestsDir = Path.Combine(output, "src", "Req2.Shared", "Requests");
            Assert.False(Directory.Exists(requestsDir));
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V2_emits_responses_without_attributes()
    {
        var (config, output) = Setup("Resp1");
        config.ApiVersion = ApiVersion.V2;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var path = Path.Combine(output, "src", "Resp1.Shared", "Responses", "PostResponse.cs");
            Assert.True(File.Exists(path), $"Missing {path}");
            var content = File.ReadAllText(path);
            Assert.Contains("namespace Resp1.Shared.Responses", content);
            Assert.Contains("public sealed class PostResponse", content);
            Assert.DoesNotContain("[Required]", content);
            Assert.DoesNotContain("[StringLength", content);
            Assert.DoesNotContain("[Range", content);
            // Response has the same columns as the entity (including identity).
            Assert.Contains("public int Id", content);
            Assert.Contains("public string Title", content);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V1_does_not_emit_response_types()
    {
        var (config, output) = Setup("Resp3");
        config.ApiVersion = ApiVersion.V1;
        var graph = SchemaGraphFixtures.SmallBlog();
        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var responsesDir = Path.Combine(output, "src", "Resp3.Shared", "Responses");
            Assert.False(Directory.Exists(responsesDir));
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
