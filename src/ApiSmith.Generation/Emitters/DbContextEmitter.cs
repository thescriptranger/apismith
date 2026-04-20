using System.Text;
using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;

namespace ApiSmith.Generation.Emitters;

public static class DbContextEmitter
{
    public static EmittedFile Emit(ApiSmithConfig config, IArchitectureLayout layout, NamedSchemaModel named, IReadOnlySet<string> collidedEntityNames)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        var entitySchemas = named.Tables.Concat(named.JoinTables)
            .Select(t => t.Schema)
            .Distinct(System.StringComparer.Ordinal)
            .OrderBy(s => s, System.StringComparer.Ordinal);
        foreach (var s in entitySchemas)
        {
            sb.AppendLine($"using {layout.EntityNamespace(config, s)};");
        }
        sb.AppendLine();
        sb.AppendLine($"namespace {layout.DataNamespace(config)};");
        sb.AppendLine();
        sb.AppendLine($"public sealed partial class {config.ProjectName}DbContext : DbContext");
        sb.AppendLine("{");
        sb.AppendLine($"    public {config.ProjectName}DbContext(DbContextOptions<{config.ProjectName}DbContext> options) : base(options)");
        sb.AppendLine("    {");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Cross-schema EntityName collisions get FQ type refs + schema-prefixed property names via DbSetNaming —
        // bare forms would hit CS0104/CS0102. Non-collided names stay bare for byte-identical single-schema replay.
        foreach (var t in named.Tables)
        {
            EmitDbSet(sb, config, layout, t, collidedEntityNames);
        }

        foreach (var t in named.JoinTables)
        {
            EmitDbSet(sb, config, layout, t, collidedEntityNames);
        }

        sb.AppendLine();
        sb.AppendLine("    protected override void OnModelCreating(ModelBuilder modelBuilder)");
        sb.AppendLine("    {");

        // Sequences first so later value-generator config can reference them; resort here to make the replay invariant explicit.
        var orderedSequences = named.Sequences
            .OrderBy(s => s.Schema, System.StringComparer.Ordinal)
            .ThenBy(s => s.Name, System.StringComparer.Ordinal);
        foreach (var seq in orderedSequences)
        {
            var clr = SequenceClrType(seq.TypeName);
            sb.AppendLine($"        modelBuilder.HasSequence<{clr}>(\"{seq.Name}\", \"{seq.Schema}\")");
            sb.AppendLine($"            .StartsAt({seq.StartValue})");
            sb.AppendLine($"            .IncrementsBy({seq.Increment});");
        }

        foreach (var t in named.Tables.Concat(named.JoinTables))
        {
            EmitEntityConfig(sb, config, layout, t, collidedEntityNames);
        }

