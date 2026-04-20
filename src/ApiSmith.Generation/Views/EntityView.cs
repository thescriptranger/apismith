using System.Collections.Immutable;
using ApiSmith.Config;
using ApiSmith.Generation.Architectures;

namespace ApiSmith.Generation.Views;

/// <summary>Precomputed strings for the entity template; template stays logic-free.</summary>
public sealed record EntityView(
    string Namespace,
    string EntityName,
    ImmutableArray<ColumnView> Columns,
    ImmutableArray<ReferenceNavView> ReferenceNavigations,
    ImmutableArray<ListNavView> CollectionNavigations,
    ImmutableArray<ListNavView> SkipNavigations,
    ImmutableArray<string> UsingNamespaces)
{
    public bool HasReferenceNavigations => ReferenceNavigations.Length > 0;

    public bool HasCollectionOrSkipNavs => CollectionNavigations.Length > 0 || SkipNavigations.Length > 0;

    public bool HasUsingNamespaces => UsingNamespaces.Length > 0;

    public static EntityView Build(
        ApiSmithConfig config,
        IArchitectureLayout layout,
        NamedSchemaModel named,
        NamedTable table)
    {
        var entityNamespace = layout.EntityNamespace(config, table.Schema);

        var columns = table.Columns
            .Select(c => new ColumnView(
                PropertyName: c.PropertyName,
                ClrTypeWithNullability: c.ClrTypeWithNullability,
                Initializer: !c.IsNullable && c.ClrTypeName == "string" ? " = string.Empty;" : string.Empty))
            .ToImmutableArray();

        var refNavs = table.ReferenceNavigations
            .Select(n => new ReferenceNavView(
                Name: n.Name,
                TypeName: n.IsOptional ? n.TargetEntityName + "?" : n.TargetEntityName,
                Initializer: n.IsOptional ? string.Empty : " = null!;"))
            .ToImmutableArray();

        var collectionNavs = table.CollectionNavigations
            .Select(n => new ListNavView(n.Name, n.SourceEntityName))
            .ToImmutableArray();

        var skipNavs = table.SkipNavigations
            .Select(n => new ListNavView(n.Name, n.OtherEntityName))
            .ToImmutableArray();

        // Nav records carry the counterpart's schema directly — name-indexing mis-resolves on same-named entities across schemas.
        var usings = new SortedSet<string>(System.StringComparer.Ordinal);

        foreach (var n in table.ReferenceNavigations)
        {
            AddIfCrossSchema(usings, layout.EntityNamespace(config, n.TargetSchema), entityNamespace);
        }

        foreach (var n in table.CollectionNavigations)
        {
            AddIfCrossSchema(usings, layout.EntityNamespace(config, n.SourceSchema), entityNamespace);
        }

        foreach (var n in table.SkipNavigations)
        {
            AddIfCrossSchema(usings, layout.EntityNamespace(config, n.TargetSchema), entityNamespace);
        }

        return new EntityView(
            Namespace: entityNamespace,
            EntityName: table.EntityName,
            Columns: columns,
            ReferenceNavigations: refNavs,
            CollectionNavigations: collectionNavs,
            SkipNavigations: skipNavs,
            UsingNamespaces: usings.ToImmutableArray());
    }

    private static void AddIfCrossSchema(SortedSet<string> set, string candidate, string currentNamespace)
    {
        if (!string.Equals(candidate, currentNamespace, System.StringComparison.Ordinal))
        {
            set.Add(candidate);
        }
    }
}

public sealed record ColumnView(string PropertyName, string ClrTypeWithNullability, string Initializer);

public sealed record ReferenceNavView(string Name, string TypeName, string Initializer);

public sealed record ListNavView(string Name, string ElementType);
