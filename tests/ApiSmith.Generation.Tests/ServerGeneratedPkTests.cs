using ApiSmith.Config;
using ApiSmith.Core.Pipeline;

namespace ApiSmith.Generation.Tests;

public sealed class ServerGeneratedPkTests
{
    [Fact]
    public void V2_create_request_omits_guid_pk_with_default()
    {
        var (config, output) = Setup("Pk1");
        config.ApiVersion = ApiVersion.V2;
        var graph = SchemaGraphFixtures.GuidPkEntity();
        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var req = File.ReadAllText(Path.Combine(output, "src", "Pk1.Shared", "Requests", "GenderRequests.cs"));
            Assert.Contains("public sealed class CreateGenderRequest", req);
            Assert.DoesNotContain("GenderId", req); // PK skipped for Create and Update
            Assert.Contains("public string Name", req);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V1_create_dto_omits_guid_pk_with_default()
    {
        var (config, output) = Setup("Pk2");
        config.ApiVersion = ApiVersion.V1;
        var graph = SchemaGraphFixtures.GuidPkEntity();
        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var dto = File.ReadAllText(Path.Combine(output, "src", "Pk2", "Dtos", "GenderDtos.cs"));
            // Read Dto keeps PK
            Assert.Contains("public sealed class GenderDto", dto);
            // Write Dtos skip it
            var createBlock = ExtractBlock(dto, "public sealed class CreateGenderDto");
            Assert.DoesNotContain("GenderId", createBlock);
            var updateBlock = ExtractBlock(dto, "public sealed class UpdateGenderDto");
            Assert.DoesNotContain("GenderId", updateBlock);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V2_mapper_to_entity_does_not_assign_server_generated_pk()
    {
        var (config, output) = Setup("Pk3");
        config.ApiVersion = ApiVersion.V2;
        var graph = SchemaGraphFixtures.GuidPkEntity();
        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var map = File.ReadAllText(Path.Combine(output, "src", "Pk3", "Mappings", "GenderMappings.cs"));
            // The generated ToEntity body must not try to set GenderId (it doesn't exist on the request).
            var toEntityBlock = ExtractBlock(map, "public static Gender ToEntity(this CreateGenderRequest request)");
            Assert.DoesNotContain("GenderId =", toEntityBlock);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V2_validator_skips_server_generated_pk()
    {
        var (config, output) = Setup("Pk4");
        config.ApiVersion = ApiVersion.V2;
        var graph = SchemaGraphFixtures.GuidPkEntity();
        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var val = File.ReadAllText(Path.Combine(output, "src", "Pk4", "Validators", "GenderValidators.cs"));
            Assert.DoesNotContain("dto.GenderId", val);
        }
        finally { CleanupBestEffort(output); }
    }

    // Negative guard: int-identity fixture (SmallBlog) must keep working — PK correctly omitted from write payloads, just via the IsIdentity branch.
    [Fact]
    public void V2_identity_pk_still_omitted_from_create_request()
    {
        var (config, output) = Setup("Pk5");
        config.ApiVersion = ApiVersion.V2;
        var graph = SchemaGraphFixtures.SmallBlog();
        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var req = File.ReadAllText(Path.Combine(output, "src", "Pk5.Shared", "Requests", "PostRequests.cs"));
            var createBlock = ExtractBlock(req, "public sealed class CreatePostRequest");
            Assert.DoesNotContain("public int Id", createBlock);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V2_dapper_repository_excludes_server_generated_pk_from_insert_sql()
    {
        var (config, output) = Setup("Pk6");
        config.ApiVersion = ApiVersion.V2;
        config.DataAccess = DataAccessStyle.Dapper;
        var graph = SchemaGraphFixtures.GuidPkEntity();
        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var repo = File.ReadAllText(Path.Combine(output, "src", "Pk6", "Data", "GenderRepository.cs"));
            // INSERT column list must NOT include [gender_id]
            Assert.DoesNotContain("[gender_id], [name]", repo);
            Assert.DoesNotContain("[gender_id],[name]", repo);
            // But it should include [name]
            Assert.Contains("[name]", repo);
            // And should use OUTPUT to round-trip the generated PK back
            Assert.Contains("OUTPUT INSERTED.[gender_id]", repo);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V2_efcore_marks_server_generated_guid_pk_as_value_generated_on_add()
    {
        var (config, output) = Setup("Pk7");
        config.ApiVersion = ApiVersion.V2;
        config.DataAccess = DataAccessStyle.EfCore;
        var graph = SchemaGraphFixtures.GuidPkEntity();
        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var dbctx = File.ReadAllText(Path.Combine(output, "src", "Pk7", "Data", "Pk7DbContext.cs"));
            Assert.Contains(".ValueGeneratedOnAdd();", dbctx);
        }
        finally { CleanupBestEffort(output); }
    }

    // Regression guard: identity PK still gets ValueGeneratedOnAdd (SmallBlog / int-identity fixture).
    [Fact]
    public void V2_efcore_identity_pk_still_value_generated_on_add()
    {
        var (config, output) = Setup("Pk8");
        config.ApiVersion = ApiVersion.V2;
        config.DataAccess = DataAccessStyle.EfCore;
        var graph = SchemaGraphFixtures.SmallBlog();
        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var dbctx = File.ReadAllText(Path.Combine(output, "src", "Pk8", "Data", "Pk8DbContext.cs"));
            Assert.Contains(".ValueGeneratedOnAdd();", dbctx);
        }
        finally { CleanupBestEffort(output); }
    }

    private static string ExtractBlock(string source, string header)
    {
        var start = source.IndexOf(header, System.StringComparison.Ordinal);
        if (start < 0) return string.Empty;
        // A block ends at the first line that starts with "}" (class close) or "    };" (method/init close) or "    }" (method close).
        var endCandidates = new[]
        {
            source.IndexOf("\n}", start, System.StringComparison.Ordinal),
            source.IndexOf("\n    };", start, System.StringComparison.Ordinal),
            source.IndexOf("\n    }\n", start, System.StringComparison.Ordinal),
        };
        var end = -1;
        foreach (var candidate in endCandidates)
        {
            if (candidate < 0) continue;
            if (end < 0 || candidate < end) end = candidate;
        }
        return end < 0 ? source[start..] : source[start..end];
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
