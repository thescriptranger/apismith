using ApiSmith.Config;
using ApiSmith.Core.Pipeline;

namespace ApiSmith.Generation.Tests;

public sealed class AuthEnforcementTests
{
    [Theory]
    [InlineData(AuthStyle.JwtBearer)]
    [InlineData(AuthStyle.Auth0)]
    [InlineData(AuthStyle.AzureAd)]
    [InlineData(AuthStyle.ApiKey)]
    public void Controllers_emit_authorize_attribute_when_auth_is_configured(AuthStyle auth)
    {
        var (config, output) = Setup($"Auth{auth}");
        config.Auth = auth;
        config.EndpointStyle = EndpointStyle.Controllers;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var ctrl = File.ReadAllText(Path.Combine(output, "src", $"Auth{auth}", "Controllers", "PostsController.cs"));
            Assert.Contains("[Authorize]", ctrl);
            Assert.Contains("using Microsoft.AspNetCore.Authorization;", ctrl);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void Controllers_do_not_emit_authorize_when_auth_is_none()
    {
        var (config, output) = Setup("AuthNone");
        config.Auth = AuthStyle.None;
        config.EndpointStyle = EndpointStyle.Controllers;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var ctrl = File.ReadAllText(Path.Combine(output, "src", "AuthNone", "Controllers", "PostsController.cs"));
            Assert.DoesNotContain("[Authorize]", ctrl);
        }
        finally { CleanupBestEffort(output); }
    }

    [Theory]
    [InlineData(AuthStyle.JwtBearer)]
    [InlineData(AuthStyle.ApiKey)]
    public void Minimal_api_groups_require_authorization_when_auth_is_configured(AuthStyle auth)
    {
        var (config, output) = Setup($"MinAuth{auth}");
        config.Auth = auth;
        config.EndpointStyle = EndpointStyle.MinimalApi;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var endp = File.ReadAllText(Path.Combine(output, "src", $"MinAuth{auth}", "Endpoints", "PostsEndpoints.cs"));
            Assert.Contains(".RequireAuthorization()", endp);
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
