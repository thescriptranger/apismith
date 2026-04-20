using System.Collections.Immutable;

namespace ApiSmith.Core.Model;

/// <summary>Full introspected DB; sorted at every boundary for deterministic emission.</summary>
public sealed record SchemaGraph(ImmutableArray<DbSchema> Schemas)
{
    public static SchemaGraph Create(IEnumerable<DbSchema> schemas) =>
        new(schemas.OrderBy(s => s.Name, System.StringComparer.Ordinal).ToImmutableArray());

    public IEnumerable<Table> AllTables => Schemas.SelectMany(s => s.Tables);
}
