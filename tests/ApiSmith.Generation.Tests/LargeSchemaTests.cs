using System.Diagnostics;
using ApiSmith.Config;
using ApiSmith.Core.Model;
using ApiSmith.Core.Pipeline;
using ApiSmith.Introspection;

namespace ApiSmith.Generation.Tests;

public sealed class LargeSchemaTests
{
    [Fact]
    public void Scaffolds_50_table_schema_in_under_60_seconds()
    {
        var output = Path.Combine(Path.GetTempPath(), "apismith-tests",
            "Large-" + System.Guid.NewGuid().ToString("N")[..8]);
        var config = new ApiSmithConfig
        {
            ProjectName = "BigApi",
            OutputDirectory = output,
            ConnectionString = "Server=x;Database=x;",
        };

        try
        {
            var graph = Build50TableGraph();

            var sw = Stopwatch.StartNew();
            var report = new Generator(new NullLog()).Generate(config, graph, output);
            sw.Stop();

            // introspection excluded; in-memory graph
            Assert.True(sw.Elapsed < System.TimeSpan.FromSeconds(60),
                $"M1 missed: scaffold took {sw.ElapsedMilliseconds} ms, budget is 60000 ms.");

            Assert.True(report.FileCount >= 50, $"Expected at least 50 files, got {report.FileCount}.");
            Assert.True(report.TableCount >= 50, $"Expected 50+ tables, got {report.TableCount}.");
        }
        finally
        {
            try { if (Directory.Exists(output)) Directory.Delete(output, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task Full_pipeline_with_simulated_introspection_stays_under_60_seconds()
    {
        var output = Path.Combine(Path.GetTempPath(), "apismith-tests",
            "Pipeline50-" + System.Guid.NewGuid().ToString("N")[..8]);
        var config = new ApiSmithConfig
        {
            ProjectName = "PipelineApi",
            OutputDirectory = output,
            ConnectionString = "Server=x;Database=x;",
        };

        try
        {
            var graph = Build50TableGraph();

            var sw = Stopwatch.StartNew();

            // 2s stand-in for the real introspection phase
            await Task.Delay(System.TimeSpan.FromSeconds(2));

            new Generator(new NullLog()).Generate(config, graph, output);

            sw.Stop();
            Assert.True(sw.Elapsed < System.TimeSpan.FromSeconds(60),
                $"Full pipeline took {sw.Elapsed.TotalSeconds:F1}s, budget is 60s.");
        }
        finally
        {
            try { if (Directory.Exists(output)) Directory.Delete(output, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Views_emit_read_only_entities_and_endpoints()
    {
        var graph = BuildGraphWithViews();
        var named = NamedSchemaModel.Build(graph);

        var vwUser = named.Tables.Single(t => t.EntityName == "VwActiveUser");
        Assert.True(vwUser.IsView);
        Assert.Null(vwUser.PrimaryKey);

        var output = Path.Combine(Path.GetTempPath(), "apismith-tests",
            "Views-" + System.Guid.NewGuid().ToString("N")[..8]);
        var config = new ApiSmithConfig
        {
            ProjectName = "ViewApi",
            OutputDirectory = output,
            ConnectionString = "Server=x;Database=x;",
        };

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);

            var psi = new ProcessStartInfo("dotnet", $"build \"{output}\" --nologo -clp:NoSummary")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            using var proc = Process.Start(psi)!;
            var stdout = proc.StandardOutput.ReadToEnd();
            var stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            Assert.True(proc.ExitCode == 0,
                $"Views solution failed to build. exit={proc.ExitCode}\n{stdout}\n{stderr}");

            var dtoPath = Path.Combine(output, "src", "ViewApi", "Dtos", "VwActiveUserDtos.cs");
            Assert.True(File.Exists(dtoPath));
            var dto = File.ReadAllText(dtoPath);
            Assert.DoesNotContain("CreateVwActiveUserDto", dto);
            Assert.DoesNotContain("UpdateVwActiveUserDto", dto);

            var dbCtx = File.ReadAllText(Path.Combine(output, "src", "ViewApi", "Data", "ViewApiDbContext.cs"));
            Assert.Contains("ToView(\"vw_active_users\"", dbCtx);
            Assert.Contains("HasNoKey()", dbCtx);
        }
        finally
        {
            try { if (Directory.Exists(output)) Directory.Delete(output, recursive: true); } catch { /* ignore */ }
        }
    }

    private static SchemaGraph Build50TableGraph()
    {
        const int tableCount = 50;
        var tables = new List<Table>(tableCount);
        var fks = new List<ForeignKey>();

        for (var i = 0; i < tableCount; i++)
        {
            var name = $"table_{i:D2}";
            var cols = new List<Column>
            {
                new("id", 1, "int", IsNullable: false, IsIdentity: true, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new("name", 2, "nvarchar", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: 100, Precision: null, Scale: null, DefaultValue: null),
                new("description", 3, "nvarchar", IsNullable: true, IsIdentity: false, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new("amount", 4, "decimal", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: null, Precision: 18, Scale: 2, DefaultValue: null),
                new("is_active", 5, "bit", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: "((1))"),
                new("created_at", 6, "datetime2", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
            };

            // first 25 have no FK; next 25 each FK back to a lower table
            if (i >= 25)
            {
                cols.Add(new Column($"parent_id", 7, "int", IsNullable: true, IsIdentity: false, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null));
                fks.Add(ForeignKey.Create(
                    $"FK_{name}_parent",
                    "dbo", name, new[] { "parent_id" },
                    "dbo", $"table_{i - 25:D2}", new[] { "id" }));
            }

            tables.Add(Table.Create("dbo", name, cols, PrimaryKey.Create($"PK_{name}", new[] { "id" })));
        }

        return SqlServerSchemaReader.BuildGraph(
            tables, fks,
            System.Array.Empty<View>(),
            System.Array.Empty<StoredProcedure>(),
            System.Array.Empty<DbFunction>());
    }

    private static SchemaGraph BuildGraphWithViews()
    {
        var users = Table.Create("dbo", "users",
            new[]
            {
                new Column("id", 1, "int", IsNullable: false, IsIdentity: true, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("email", 2, "nvarchar", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: 256, Precision: null, Scale: null, DefaultValue: null),
                new Column("is_active", 3, "bit", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
            },
            PrimaryKey.Create("PK_users", new[] { "id" }));

        var view = View.Create("dbo", "vw_active_users",
            new[]
            {
                new Column("id", 1, "int", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("email", 2, "nvarchar", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: 256, Precision: null, Scale: null, DefaultValue: null),
            });

        return SqlServerSchemaReader.BuildGraph(
            new[] { users },
            System.Array.Empty<ForeignKey>(),
            new[] { view },
            System.Array.Empty<StoredProcedure>(),
            System.Array.Empty<DbFunction>());
    }

    private sealed class NullLog : IScaffoldLog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
