using System.Globalization;
using System.Text;
using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;
using ApiSmith.Generation.Validation;

namespace ApiSmith.Generation.Emitters;

public static class TestsProjectEmitter
{
    public static IEnumerable<EmittedFile> Emit(ApiSmithConfig config, IArchitectureLayout layout, NamedSchemaModel named)
    {
        var testsNs = layout.TestsNamespace(config);
        var project = layout.TestsProject(config);
        yield return new EmittedFile(project.RelativeCsprojPath, project.CsprojContent);

        yield return new EmittedFile(
            $"{layout.TestsProjectFolder(config)}/TestWebApplicationFactory.cs",
            BuildFactory(config, layout));

        foreach (var table in named.Tables)
        {
            yield return new EmittedFile(
                layout.TestsValidatorPath(config, table.EntityName),
                BuildValidatorTest(config, layout, table));

            if (table.PrimaryKey is not null && config.DataAccess is DataAccessStyle.EfCore)
            {
                // EF Core InMemory only — Dapper needs a real DB.
                yield return new EmittedFile(
                    layout.TestsEndpointPath(config, table.CollectionName),
                    BuildEndpointTest(config, layout, table));
            }
        }
    }

    private static string BuildFactory(ApiSmithConfig config, IArchitectureLayout layout)
    {
        var testsNs = layout.TestsNamespace(config);
        var dataNs = layout.DataNamespace(config);
        var dbCtx = $"{config.ProjectName}DbContext";

        if (config.DataAccess is not DataAccessStyle.EfCore)
        {
            return $$"""
                using Microsoft.AspNetCore.Mvc.Testing;

                namespace {{testsNs}};

                /// <summary>
                /// Minimal factory — Dapper tests would need a real DB, so the generated
                /// tests project currently exercises validators only when Dapper is chosen.
                /// </summary>
                public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
                {
                }
                """;
        }

        return $$"""
            using Microsoft.AspNetCore.Hosting;
            using Microsoft.AspNetCore.Mvc.Testing;
            using Microsoft.EntityFrameworkCore;
            using Microsoft.Extensions.DependencyInjection;
            using {{dataNs}};

            namespace {{testsNs}};

            /// <summary>
            /// Replaces the real SQL Server DbContext with an in-memory one so the test
            /// server can be started without network access. Each test gets its own DB.
            /// </summary>
            public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
            {
                public string DatabaseName { get; } = "Tests-" + System.Guid.NewGuid().ToString("N")[..8];

                protected override void ConfigureWebHost(IWebHostBuilder builder)
                {
                    builder.ConfigureServices(services =>
                    {
                        var toRemove = services.Where(d => d.ServiceType == typeof(DbContextOptions<{{dbCtx}}>)).ToList();
                        foreach (var d in toRemove)
                        {
                            services.Remove(d);
                        }

                        services.AddDbContext<{{dbCtx}}>(opt => opt.UseInMemoryDatabase(DatabaseName));
                    });
                }
            }
            """;
    }

