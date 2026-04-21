using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;

namespace ApiSmith.Generation.Tests;

public sealed class ServerSideDtoTests
{
    [Theory]
    [InlineData(ArchitectureStyle.Flat)]
    [InlineData(ArchitectureStyle.Clean)]
    [InlineData(ArchitectureStyle.Layered)]
    [InlineData(ArchitectureStyle.Onion)]
    [InlineData(ArchitectureStyle.VerticalSlice)]
    public void V2_dto_path_is_server_side_not_shared(ArchitectureStyle arch)
    {
        var config = new ApiSmithConfig { ProjectName = "Demo", ApiVersion = ApiVersion.V2, Architecture = arch };
        var layout = LayoutFactory.Create(arch);
        var path = layout.DtoPath(config, "dbo", "UserDto").Replace('\\', '/');
        Assert.DoesNotContain("Demo.Shared/", path);
    }

    [Fact]
    public void V2_emits_single_entity_dto_server_side_without_attributes()
    {
        var (config, output) = Setup("Dto1");
        config.ApiVersion = ApiVersion.V2;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);

            // Dto lives server-side (Flat default layout)
            var dtoPath = Path.Combine(output, "src", "Dto1", "Dtos", "PostDto.cs");
            Assert.True(File.Exists(dtoPath), $"Missing {dtoPath}");
            var content = File.ReadAllText(dtoPath);
            Assert.Contains("public sealed class PostDto", content);
            // No Create/Update variants under v2 — those are now in Shared as Requests.
            Assert.DoesNotContain("CreatePostDto", content);
            Assert.DoesNotContain("UpdatePostDto", content);
            // No attributes on Dto.
            Assert.DoesNotContain("[Required]", content);
            Assert.DoesNotContain("[StringLength", content);
            Assert.DoesNotContain("System.ComponentModel.DataAnnotations", content);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V1_still_emits_create_and_update_dto_variants()
    {
        var (config, output) = Setup("Dto2");
        config.ApiVersion = ApiVersion.V1;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var dtoPath = Path.Combine(output, "src", "Dto2", "Dtos", "PostDtos.cs");
            Assert.True(File.Exists(dtoPath));
            var content = File.ReadAllText(dtoPath);
            Assert.Contains("public sealed class PostDto", content);
            Assert.Contains("public sealed class CreatePostDto", content);
            Assert.Contains("public sealed class UpdatePostDto", content);
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
