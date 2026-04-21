using ApiSmith.Config;
using ApiSmith.Core.Pipeline;

namespace ApiSmith.Generation.Tests;

public sealed class MapperFiveMethodTests
{
    [Fact]
    public void V2_mapper_emits_five_methods()
    {
        var (config, output) = Setup("Map1");
        config.ApiVersion = ApiVersion.V2;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var mapper = File.ReadAllText(Path.Combine(output, "src", "Map1", "Mappings", "PostMappings.cs"));
            Assert.Contains("public static Post ToEntity(this CreatePostRequest request)", mapper);
            Assert.Contains("public static void UpdateFromRequest(this Post entity, UpdatePostRequest request)", mapper);
            Assert.Contains("public static PostDto ToDto(this Post entity)", mapper);
            Assert.Contains("public static PostResponse ToResponse(this PostDto dto)", mapper);
            Assert.Contains("public static PostResponse ToResponse(this Post entity) => entity.ToDto().ToResponse();", mapper);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V2_mapper_has_onmapped_hooks_for_both_stages()
    {
        var (config, output) = Setup("Map2");
        config.ApiVersion = ApiVersion.V2;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var mapper = File.ReadAllText(Path.Combine(output, "src", "Map2", "Mappings", "PostMappings.cs"));
            Assert.Contains("static partial void OnMapped(Post entity, PostDto dto);", mapper);
            Assert.Contains("static partial void OnMapped(PostDto dto, PostResponse response);", mapper);
            Assert.Contains("OnMapped(entity, dto);", mapper);
            Assert.Contains("OnMapped(dto, response);", mapper);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V1_mapper_unchanged_three_method_shape()
    {
        var (config, output) = Setup("Map3");
        config.ApiVersion = ApiVersion.V1;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var mapper = File.ReadAllText(Path.Combine(output, "src", "Map3", "Mappings", "PostMappings.cs"));
            Assert.Contains("public static PostDto ToDto(this Post entity)", mapper);
            Assert.Contains("public static Post ToEntity(this CreatePostDto dto)", mapper);
            Assert.Contains("public static void UpdateFromDto(this Post entity, UpdatePostDto dto)", mapper);
            Assert.DoesNotContain("CreatePostRequest", mapper);
            Assert.DoesNotContain("UpdatePostRequest", mapper);
            Assert.DoesNotContain("PostResponse", mapper);
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
