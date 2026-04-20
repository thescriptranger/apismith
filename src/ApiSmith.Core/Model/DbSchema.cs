using System.Collections.Immutable;

namespace ApiSmith.Core.Model;

public sealed record DbSchema(
    string Name,
    ImmutableArray<Table> Tables,
    ImmutableArray<View> Views,
    ImmutableArray<StoredProcedure> Procedures,
    ImmutableArray<DbFunction> Functions,
    ImmutableArray<Sequence> Sequences)
{
    public static DbSchema Create(
        string name,
        IEnumerable<Table> tables,
        IEnumerable<View>? views = null,
        IEnumerable<StoredProcedure>? procedures = null,
        IEnumerable<DbFunction>? functions = null,
        IEnumerable<Sequence>? sequences = null) =>
        new(name,
            tables.OrderBy(t => t.Name, System.StringComparer.Ordinal).ToImmutableArray(),
            (views ?? System.Array.Empty<View>())
                .OrderBy(v => v.Name, System.StringComparer.Ordinal).ToImmutableArray(),
            (procedures ?? System.Array.Empty<StoredProcedure>())
                .OrderBy(p => p.Name, System.StringComparer.Ordinal).ToImmutableArray(),
            (functions ?? System.Array.Empty<DbFunction>())
                .OrderBy(f => f.Name, System.StringComparer.Ordinal).ToImmutableArray(),
            (sequences ?? System.Array.Empty<Sequence>())
                .OrderBy(s => s.Name, System.StringComparer.Ordinal).ToImmutableArray());

    public DbSchema WithTables(IEnumerable<Table> tables) =>
        this with
        {
            Tables = tables.OrderBy(t => t.Name, System.StringComparer.Ordinal).ToImmutableArray(),
        };
}