    private static string BuildValidatorTest(ApiSmithConfig config, IArchitectureLayout layout, NamedTable table)
    {
        var testsNs = layout.TestsNamespace(config);
        var dtoNs = layout.DtoNamespace(config, table.Schema);
        var validatorNs = layout.ValidatorNamespace(config, table.Schema);
        var entity = table.EntityName;

        var (overrides, checkOverrides) = BuildMinimallyValidOverrides(config, table);
        var minimallyValidInitializer = BuildInitializer(overrides, skipPropertyName: null);

        var sbClass = new StringBuilder();
        sbClass.Append("    [Fact]\n");
        sbClass.Append("    public void Rejects_null_dto()\n");
        sbClass.Append("    {\n");
        sbClass.Append($"        var result = new Create{entity}DtoValidator().Validate(null!);\n");
        sbClass.Append("        Assert.False(result.IsValid);\n");
        sbClass.Append("    }\n");
        sbClass.Append("\n");
        sbClass.Append("    [Fact]\n");
        sbClass.Append("    public void Accepts_minimally_valid_dto()\n");
        sbClass.Append("    {\n");
        sbClass.Append($"        var dto = new Create{entity}Dto\n");
        sbClass.Append("        {\n");
        // Blank line when empty matches the 3.2-era template — replay invariant.
        sbClass.Append(minimallyValidInitializer.Length == 0 ? "\n" : minimallyValidInitializer);
        sbClass.Append("        };\n");
        sbClass.Append("\n");
        sbClass.Append($"        var result = new Create{entity}DtoValidator().Validate(dto);\n");
        sbClass.Append("        Assert.True(result.IsValid, string.Join(\"; \", result.Errors.Select(e => $\"{e.PropertyName}: {e.Message}\")));\n");
        sbClass.Append("    }\n");

        // B.1 — exercise the first required FK nav (alphabetical for determinism).
        if (config.ValidateForeignKeyReferences)
        {
            var requiredFk = table.ReferenceNavigations
                .Where(n => !n.IsOptional)
                .OrderBy(n => n.FkPropertyName, StringComparer.Ordinal)
                .FirstOrDefault();

            if (requiredFk is not null)
            {
                var fkInitializer = BuildInitializer(overrides, skipPropertyName: requiredFk.FkPropertyName);
                sbClass.Append("\n");
                sbClass.Append("    [Fact]\n");
                sbClass.Append("    public void Rejects_default_foreign_key()\n");
                sbClass.Append("    {\n");
                sbClass.Append($"        var dto = new Create{entity}Dto\n");
                sbClass.Append("        {\n");
                sbClass.Append(fkInitializer);
                sbClass.Append("        };\n");
                sbClass.Append("\n");
                sbClass.Append($"        var result = new Create{entity}DtoValidator().Validate(dto);\n");
                sbClass.Append("        Assert.False(result.IsValid);\n");
                sbClass.Append($"        Assert.Contains(result.Errors, e => e.PropertyName == \"{requiredFk.FkPropertyName}\");\n");
                sbClass.Append("    }\n");
            }
        }

        // B.2 — one Rejects_value_outside_check_constraint_{Prop} per translated CK column, alphabetical.
        foreach (var co in checkOverrides.OrderBy(c => c.PropertyName, StringComparer.Ordinal))
        {
            // CK column is already in `overrides`; swap pass value for violate value.
            var violatingOverrides = overrides
                .Select(o => o.Property == co.PropertyName ? (o.Property, Value: co.ViolateLiteral) : o)
                .ToList();
            var violatingInitializer = BuildInitializer(violatingOverrides, skipPropertyName: null);

            sbClass.Append("\n");
            sbClass.Append("    [Fact]\n");
            sbClass.Append($"    public void Rejects_value_outside_check_constraint_{co.PropertyName}()\n");
            sbClass.Append("    {\n");
            sbClass.Append($"        var dto = new Create{entity}Dto\n");
            sbClass.Append("        {\n");
            sbClass.Append(violatingInitializer);
            sbClass.Append("        };\n");
            sbClass.Append("\n");
            sbClass.Append($"        var result = new Create{entity}DtoValidator().Validate(dto);\n");
            sbClass.Append("        Assert.False(result.IsValid);\n");
            sbClass.Append($"        Assert.Contains(result.Errors, e => e.PropertyName == \"{co.PropertyName}\");\n");
            sbClass.Append("    }\n");
        }

        var result = new StringBuilder();
        result.Append($"using {dtoNs};\n");
        result.Append($"using {validatorNs};\n");
        result.Append("\n");
        result.Append($"namespace {testsNs};\n");
        result.Append("\n");
        result.Append($"public sealed class {entity}ValidatorTests\n");
        result.Append("{\n");
        result.Append(sbClass.ToString());
        // No trailing newline after the closing brace — 3.2-era replay invariant.
        result.Append("}");
        return result.ToString();
    }

    private sealed record CheckOverride(string PropertyName, string PassLiteral, string ViolateLiteral);

