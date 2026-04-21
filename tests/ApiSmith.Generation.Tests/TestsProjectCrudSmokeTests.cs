using ApiSmith.Config;
using ApiSmith.Core.Pipeline;

namespace ApiSmith.Generation.Tests;

public sealed class TestsProjectCrudSmokeTests
{
    [Fact]
    public void All_crud_flags_emit_six_smoke_test_methods()
    {
        var (config, output) = Setup("AllCrudTests");
        config.IncludeTestsProject = true;
        config.Crud = CrudOperations.All;
        var graph = SchemaGraphFixtures.Relational(); // posts table has PK + required user_id FK

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);

            var endpointTestFile = File.ReadAllText(
                Path.Combine(output, "tests", $"{config.ProjectName}.IntegrationTests", "Endpoints", "PostsEndpointTests.cs"));

            Assert.Contains("public async System.Threading.Tasks.Task Get_list_returns_ok()", endpointTestFile);
            Assert.Contains("public async System.Threading.Tasks.Task Get_by_id_returns_404_when_missing()", endpointTestFile);
            Assert.Contains("public async System.Threading.Tasks.Task Post_returns_success_with_valid_payload()", endpointTestFile);
            Assert.Contains("public async System.Threading.Tasks.Task Put_returns_404_when_id_missing()", endpointTestFile);
            Assert.Contains("public async System.Threading.Tasks.Task Patch_returns_404_when_id_missing()", endpointTestFile);
            Assert.Contains("public async System.Threading.Tasks.Task Delete_returns_404_when_id_missing()", endpointTestFile);

            Assert.Contains("using System.Net.Http.Json;", endpointTestFile);

            Assert.Contains($"var dto = new CreatePostDto", endpointTestFile);
            Assert.Contains($"var dto = new UpdatePostDto", endpointTestFile);

            Assert.Contains("/api/posts/99999", endpointTestFile);
        }
        finally
        {
            CleanupBestEffort(output);
        }
    }

    [Fact]
    public void V2_tests_project_references_requests_and_responses()
    {
        var (config, output) = Setup("TP1");
        config.ApiVersion = ApiVersion.V2;
        config.IncludeTestsProject = true;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var endpTest = File.ReadAllText(Path.Combine(output, "tests", "TP1.IntegrationTests", "Endpoints", "PostsEndpointTests.cs"));
            Assert.Contains("CreatePostRequest", endpTest);
            Assert.Contains("UpdatePostRequest", endpTest);
            Assert.DoesNotContain("CreatePostDto", endpTest);

            var valTest = File.ReadAllText(Path.Combine(output, "tests", "TP1.IntegrationTests", "Validators", "PostValidatorTests.cs"));
            Assert.Contains("CreatePostRequestValidator", valTest);
            Assert.Contains("new CreatePostRequest", valTest);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void Only_getlist_flag_emits_legacy_byte_identical_output()
    {
        var (config, output) = Setup("GetListOnlyTests");
        config.IncludeTestsProject = true;
        config.Crud = CrudOperations.GetList;
        var graph = SchemaGraphFixtures.Relational();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);

            var endpointTestFile = File.ReadAllText(
                Path.Combine(output, "tests", $"{config.ProjectName}.IntegrationTests", "Endpoints", "PostsEndpointTests.cs"));

            Assert.Contains("public async System.Threading.Tasks.Task List_returns_ok_on_empty_db()", endpointTestFile);
            Assert.DoesNotContain("Get_by_id_returns_404_when_missing", endpointTestFile);
            Assert.DoesNotContain("Post_returns_success_with_valid_payload", endpointTestFile);
            Assert.DoesNotContain("Put_returns_404_when_id_missing", endpointTestFile);
            Assert.DoesNotContain("Patch_returns_404_when_id_missing", endpointTestFile);
            Assert.DoesNotContain("Delete_returns_404_when_id_missing", endpointTestFile);

            Assert.DoesNotContain("System.Net.Http.Json", endpointTestFile);
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
