using ApiSmith.Config;
using ApiSmith.Generation.Architectures;
using ApiSmith.Naming;

namespace ApiSmith.Generation;

/// <summary>
/// DbSet property/type naming: schema-prefixed (<c>DboUsers</c>/<c>AuditUsers</c>) on
/// cross-schema entity-name collisions, bare otherwise. Any emitter touching
/// <c>_db.{Property}</c> must go through <see cref="PropertyName"/> with the set from
/// <see cref="CollidedEntityNames"/> or the scaffold won't compile.
/// </summary>
public static class DbSetNaming
{
    public static HashSet<string> CollidedEntityNames(NamedSchemaModel named) =>
        named.Tables.Concat(named.JoinTables)
            .GroupBy(t => t.EntityName, System.StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(System.StringComparer.Ordinal);

    public static string PropertyName(NamedTable t, IReadOnlySet<string> collided) =>
        collided.Contains(t.EntityName)
            ? SchemaSegment.ToPascal(t.Schema) + t.CollectionName
            : t.CollectionName;

    /// <summary>Fully-qualified entity type ref on cross-schema collision, bare otherwise.</summary>
    public static string EntityTypeRef(
        ApiSmithConfig config,
        IArchitectureLayout layout,
        NamedTable t,
        IReadOnlySet<string> collided) =>
        collided.Contains(t.EntityName)
            ? $"{layout.EntityNamespace(config, t.Schema)}.{t.EntityName}"
            : t.EntityName;
}