    // Minimally-valid overrides for a table's Create/Update DTO, shared by validator and endpoint tests.
    private static (List<(string Property, string Value)> Overrides, List<CheckOverride> CheckOverrides)
        BuildMinimallyValidOverrides(ApiSmithConfig config, NamedTable table)
    {
        // Preserve first-insertion order (columns, then FK navs, then CKs) — replay invariant.
        var overrides = new List<(string Property, string Value)>();
        var overrideIndex = new Dictionary<string, int>(StringComparer.Ordinal);

        void SetOverride(string property, string value)
        {
            if (overrideIndex.TryGetValue(property, out var idx))
            {
                overrides[idx] = (property, value);
            }
            else
            {
                overrideIndex[property] = overrides.Count;
                overrides.Add((property, value));
            }
        }

        // Required strings get "x" for the NotNullOrWhiteSpace rule; column order preserves the 3.2-era output.
        foreach (var c in table.Columns)
        {
            if (c.IsIdentity) { continue; }

            if (!c.IsNullable && c.ClrTypeName == "string")
            {
                SetOverride(c.PropertyName, "\"x\"");
            }
        }

        // Required FK columns need non-default values for the FK required-check to pass.
        if (config.ValidateForeignKeyReferences)
        {
            var columnsByDbName = table.Columns.ToDictionary(c => c.DbName, StringComparer.Ordinal);
            foreach (var nav in table.ReferenceNavigations)
            {
                if (nav.IsOptional) { continue; }
                if (!columnsByDbName.TryGetValue(nav.FkColumnName, out var fkColumn)) { continue; }

                var literal = NonDefaultLiteralFor(fkColumn.ClrTypeName);
                if (literal is not null)
                {
                    SetOverride(nav.FkPropertyName, literal);
                }
            }
        }

        // CK overrides win over FK overrides; keep alphabetical for B.2 emission.
        var checkOverrides = new List<CheckOverride>();
        if (table.Source is { } source && source.CheckConstraints.Length > 0)
        {
            // CK-expression column names are verbatim from SQL; match case-insensitively or by PascalCase property.
            // Silently skip unknown columns — tests must not reference nonexistent properties (ValidatorEmitter is stricter).
            var columnsByDbNameCi = table.Columns.ToDictionary(
                c => c.DbName,
                c => c,
                StringComparer.OrdinalIgnoreCase);
            var columnsByPropertyName = table.Columns.ToDictionary(
                c => c.PropertyName,
                c => c,
                StringComparer.OrdinalIgnoreCase);
            var seenProps = new HashSet<string>(StringComparer.Ordinal);

            foreach (var cc in source.CheckConstraints)
            {
                var translated = CheckConstraintTranslator.TryTranslate(cc.Expression);
                if (translated is null) { continue; }

                string? dbColumn = translated switch
                {
                    ComparisonRule cmp => cmp.Column,
                    BetweenRule between => between.Column,
                    _ => null,
                };
                if (dbColumn is null) { continue; }

                if (!columnsByDbNameCi.TryGetValue(dbColumn, out var col) &&
                    !columnsByPropertyName.TryGetValue(dbColumn, out col))
                {
                    continue;
                }

                // Only integer columns get a literal; otherwise skip.
                if (!IsIntegerClrType(col.ClrTypeName)) { continue; }
                if (!seenProps.Add(col.PropertyName)) { continue; }

                var (pass, violate) = ValuesFor(translated);
                checkOverrides.Add(new CheckOverride(
                    PropertyName: col.PropertyName,
                    PassLiteral: FormatIntegerLiteral(pass, col.ClrTypeName),
                    ViolateLiteral: FormatIntegerLiteral(violate, col.ClrTypeName)));
            }

            // CK pass-values win over FK overrides for the same property.
            foreach (var co in checkOverrides)
            {
                SetOverride(co.PropertyName, co.PassLiteral);
            }
        }

        return (overrides, checkOverrides);
    }

    private static string BuildInitializer(
        IReadOnlyList<(string Property, string Value)> overrides,
        string? skipPropertyName)
    {
        var sb = new StringBuilder();
        foreach (var (property, value) in overrides)
        {
            if (skipPropertyName is not null && string.Equals(property, skipPropertyName, StringComparison.Ordinal))
            {
                continue;
            }

            sb.Append($"            {property} = {value},\n");
        }
        return sb.ToString();
    }

