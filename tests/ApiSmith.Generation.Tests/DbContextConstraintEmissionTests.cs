using ApiSmith.Config;
using ApiSmith.Core.Model;
using ApiSmith.Core.Pipeline;
using ApiSmith.Introspection;

namespace ApiSmith.Generation.Tests;

public sealed class DbContextConstraintEmissionTests
{
    [Fact]
    public void Emits_unique_constraints_alphabetically_sorted()
    {
        // reverse order to prove the sort happens
        var uniques = new Dictionary<(string, string), IReadOnlyList<UniqueConstraint>>
        {
            [("dbo", "widgets")] = new[]
            {
                UniqueConstraint.Create("UX_B_widgets_code", new[] { "code" }),
                UniqueConstraint.Create("UX_A_widgets_slug", new[] { "slug" }),
            },
        };

        var dbCtx = EmitDbContext("UniqApi", BuildWidgetsGraph(uniques: uniques));

        var idxA = dbCtx.IndexOf("UX_A_widgets_slug", System.StringComparison.Ordinal);
        var idxB = dbCtx.IndexOf("UX_B_widgets_code", System.StringComparison.Ordinal);
        Assert.True(idxA > 0, "UX_A_widgets_slug not emitted");
        Assert.True(idxB > 0, "UX_B_widgets_code not emitted");
        Assert.True(idxA < idxB, "UX_A must appear before UX_B");

        Assert.Contains("b.HasAlternateKey(e => new { e.Slug }).HasName(\"UX_A_widgets_slug\");", dbCtx);
        Assert.Contains("b.HasAlternateKey(e => new { e.Code }).HasName(\"UX_B_widgets_code\");", dbCtx);
    }

    [Fact]
    public void Emits_indexes_with_unique_flag_when_applicable()
    {
        var indexes = new Dictionary<(string, string), IReadOnlyList<TableIndex>>
        {
            [("dbo", "widgets")] = new[]
            {
                TableIndex.Create("IX_widgets_code",  isUnique: true,  new[] { "code" }),
                TableIndex.Create("IX_widgets_name",  isUnique: false, new[] { "name" }),
            },
        };

        var dbCtx = EmitDbContext("IdxApi", BuildWidgetsGraph(indexes: indexes));

        Assert.Contains("b.HasIndex(e => new { e.Code }).HasDatabaseName(\"IX_widgets_code\").IsUnique();", dbCtx);
        Assert.Contains("b.HasIndex(e => new { e.Name }).HasDatabaseName(\"IX_widgets_name\");", dbCtx);
        Assert.DoesNotContain("HasDatabaseName(\"IX_widgets_name\").IsUnique()", dbCtx);
    }

    [Fact]
    public void Emits_check_constraints_with_escaped_expressions()
    {
        // embedded " must double in the verbatim literal
        var expr = "([Name] <> \"\")";
        var checks = new Dictionary<(string, string), IReadOnlyList<CheckConstraint>>
        {
            [("dbo", "widgets")] = new[]
            {
                new CheckConstraint("CK_widgets_name_nonempty", expr),
            },
        };

        var dbCtx = EmitDbContext("CkApi", BuildWidgetsGraph(checks: checks));

        Assert.Contains("b.ToTable(tb => tb.HasCheckConstraint(\"CK_widgets_name_nonempty\", @\"([Name] <> \"\"\"\")\"));", dbCtx);
    }

    [Fact]
    public void Emits_sequences_at_top_of_OnModelCreating_ordered_by_schema_and_name()
    {
        // reverse order to prove the sort happens
        var sequences = new[]
        {
            new Sequence("dbo",   "widget_seq", "bigint", StartValue: 1000, Increment: 1, MinValue: null, MaxValue: null, Cycle: false),
            new Sequence("audit", "event_seq",  "int",    StartValue: 1,    Increment: 1, MinValue: null, MaxValue: null, Cycle: false),
        };

        var graph = BuildWidgetsGraph(sequences: sequences);
        var dbCtx = EmitDbContext("SeqApi", graph);

        var auditIdx   = dbCtx.IndexOf("HasSequence<int>(\"event_seq\", \"audit\")", System.StringComparison.Ordinal);
        var dboIdx     = dbCtx.IndexOf("HasSequence<long>(\"widget_seq\", \"dbo\")", System.StringComparison.Ordinal);
        var entityIdx  = dbCtx.IndexOf("modelBuilder.Entity<", System.StringComparison.Ordinal);

        Assert.True(auditIdx > 0, "audit.event_seq not emitted");
        Assert.True(dboIdx   > 0, "dbo.widget_seq not emitted");
        Assert.True(entityIdx > 0, "no Entity<> block present");

        Assert.True(auditIdx < dboIdx, "sequences must sort by schema Ordinal");
        Assert.True(dboIdx < entityIdx, "sequences must precede modelBuilder.Entity<> blocks");

        Assert.Contains(".StartsAt(1000)", dbCtx);
        Assert.Contains(".IncrementsBy(1);", dbCtx);
    }

