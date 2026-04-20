using ApiSmith.Config;
using ApiSmith.Core.Pipeline;

namespace ApiSmith.Generation.Tests;

public sealed class PartialClassHookTests
{
    [Fact]
    public void Validators_are_partial_with_custom_extension_hook()
    {
        var (config, output) = Setup("Part1");
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var validators = File.ReadAllText(Path.Combine(output, "src", "Part1", "Validators", "PostDtoValidators.cs"));
            // Partial class declaration
            Assert.Contains("public sealed partial class CreatePostDtoValidator", validators);
            Assert.Contains("public sealed partial class UpdatePostDtoValidator", validators);
            // Hook declarations
            Assert.Contains("partial void ExtendValidate(CreatePostDto dto, ValidationResult result);", validators);
            Assert.Contains("partial void ExtendValidate(UpdatePostDto dto, ValidationResult result);", validators);
            // Hook invocation before return in Validate
            Assert.Contains("ExtendValidate(dto, result);", validators);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void Mappers_are_partial_static_with_onmapped_hook()
    {
        var (config, output) = Setup("Part2");
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var mapper = File.ReadAllText(Path.Combine(output, "src", "Part2", "Mappings", "PostMappings.cs"));
            // Class declaration — static partial
            Assert.Contains("public static partial class PostMappings", mapper);
            // Hook called in ToDto just before return
            Assert.Contains("OnMapped(entity, dto);", mapper);
            // Hook declaration — static partial void
            Assert.Contains("static partial void OnMapped(Post entity, PostDto dto);", mapper);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void DbContext_is_partial()
    {
        var (config, output) = Setup("Part3");
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var ctx = File.ReadAllText(Path.Combine(output, "src", "Part3", "Data", "Part3DbContext.cs"));
            Assert.Contains("public sealed partial class Part3DbContext : DbContext", ctx);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void Dapper_repositories_are_partial()
    {
        var (config, output) = Setup("Part4");
        config.DataAccess = DataAccessStyle.Dapper;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var repo = File.ReadAllText(Path.Combine(output, "src", "Part4", "Data", "PostRepository.cs"));
            Assert.Contains("public sealed partial class PostRepository", repo);
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