        sb.AppendLine("        base.OnModelCreating(modelBuilder);");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return new EmittedFile(layout.DbContextPath(config), sb.ToString());
    }

    private static void EmitDbSet(
        StringBuilder sb,
        ApiSmithConfig config,
        IArchitectureLayout layout,
        NamedTable t,
        IReadOnlySet<string> collidedEntityNames)
    {
        var propertyName = DbSetNaming.PropertyName(t, collidedEntityNames);
        var typeRef = DbSetNaming.EntityTypeRef(config, layout, t, collidedEntityNames);
        sb.AppendLine($"    public DbSet<{typeRef}> {propertyName} => Set<{typeRef}>();");
    }

    private static void EmitEntityConfig(
        StringBuilder sb,
        ApiSmithConfig config,
        IArchitectureLayout layout,
        NamedTable t,
        IReadOnlySet<string> collidedEntityNames)
    {
        var entityTypeRef = DbSetNaming.EntityTypeRef(config, layout, t, collidedEntityNames);

        sb.AppendLine($"        modelBuilder.Entity<{entityTypeRef}>(b =>");
        sb.AppendLine("        {");

        if (t.IsView)
        {
            sb.AppendLine($"            b.ToView(\"{t.DbTableName}\", \"{t.Schema}\");");
            sb.AppendLine("            b.HasNoKey();");
        }
        else
        {
            sb.AppendLine($"            b.ToTable(\"{t.DbTableName}\", \"{t.Schema}\");");

            if (t.PrimaryKey is { } pk)
            {
                sb.AppendLine($"            b.HasKey(e => e.{pk.PropertyName});");
                if (pk.IsIdentity)
                {
                    sb.AppendLine($"            b.Property(e => e.{pk.PropertyName}).ValueGeneratedOnAdd();");
                }
            }
            else if (t.IsJoinTable && t.ReferenceNavigations.Length > 0)
            {
                var keyMembers = string.Join(", ", t.ReferenceNavigations.Select(n => $"e.{n.FkPropertyName}"));
                sb.AppendLine($"            b.HasKey(e => new {{ {keyMembers} }});");
            }
            else
            {
                sb.AppendLine("            b.HasNoKey();");
            }
        }

        foreach (var c in t.Columns)
        {
            sb.AppendLine($"            b.Property(e => e.{c.PropertyName}).HasColumnName(\"{c.DbName}\");");
        }

        foreach (var nav in t.ReferenceNavigations)
        {
            var withMany = t.IsJoinTable
                ? ".WithMany()"
                : $".WithMany(x => x.{Naming.Pluralizer.Pluralize(t.EntityName)})";

            sb.AppendLine($"            b.HasOne(e => e.{nav.Name})");
            sb.AppendLine($"                {withMany}");
            sb.AppendLine($"                .HasForeignKey(e => e.{nav.FkPropertyName})");
            sb.AppendLine($"                .IsRequired({(!nav.IsOptional).ToString().ToLowerInvariant()});");
        }

        // Views carry no storage-level constraints — skip unique/index/check emission.
        if (!t.IsView && t.Source is { } source)
        {
            var propByColumn = new Dictionary<string, string>(System.StringComparer.Ordinal);
            foreach (var col in t.Columns)
            {
                propByColumn[col.DbName] = col.PropertyName;
            }

            foreach (var uq in source.UniqueConstraints)
            {
                var members = string.Join(", ", uq.Columns.Select(col => $"e.{PropertyNameFor(propByColumn, col)}"));
                sb.AppendLine($"            b.HasAlternateKey(e => new {{ {members} }}).HasName(\"{uq.Name}\");");
            }

            foreach (var ix in source.Indexes)
            {
                var members = string.Join(", ", ix.Columns.Select(col => $"e.{PropertyNameFor(propByColumn, col)}"));
                var isUnique = ix.IsUnique ? ".IsUnique()" : string.Empty;
                sb.AppendLine($"            b.HasIndex(e => new {{ {members} }}).HasDatabaseName(\"{ix.Name}\"){isUnique};");
            }

            foreach (var ck in source.CheckConstraints)
            {
                sb.AppendLine($"            b.ToTable(tb => tb.HasCheckConstraint(\"{ck.Name}\", {EscapeStringLiteral(ck.Expression)}));");
            }
        }

        sb.AppendLine("        });");
        sb.AppendLine();
    }

    // Falls back to PascalCase for unknown columns so the emitter never throws mid-emission.
    private static string PropertyNameFor(IReadOnlyDictionary<string, string> propByColumn, string column)
    {
        return propByColumn.TryGetValue(column, out var name)
            ? name
            : Naming.Casing.ToPascal(column);
    }

    // Emits a C# verbatim string literal; doubles embedded quotes so check-constraint expressions stay well-formed.
    private static string EscapeStringLiteral(string raw)
        => "@\"" + raw.Replace("\"", "\"\"") + "\"";

    // Maps SQL Server sequence types to CLR; unknown falls back to long to keep emission resilient.
    private static string SequenceClrType(string typeName) => typeName switch
    {
        "bigint"   => "long",
        "int"      => "int",
        "smallint" => "short",
        "tinyint"  => "byte",
        "decimal"  => "decimal",
        "numeric"  => "decimal",
        _          => "long",
    };
}