    [Fact]
    public void Views_do_not_receive_constraint_or_index_emission()
    {
        var widgets = Table.Create("dbo", "widgets",
            new[]
            {
                new Column("id",   1, "int",      IsNullable: false, IsIdentity: true,  IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("slug", 2, "nvarchar", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: 64,   Precision: null, Scale: null, DefaultValue: null),
            },
            PrimaryKey.Create("PK_widgets", new[] { "id" }));

        var vw = View.Create("dbo", "vw_widgets",
            new[]
            {
                new Column("id",   1, "int",      IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("slug", 2, "nvarchar", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: 64,   Precision: null, Scale: null, DefaultValue: null),
            });

        var graph = SqlServerSchemaReader.BuildGraph(
            new[] { widgets },
            System.Array.Empty<ForeignKey>(),
            new[] { vw },
            System.Array.Empty<StoredProcedure>(),
            System.Array.Empty<DbFunction>());

        var dbCtx = EmitDbContext("ViewNoConstraintApi", graph);

        Assert.Contains("b.ToView(\"vw_widgets\", \"dbo\");", dbCtx);

        Assert.DoesNotContain("HasAlternateKey", dbCtx);
        Assert.DoesNotContain("HasCheckConstraint", dbCtx);
        Assert.DoesNotContain("HasIndex", dbCtx);
    }

    private static SchemaGraph BuildWidgetsGraph(
        IReadOnlyDictionary<(string Schema, string Table), IReadOnlyList<UniqueConstraint>>? uniques = null,
        IReadOnlyDictionary<(string Schema, string Table), IReadOnlyList<CheckConstraint>>?   checks  = null,
        IReadOnlyDictionary<(string Schema, string Table), IReadOnlyList<TableIndex>>?        indexes = null,
        IReadOnlyList<Sequence>?                                                              sequences = null)
    {
        var widgets = Table.Create("dbo", "widgets",
            new[]
            {
                new Column("id",   1, "int",      IsNullable: false, IsIdentity: true,  IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("slug", 2, "nvarchar", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: 64,   Precision: null, Scale: null, DefaultValue: null),
                new Column("code", 3, "nvarchar", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: 32,   Precision: null, Scale: null, DefaultValue: null),
                new Column("name", 4, "nvarchar", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: 200,  Precision: null, Scale: null, DefaultValue: null),
            },
            PrimaryKey.Create("PK_widgets", new[] { "id" }));

        return SqlServerSchemaReader.BuildGraph(
            new[] { widgets },
            System.Array.Empty<ForeignKey>(),
            System.Array.Empty<View>(),
            System.Array.Empty<StoredProcedure>(),
            System.Array.Empty<DbFunction>(),
            uniques,
            checks,
            indexes,
            sequences);
    }

    private static string EmitDbContext(string projectName, SchemaGraph graph)
    {
        var output = Path.Combine(Path.GetTempPath(), "apismith-tests",
            projectName + "-" + System.Guid.NewGuid().ToString("N")[..8]);
        var config = new ApiSmithConfig
        {
            ProjectName = projectName,
            OutputDirectory = output,
            ConnectionString = "Server=x;Database=x;",
        };

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var path = Path.Combine(output, "src", projectName, "Data", $"{projectName}DbContext.cs");
            Assert.True(File.Exists(path), $"Expected DbContext at {path}");
            return File.ReadAllText(path);
        }
        finally
        {
            try { if (Directory.Exists(output)) Directory.Delete(output, recursive: true); } catch { /* ignore */ }
        }
    }

    private sealed class NullLog : IScaffoldLog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