    private static string? NonDefaultLiteralFor(string clrTypeName) => clrTypeName switch
    {
        "int" or "long" or "short" or "byte" => "1",
        "Guid" => "new Guid(\"00000000-0000-0000-0000-000000000001\")",
        _ => null,
    };

    private static bool IsIntegerClrType(string clrTypeName) => clrTypeName switch
    {
        "int" or "long" or "short" or "byte" => true,
        _ => false,
    };

    private static string FormatIntegerLiteral(long value, string clrTypeName)
    {
        var s = value.ToString(CultureInfo.InvariantCulture);
        return clrTypeName switch
        {
            "long" => s + "L",
            _ => s,
        };
    }

    private static (long Pass, long Violate) ValuesFor(object translated) => translated switch
    {
        ComparisonRule cmp => cmp.Operator switch
        {
            ">=" => (cmp.LiteralValue, cmp.LiteralValue - 1),
            ">"  => (cmp.LiteralValue + 1, cmp.LiteralValue),
            "<=" => (cmp.LiteralValue, cmp.LiteralValue + 1),
            "<"  => (cmp.LiteralValue - 1, cmp.LiteralValue),
            _    => (0L, 0L),
        },
        BetweenRule between => (between.LowerInclusive, between.LowerInclusive - 1),
        _ => (0L, 0L),
    };

