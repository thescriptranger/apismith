using ApiSmith.Config;
using ApiSmith.Core.Model;
using ApiSmith.Core.Pipeline;

namespace ApiSmith.Generation.Tests;

public sealed class SharedEnumTests
{
    [Fact]
    public void V2_enum_emitted_for_in_constraint_on_dto_column()
    {
        var (config, output) = Setup("Enum1");
        config.ApiVersion = ApiVersion.V2;
        var graph = BuildGraphWithStatusEnum();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var path = Path.Combine(output, "src", "Enum1.Shared", "Enums", "Status.cs");
            Assert.True(File.Exists(path), $"Missing enum at {path}");
            var content = File.ReadAllText(path);
            Assert.Contains("namespace Enum1.Shared.Enums", content);
            Assert.Contains("public enum Status", content);
            Assert.Contains("Draft", content);
            Assert.Contains("Published", content);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V1_does_not_emit_enums()
    {
        var (config, output) = Setup("Enum2");
        config.ApiVersion = ApiVersion.V1;
        var graph = BuildGraphWithStatusEnum();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var enumsDir = Path.Combine(output, "src", "Enum2.Shared", "Enums");
            Assert.False(Directory.Exists(enumsDir));
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V2_enum_not_emitted_for_identity_column()
    {
        var (config, output) = Setup("Enum3");
        config.ApiVersion = ApiVersion.V2;
        var graph = BuildGraphWithCheckOnIdentity();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var enumsDir = Path.Combine(output, "src", "Enum3.Shared", "Enums");
            if (Directory.Exists(enumsDir))
                Assert.Empty(Directory.GetFiles(enumsDir));
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V2_case_colliding_values_do_not_emit_enum()
    {
        var (config, output) = Setup("Enum4");
        config.ApiVersion = ApiVersion.V2;
        var graph = BuildGraphWithCaseCollidingValues();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var enumsDir = Path.Combine(output, "src", "Enum4.Shared", "Enums");
            if (Directory.Exists(enumsDir))
                Assert.Empty(Directory.GetFiles(enumsDir));
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V2_dto_property_for_in_constrained_column_uses_enum_type()
    {
        var (config, output) = Setup("Enum5");
        config.ApiVersion = ApiVersion.V2;
        var graph = BuildGraphWithStatusEnum();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var dtos = File.ReadAllText(Path.Combine(output, "src", "Enum5", "Dtos", "OrderDto.cs"));

            Assert.Contains("using Enum5.Shared.Enums;", dtos);
            Assert.Contains("public Status Status { get; set; }", dtos);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V2_mapper_converts_enum_and_string_at_boundary()
    {
        var (config, output) = Setup("Enum6");
        config.ApiVersion = ApiVersion.V2;
        var graph = BuildGraphWithStatusEnum();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var mapper = File.ReadAllText(Path.Combine(output, "src", "Enum6", "Mappings", "OrderMappings.cs"));

            // Entity → Dto: string → enum via Enum.Parse
            Assert.Contains("System.Enum.Parse<Status>", mapper);
            // Request → Entity: enum → string via .ToString() (V2 write path now comes from Request, not Dto)
            Assert.Contains("request.Status.ToString()", mapper);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void V1_dto_and_mapper_unaffected_by_enum_logic()
    {
        var (config, output) = Setup("Enum7");
        config.ApiVersion = ApiVersion.V1;
        var graph = BuildGraphWithStatusEnum();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            // V1 DTOs live at src/Enum7/Dtos/ (not Shared).
            var dtos = File.ReadAllText(Path.Combine(output, "src", "Enum7", "Dtos", "OrderDtos.cs"));
            Assert.Contains("public string Status { get; set; }", dtos);
            Assert.DoesNotContain(".Enums;", dtos);

            var mapper = File.ReadAllText(Path.Combine(output, "src", "Enum7", "Mappings", "OrderMappings.cs"));
            Assert.DoesNotContain("Enum.Parse", mapper);
        }
        finally { CleanupBestEffort(output); }
    }

    private static SchemaGraph BuildGraphWithStatusEnum()
    {
        var table = Table.Create(
            schema: "dbo",
            name: "orders",
            columns: new[]
            {
                new Column("id",     1, "int",       IsNullable: false, IsIdentity: true,  IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("status", 2, "nvarchar",  IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: 20,   Precision: null, Scale: null, DefaultValue: null),
            },
            primaryKey: PrimaryKey.Create("PK_orders", new[] { "id" }),
            checkConstraints: new[] { new CheckConstraint("CK_orders_status", "([Status] IN ('draft','published'))") });

        return SchemaGraph.Create(new[] { DbSchema.Create("dbo", new[] { table }) });
    }

    private static SchemaGraph BuildGraphWithCheckOnIdentity()
    {
        // Contrived: CHECK IN on the identity column. The column is excluded from
        // write DTOs (identity), so no enum should be emitted.
        var table = Table.Create(
            schema: "dbo",
            name: "events",
            columns: new[]
            {
                new Column("id",    1, "int",      IsNullable: false, IsIdentity: true,  IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("title", 2, "nvarchar", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: 50,   Precision: null, Scale: null, DefaultValue: null),
            },
            primaryKey: PrimaryKey.Create("PK_events", new[] { "id" }),
            checkConstraints: new[] { new CheckConstraint("CK_events_id_bucket", "([id] IN ('1','2','3'))") });

        return SchemaGraph.Create(new[] { DbSchema.Create("dbo", new[] { table }) });
    }

    private static SchemaGraph BuildGraphWithCaseCollidingValues()
    {
        // 'draft' and 'DRAFT' would pascalize to the same enum member.
        // EnumCandidates.TryParseInList rejects this; no enum emitted.
        var table = Table.Create(
            schema: "dbo",
            name: "orders",
            columns: new[]
            {
                new Column("id",     1, "int",      IsNullable: false, IsIdentity: true,  IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("status", 2, "nvarchar", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: 20,   Precision: null, Scale: null, DefaultValue: null),
            },
            primaryKey: PrimaryKey.Create("PK_orders", new[] { "id" }),
            checkConstraints: new[] { new CheckConstraint("CK_orders_status", "([status] IN ('draft','DRAFT'))") });

        return SchemaGraph.Create(new[] { DbSchema.Create("dbo", new[] { table }) });
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
