using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;

namespace ApiSmith.Generation.Tests;

public sealed class SharedProjectTests
{
    [Fact]
    public void V2_scaffold_writes_shared_csproj_to_disk()
    {
        var (config, output) = Setup("Shared1");
        config.ApiVersion = ApiVersion.V2;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var path = Path.Combine(output, "src", "Shared1.Shared", "Shared1.Shared.csproj");
            Assert.True(File.Exists(path), $"Missing Shared csproj at {path}");
            var content = File.ReadAllText(path);
            Assert.Contains("<PackageId>Shared1.Shared</PackageId>", content);
        }
        finally
        {
            CleanupBestEffort(output);
        }
    }


    [Fact]
    public void Shared_csproj_is_packable_with_expected_metadata()
    {
        var config = new ApiSmithConfig { ProjectName = "Demo", ApiVersion = ApiVersion.V2 };
        var csproj = CsprojTemplates.SharedClassLibrary(config);

        Assert.Contains("<IsPackable>true</IsPackable>", csproj);
        Assert.Contains("<PackageId>Demo.Shared</PackageId>", csproj);
        Assert.Contains("<Version>1.0.0</Version>", csproj);
        Assert.Contains("<Description>API contracts for Demo.</Description>", csproj);
        Assert.Contains("<GeneratePackageOnBuild>false</GeneratePackageOnBuild>", csproj);

        // Commented-out placeholders.
        Assert.Contains("<!-- <PackageProjectUrl>", csproj);
        Assert.Contains("<!-- <PackageLicenseExpression>", csproj);
        Assert.Contains("<!-- <RepositoryUrl>", csproj);

        // BCL-only: no PackageReference elements.
        Assert.DoesNotContain("<PackageReference", csproj);

        // Strict mode inherited like every other emitted csproj.
        Assert.Contains("<TreatWarningsAsErrors>true</TreatWarningsAsErrors>", csproj);
        Assert.Contains("<Nullable>enable</Nullable>", csproj);
    }

    [Theory]
    [InlineData(ArchitectureStyle.Flat)]
    [InlineData(ArchitectureStyle.Clean)]
    [InlineData(ArchitectureStyle.Layered)]
    [InlineData(ArchitectureStyle.Onion)]
    [InlineData(ArchitectureStyle.VerticalSlice)]
    public void V2_projects_list_includes_shared(ArchitectureStyle arch)
    {
        var config = new ApiSmithConfig { ProjectName = "Demo", Architecture = arch, ApiVersion = ApiVersion.V2 };
        var layout = LayoutFactory.Create(arch);
        var projects = layout.Projects(config);
        Assert.Contains(projects, p => p.AssemblyName == "Demo.Shared");
    }

    [Fact]
    public void V2_sln_includes_shared_project()
    {
        var (config, output) = Setup("Shared2");
        config.ApiVersion = ApiVersion.V2;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var sln = File.ReadAllText(Path.Combine(output, "Shared2.sln"));
            // SLN file format uses backslashes by design (per Visual Studio spec).
            Assert.Contains(@"src\Shared2.Shared\Shared2.Shared.csproj", sln);
        }
        finally { CleanupBestEffort(output); }
    }

    [Theory]
    [InlineData(ArchitectureStyle.Flat)]
    [InlineData(ArchitectureStyle.Clean)]
    [InlineData(ArchitectureStyle.Layered)]
    [InlineData(ArchitectureStyle.Onion)]
    [InlineData(ArchitectureStyle.VerticalSlice)]
    public void V1_projects_list_excludes_shared(ArchitectureStyle arch)
    {
        var config = new ApiSmithConfig { ProjectName = "Demo", Architecture = arch, ApiVersion = ApiVersion.V1 };
        var layout = LayoutFactory.Create(arch);
        var projects = layout.Projects(config);
        Assert.DoesNotContain(projects, p => p.AssemblyName == "Demo.Shared");
    }

    [Fact]
    public void V2_flat_dto_path_is_server_side()
    {
        var config = new ApiSmithConfig { ProjectName = "Demo", ApiVersion = ApiVersion.V2, Architecture = ArchitectureStyle.Flat };
        var layout = LayoutFactory.Create(ArchitectureStyle.Flat);
        var path = layout.DtoPath(config, "dbo", "UserDtos");
        var ns = layout.DtoNamespace(config, "dbo");
        Assert.Equal("src/Demo/Dtos/UserDtos.cs", path.Replace('\\', '/'));
        Assert.Equal("Demo.Dtos", ns);
    }

    [Fact]
    public void V1_flat_dto_path_unchanged()
    {
        var config = new ApiSmithConfig { ProjectName = "Demo", ApiVersion = ApiVersion.V1, Architecture = ArchitectureStyle.Flat };
        var layout = LayoutFactory.Create(ArchitectureStyle.Flat);
        var path = layout.DtoPath(config, "dbo", "UserDtos").Replace('\\', '/');
        Assert.Contains("/Dtos/", path);
        Assert.DoesNotContain(".Shared/", path);
    }

    [Theory]
    [InlineData(ArchitectureStyle.Clean)]
    [InlineData(ArchitectureStyle.Layered)]
    [InlineData(ArchitectureStyle.Onion)]
    [InlineData(ArchitectureStyle.VerticalSlice)]
    public void V2_dto_paths_are_server_side_for_every_architecture(ArchitectureStyle arch)
    {
        var config = new ApiSmithConfig { ProjectName = "Demo", ApiVersion = ApiVersion.V2, Architecture = arch };
        var layout = LayoutFactory.Create(arch);
        var path = layout.DtoPath(config, "dbo", "X").Replace('\\', '/');
        var ns = layout.DtoNamespace(config, "dbo");
        Assert.DoesNotContain("Demo.Shared/", path);
        Assert.DoesNotContain("Shared", ns);
    }

    [Fact]
    public void V1_dtos_have_no_data_annotations()
    {
        var (config, output) = Setup("Attr3");
        config.ApiVersion = ApiVersion.V1;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            // V1 DTOs live at src/Attr3/Dtos/ (not Shared).
            var userDtos = File.ReadAllText(Path.Combine(output, "src", "Attr3", "Dtos", "UserDtos.cs"));
            Assert.DoesNotContain("DataAnnotations", userDtos);
            Assert.DoesNotContain("[Required]", userDtos);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V1_dtos_unaffected_by_range()
    {
        var (config, output) = Setup("Attr5");
        config.ApiVersion = ApiVersion.V1;
        var graph = SchemaGraphFixtures.RelationalWithCheck();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var orderDtos = File.ReadAllText(Path.Combine(output, "src", "Attr5", "Dtos", "OrderDtos.cs"));
            Assert.DoesNotContain("[Range", orderDtos);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V2_validation_error_lives_in_shared_errors()
    {
        var (config, output) = Setup("Err1");
        config.ApiVersion = ApiVersion.V2;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var errPath = Path.Combine(output, "src", "Err1.Shared", "Errors", "ValidationError.cs");
            Assert.True(File.Exists(errPath), $"Missing {errPath}");
            var errContent = File.ReadAllText(errPath);
            Assert.Contains("namespace Err1.Shared.Errors", errContent);
            Assert.Contains("public sealed record ValidationError", errContent);

            // V2 server-side ValidationResult file still exists but must NOT redefine ValidationError.
            var vrPath = Path.Combine(output, "src", "Err1", "Validators", "ValidationResult.cs");
            Assert.True(File.Exists(vrPath));
            var vrContent = File.ReadAllText(vrPath);
            Assert.DoesNotContain("record ValidationError", vrContent);
            Assert.Contains("using Err1.Shared.Errors;", vrContent);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V1_validation_error_stays_server_side_unchanged()
    {
        var (config, output) = Setup("Err2");
        config.ApiVersion = ApiVersion.V1;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            // V1: no Shared/Errors/ directory.
            Assert.False(Directory.Exists(Path.Combine(output, "src", "Err2.Shared", "Errors")));

            // V1: ValidationError still inlined in the server-side ValidationResult file.
            var vrPath = Path.Combine(output, "src", "Err2", "Validators", "ValidationResult.cs");
            var vrContent = File.ReadAllText(vrPath);
            Assert.Contains("record ValidationError", vrContent);
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