    private static string BuildEndpointTest(ApiSmithConfig config, IArchitectureLayout layout, NamedTable table)
    {
        var testsNs = layout.TestsNamespace(config);
        var dtoNs = layout.DtoNamespace(config, table.Schema);
        var route = table.RouteSegment;
        var entity = table.EntityName;
        var crud = config.Crud;

        // Byte-identity guard: GetList-only falls through to the pre-task template verbatim.
        if (crud == CrudOperations.GetList)
        {
            return $$"""
                using System.Net;

                namespace {{testsNs}};

                public sealed class {{table.CollectionName}}EndpointTests : IClassFixture<TestWebApplicationFactory>
                {
                    private readonly TestWebApplicationFactory _factory;

                    public {{table.CollectionName}}EndpointTests(TestWebApplicationFactory factory)
                    {
                        _factory = factory;
                    }

                    [Fact]
                    public async System.Threading.Tasks.Task List_returns_ok_on_empty_db()
                    {
                        using var client = _factory.CreateClient();
                        var response = await client.GetAsync("/api/{{route}}").ConfigureAwait(false);
                        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    }
                }
                """;
        }

        // Reuse Accepts_minimally_valid_dto overrides so Post/Put/Patch payloads stay consistent.
        var needsDto = (crud & (CrudOperations.Post | CrudOperations.Put | CrudOperations.Patch)) != 0;
        string initializerBody = string.Empty;
        if (needsDto)
        {
            var (overrides, _) = BuildMinimallyValidOverrides(config, table);
            initializerBody = BuildInitializer(overrides, skipPropertyName: null);
        }

        var usings = new StringBuilder();
        usings.Append("using System.Net;\n");
        if (needsDto)
        {
            usings.Append("using System.Net.Http.Json;\n");
            // Guard DTO using against CS0105/CS8019 under TreatWarningsAsErrors when namespaces collide.
            if (!string.Equals(dtoNs, testsNs, StringComparison.Ordinal))
            {
                usings.Append($"using {dtoNs};\n");
            }
        }

        var body = new StringBuilder();
        body.Append("    private readonly TestWebApplicationFactory _factory;\n");
        body.Append("\n");
        body.Append($"    public {table.CollectionName}EndpointTests(TestWebApplicationFactory factory)\n");
        body.Append("    {\n");
        body.Append("        _factory = factory;\n");
        body.Append("    }\n");

        if ((crud & CrudOperations.GetList) != 0)
        {
            body.Append("\n");
            body.Append("    [Fact]\n");
            body.Append("    public async System.Threading.Tasks.Task Get_list_returns_ok()\n");
            body.Append("    {\n");
            body.Append("        using var client = _factory.CreateClient();\n");
            body.Append($"        var response = await client.GetAsync(\"/api/{route}\").ConfigureAwait(false);\n");
            body.Append("        Assert.Equal(HttpStatusCode.OK, response.StatusCode);\n");
            body.Append("    }\n");
        }

        if ((crud & CrudOperations.GetById) != 0)
        {
            body.Append("\n");
            body.Append("    [Fact]\n");
            body.Append("    public async System.Threading.Tasks.Task Get_by_id_returns_404_when_missing()\n");
            body.Append("    {\n");
            body.Append("        using var client = _factory.CreateClient();\n");
            body.Append($"        var response = await client.GetAsync(\"/api/{route}/99999\").ConfigureAwait(false);\n");
            body.Append("        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);\n");
            body.Append("    }\n");
        }

        if ((crud & CrudOperations.Post) != 0)
        {
            body.Append("\n");
            body.Append("    [Fact]\n");
            body.Append("    public async System.Threading.Tasks.Task Post_returns_success_with_valid_payload()\n");
            body.Append("    {\n");
            body.Append("        using var client = _factory.CreateClient();\n");
            body.Append($"        var dto = new Create{entity}Dto\n");
            body.Append("        {\n");
            body.Append(initializerBody.Length == 0 ? "\n" : initializerBody);
            body.Append("        };\n");
            body.Append($"        var response = await client.PostAsJsonAsync(\"/api/{route}\", dto).ConfigureAwait(false);\n");
            body.Append("        Assert.True(response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.OK);\n");
            body.Append("    }\n");
        }

        if ((crud & CrudOperations.Put) != 0)
        {
            body.Append("\n");
            body.Append("    [Fact]\n");
            body.Append("    public async System.Threading.Tasks.Task Put_returns_404_when_id_missing()\n");
            body.Append("    {\n");
            body.Append("        using var client = _factory.CreateClient();\n");
            body.Append($"        var dto = new Update{entity}Dto\n");
            body.Append("        {\n");
            body.Append(initializerBody.Length == 0 ? "\n" : initializerBody);
            body.Append("        };\n");
            body.Append($"        var response = await client.PutAsJsonAsync(\"/api/{route}/99999\", dto).ConfigureAwait(false);\n");
            body.Append("        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);\n");
            body.Append("    }\n");
        }

        if ((crud & CrudOperations.Patch) != 0)
        {
            body.Append("\n");
            body.Append("    [Fact]\n");
            body.Append("    public async System.Threading.Tasks.Task Patch_returns_404_when_id_missing()\n");
            body.Append("    {\n");
            body.Append("        using var client = _factory.CreateClient();\n");
            body.Append($"        var dto = new Update{entity}Dto\n");
            body.Append("        {\n");
            body.Append(initializerBody.Length == 0 ? "\n" : initializerBody);
            body.Append("        };\n");
            body.Append($"        var response = await client.PatchAsJsonAsync(\"/api/{route}/99999\", dto).ConfigureAwait(false);\n");
            body.Append("        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);\n");
            body.Append("    }\n");
        }

        if ((crud & CrudOperations.Delete) != 0)
        {
            body.Append("\n");
            body.Append("    [Fact]\n");
            body.Append("    public async System.Threading.Tasks.Task Delete_returns_404_when_id_missing()\n");
            body.Append("    {\n");
            body.Append("        using var client = _factory.CreateClient();\n");
            body.Append($"        var response = await client.DeleteAsync(\"/api/{route}/99999\").ConfigureAwait(false);\n");
            body.Append("        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);\n");
            body.Append("    }\n");
        }

        var result = new StringBuilder();
        result.Append(usings);
        result.Append("\n");
        result.Append($"namespace {testsNs};\n");
        result.Append("\n");
        result.Append($"public sealed class {table.CollectionName}EndpointTests : IClassFixture<TestWebApplicationFactory>\n");
        result.Append("{\n");
        result.Append(body);
        result.Append("}\n");
        return result.ToString();
    }
}
