using ApiSmith.Config;
using ApiSmith.Core.Pipeline;

namespace ApiSmith.Generation.Tests;

public sealed class ValidatorDiTests
{
    [Fact]
    public void V1_validators_folder_emits_ivalidator_interface()
    {
        var (config, output) = Setup("Val1");
        config.ApiVersion = ApiVersion.V1;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var vrPath = Path.Combine(output, "src", "Val1", "Validators", "ValidationResult.cs");
            var content = File.ReadAllText(vrPath);
            Assert.Contains("public interface IValidator<in TDto>", content);
            Assert.Contains("ValidationResult Validate(TDto dto);", content);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V2_validators_folder_emits_ivalidator_interface_too()
    {
        var (config, output) = Setup("Val2");
        config.ApiVersion = ApiVersion.V2;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var vrPath = Path.Combine(output, "src", "Val2", "Validators", "ValidationResult.cs");
            var content = File.ReadAllText(vrPath);
            Assert.Contains("public interface IValidator<in TDto>", content);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void Generated_validators_implement_ivalidator_interface()
    {
        var (config, output) = Setup("Val3");
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var validators = File.ReadAllText(Path.Combine(output, "src", "Val3", "Validators", "PostDtoValidators.cs"));
            Assert.Contains("public sealed partial class CreatePostDtoValidator : IValidator<CreatePostDto>", validators);
            Assert.Contains("public sealed partial class UpdatePostDtoValidator : IValidator<UpdatePostDto>", validators);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void Program_cs_registers_each_validator_as_scoped()
    {
        var (config, output) = Setup("Val4");
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var program = File.ReadAllText(Path.Combine(output, "src", "Val4", "Program.cs"));
            Assert.Contains("AddScoped<IValidator<CreatePostDto>, CreatePostDtoValidator>()", program);
            Assert.Contains("AddScoped<IValidator<UpdatePostDto>, UpdatePostDtoValidator>()", program);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void Controller_injects_validators_instead_of_newing_them()
    {
        var (config, output) = Setup("Val5");
        config.EndpointStyle = EndpointStyle.Controllers;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var ctrl = File.ReadAllText(Path.Combine(output, "src", "Val5", "Controllers", "PostsController.cs"));
            // Constructor injects the validators
            Assert.Contains("IValidator<CreatePostDto>", ctrl);
            Assert.Contains("IValidator<UpdatePostDto>", ctrl);
            // No more inline new
            Assert.DoesNotContain("new CreatePostDtoValidator()", ctrl);
            Assert.DoesNotContain("new UpdatePostDtoValidator()", ctrl);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void Minimal_api_injects_validators_as_handler_parameters()
    {
        var (config, output) = Setup("Val6");
        config.EndpointStyle = EndpointStyle.MinimalApi;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var endp = File.ReadAllText(Path.Combine(output, "src", "Val6", "Endpoints", "PostsEndpoints.cs"));
            Assert.Contains("IValidator<CreatePostDto>", endp);
            Assert.DoesNotContain("new CreatePostDtoValidator()", endp);
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
