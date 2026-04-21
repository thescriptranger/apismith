using ApiSmith.Config;
using ApiSmith.Core.Pipeline;

namespace ApiSmith.Generation.Tests;

public sealed class ControllerShapeV2Tests
{
    [Fact]
    public void V2_controller_list_returns_paged_response()
    {
        var (config, output) = Setup("Ctrl1");
        config.ApiVersion = ApiVersion.V2;
        config.EndpointStyle = EndpointStyle.Controllers;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var ctrl = File.ReadAllText(Path.Combine(output, "src", "Ctrl1", "Controllers", "PostsController.cs"));
            Assert.Contains("Task<ActionResult<PagedResponse<PostResponse>>> List(", ctrl);
            Assert.Contains("int page = 1, int pageSize = 50", ctrl);
            Assert.Contains(".Skip((page - 1) * pageSize)", ctrl);
            Assert.Contains(".Take(pageSize)", ctrl);
            Assert.Contains("new PagedResponse<PostResponse>", ctrl);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V2_controller_create_accepts_request_returns_response()
    {
        var (config, output) = Setup("Ctrl2");
        config.ApiVersion = ApiVersion.V2;
        config.EndpointStyle = EndpointStyle.Controllers;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var ctrl = File.ReadAllText(Path.Combine(output, "src", "Ctrl2", "Controllers", "PostsController.cs"));
            Assert.Contains("Task<ActionResult<PostResponse>> Create(CreatePostRequest request", ctrl);
            Assert.Contains("IValidator<CreatePostRequest> _createValidator", ctrl);
            Assert.Contains("IValidator<UpdatePostRequest> _updateValidator", ctrl);
            Assert.Contains("entity.ToResponse()", ctrl);
            Assert.Contains("request.ToEntity()", ctrl);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V2_controller_update_uses_update_from_request()
    {
        var (config, output) = Setup("Ctrl3");
        config.ApiVersion = ApiVersion.V2;
        config.EndpointStyle = EndpointStyle.Controllers;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var ctrl = File.ReadAllText(Path.Combine(output, "src", "Ctrl3", "Controllers", "PostsController.cs"));
            Assert.Contains("Update(int id, UpdatePostRequest request", ctrl);
            Assert.Contains("entity.UpdateFromRequest(request)", ctrl);
            Assert.DoesNotContain("UpdateFromDto", ctrl);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V2_controller_get_by_id_returns_response()
    {
        var (config, output) = Setup("Ctrl4");
        config.ApiVersion = ApiVersion.V2;
        config.EndpointStyle = EndpointStyle.Controllers;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var ctrl = File.ReadAllText(Path.Combine(output, "src", "Ctrl4", "Controllers", "PostsController.cs"));
            Assert.Contains("Task<ActionResult<PostResponse>> GetById(", ctrl);
            Assert.Contains("entity.ToResponse()", ctrl);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V1_controller_preserves_dto_signatures()
    {
        var (config, output) = Setup("Ctrl5");
        config.ApiVersion = ApiVersion.V1;
        config.EndpointStyle = EndpointStyle.Controllers;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var ctrl = File.ReadAllText(Path.Combine(output, "src", "Ctrl5", "Controllers", "PostsController.cs"));
            Assert.Contains("Task<ActionResult<IReadOnlyList<PostDto>>> List(CancellationToken ct)", ctrl);
            Assert.Contains("Create(CreatePostDto dto", ctrl);
            Assert.Contains("IValidator<CreatePostDto>", ctrl);
            Assert.Contains("IValidator<UpdatePostDto>", ctrl);
            Assert.Contains("entity.UpdateFromDto(dto)", ctrl);
            Assert.DoesNotContain("PagedResponse", ctrl);
            Assert.DoesNotContain("PostResponse", ctrl);
            Assert.DoesNotContain("CreatePostRequest", ctrl);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V2_minimal_api_list_returns_paged_response()
    {
        var (config, output) = Setup("Min1");
        config.ApiVersion = ApiVersion.V2;
        config.EndpointStyle = EndpointStyle.MinimalApi;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var endp = File.ReadAllText(Path.Combine(output, "src", "Min1", "Endpoints", "PostsEndpoints.cs"));
            Assert.Contains("PagedResponse<PostResponse>", endp);
            Assert.Contains("int page", endp);
            Assert.Contains("int pageSize", endp);
            Assert.Contains("CreatePostRequest", endp);
            Assert.Contains("IValidator<CreatePostRequest>", endp);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V1_minimal_api_uses_dto_signatures()
    {
        var (config, output) = Setup("Min2");
        config.ApiVersion = ApiVersion.V1;
        config.EndpointStyle = EndpointStyle.MinimalApi;
        var graph = SchemaGraphFixtures.SmallBlog();
        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var endp = File.ReadAllText(Path.Combine(output, "src", "Min2", "Endpoints", "PostsEndpoints.cs"));
            Assert.Contains("CreatePostDto", endp);
            Assert.Contains("IValidator<CreatePostDto>", endp);
            Assert.DoesNotContain("PagedResponse", endp);
            Assert.DoesNotContain("PostResponse", endp);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V2_dapper_controller_list_slices_client_side()
    {
        var (config, output) = Setup("Ctrl6");
        config.ApiVersion = ApiVersion.V2;
        config.EndpointStyle = EndpointStyle.Controllers;
        config.DataAccess = DataAccessStyle.Dapper;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var ctrl = File.ReadAllText(Path.Combine(output, "src", "Ctrl6", "Controllers", "PostsController.cs"));
            Assert.Contains("Task<ActionResult<PagedResponse<PostResponse>>> List(", ctrl);
            Assert.Contains("int page = 1, int pageSize = 50", ctrl);
            Assert.Contains("_repo.ListAsync", ctrl);
            Assert.Contains(".Skip((page - 1) * pageSize)", ctrl);
            Assert.Contains(".Take(pageSize)", ctrl);
            Assert.Contains("new PagedResponse<PostResponse>", ctrl);
            Assert.Contains("CreatePostRequest", ctrl);
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
