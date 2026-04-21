using System.Collections.Immutable;
using ApiSmith.Core.Model;
using ApiSmith.Introspection.TypeMapping;
using ApiSmith.Naming;

namespace ApiSmith.Generation;

/// <summary>Names and navigations resolved per-table once, so emitters never touch <see cref="Casing"/>.</summary>
public sealed record NamedSchemaModel(
    ImmutableArray<NamedTable> Tables,
    ImmutableArray<NamedTable> JoinTables,
    ImmutableArray<Sequence> Sequences)
{
    public static NamedSchemaModel Build(SchemaGraph graph)
    {
        var allTables = graph.AllTables.ToList();

        var fksBySource = allTables
            .SelectMany(t => t.ForeignKeys.Select(fk => (t, fk)))
            .GroupBy(x => (x.t.Schema, x.t.Name))
            .ToDictionary(g => g.Key, g => g.Select(x => x.fk).ToList());

        // Pass 1: shells with no navigations.
        var byKey = allTables
            .ToDictionary(
                t => (t.Schema, t.Name),
                t => NamedTable.ShellFrom(t));

        // Pass 2: reference navigations.
        foreach (var table in allTables)
        {
            var source = byKey[(table.Schema, table.Name)];

            var referenceNavs = new List<NamedReferenceNavigation>();
            if (fksBySource.TryGetValue((table.Schema, table.Name), out var fks))
            {
                // Disambiguate when the same target is referenced multiple times.
                var targetCounts = fks
                    .GroupBy(f => (f.ToSchema, f.ToTable))
                    .ToDictionary(g => g.Key, g => g.Count());

                foreach (var fk in fks)
                {
                    if (!byKey.TryGetValue((fk.ToSchema, fk.ToTable), out var target))
                    {
                        continue; // target outside included schemas
                    }

                    var sourceColumnName = fk.FromColumns[0];
                    var sourceColumn = source.Columns.FirstOrDefault(c => string.Equals(c.DbName, sourceColumnName, System.StringComparison.Ordinal));
                    if (sourceColumn is null)
                    {
                        continue;
                    }

                    var multipleToSameTarget = targetCounts[(fk.ToSchema, fk.ToTable)] > 1;
                    var navName = multipleToSameTarget
                        ? NavigationNamer.ReferenceName(sourceColumnName, target.EntityName)
                        : target.EntityName;

                    // Avoid collision with a column property of the same name.
                    if (source.Columns.Any(c => string.Equals(c.PropertyName, navName, System.StringComparison.Ordinal)))
                    {
                        navName = navName + "Navigation";
                    }

                    referenceNavs.Add(new NamedReferenceNavigation(
                        Name: navName,
                        TargetSchema: fk.ToSchema,
                        TargetTable: fk.ToTable,
                        TargetEntityName: target.EntityName,
                        FkColumnName: sourceColumnName,
                        FkPropertyName: sourceColumn.PropertyName,
                        PkColumnName: fk.ToColumns[0],
                        IsOptional: sourceColumn.IsNullable));
                }
            }

            byKey[(table.Schema, table.Name)] = source with { ReferenceNavigations = referenceNavs.ToImmutableArray() };
        }

        // Pass 3: inverse collection navigations.
        foreach (var table in allTables)
        {
            if (!fksBySource.TryGetValue((table.Schema, table.Name), out var fks))
            {
                continue;
            }

            var sourceNamed = byKey[(table.Schema, table.Name)];

            foreach (var fk in fks)
            {
                if (!byKey.TryGetValue((fk.ToSchema, fk.ToTable), out var target))
                {
                    continue;
                }

                // Join tables surface as skip navs in pass 4 instead.
                if (table.IsJoinTable)
                {
                    continue;
                }

                var collectionName = NavigationNamer.CollectionName(sourceNamed.EntityName);

                // Suffix with an index on collision (Orders, Orders2, ...).
                var existingNames = target.CollectionNavigations.Select(n => n.Name).ToHashSet(System.StringComparer.Ordinal);
                var candidate = collectionName;
                var i = 2;
                while (existingNames.Contains(candidate))
                {
                    candidate = collectionName + i++;
                }

                var updated = target with
                {
                    CollectionNavigations = target.CollectionNavigations.Add(new NamedCollectionNavigation(
                        Name: candidate,
                        SourceSchema: table.Schema,
                        SourceTable: table.Name,
                        SourceEntityName: sourceNamed.EntityName,
                        FkColumnName: fk.FromColumns[0])),
                };

                byKey[(fk.ToSchema, fk.ToTable)] = updated;
            }
        }

        // Pass 4: skip navs for join tables (m:n).
        foreach (var table in allTables.Where(t => t.IsJoinTable))
        {
            if (!fksBySource.TryGetValue((table.Schema, table.Name), out var fks) || fks.Count != 2)
            {
                continue;
            }

            var left  = fks[0];
            var right = fks[1];
            if (!byKey.TryGetValue((left.ToSchema, left.ToTable), out var leftTarget) ||
                !byKey.TryGetValue((right.ToSchema, right.ToTable), out var rightTarget))
            {
                continue;
            }

            var leftSkipName  = NavigationNamer.CollectionName(rightTarget.EntityName);
            var rightSkipName = NavigationNamer.CollectionName(leftTarget.EntityName);

            byKey[(left.ToSchema, left.ToTable)] = leftTarget with
            {
                SkipNavigations = leftTarget.SkipNavigations.Add(new NamedSkipNavigation(
                    Name: leftSkipName,
                    OtherEntityName: rightTarget.EntityName,
                    TargetSchema: rightTarget.Schema,
                    JoinSchema: table.Schema,
                    JoinTable: table.Name)),
            };

            byKey[(right.ToSchema, right.ToTable)] = rightTarget with
            {
                SkipNavigations = rightTarget.SkipNavigations.Add(new NamedSkipNavigation(
                    Name: rightSkipName,
                    OtherEntityName: leftTarget.EntityName,
                    TargetSchema: leftTarget.Schema,
                    JoinSchema: table.Schema,
                    JoinTable: table.Name)),
            };
        }

        var tables = byKey.Values
            .OrderBy(t => t.Schema, System.StringComparer.Ordinal)
            .ThenBy(t => t.EntityName, System.StringComparer.Ordinal)
            .ToImmutableArray();

        var views = graph.Schemas
            .SelectMany(s => s.Views)
            .Select(NamedTable.FromView)
            .OrderBy(t => t.Schema, System.StringComparer.Ordinal)
            .ThenBy(t => t.EntityName, System.StringComparer.Ordinal);

        var entities = tables
            .Where(t => !t.IsJoinTable)
            .Concat(views)
            .ToImmutableArray();

        var joinTables = tables.Where(t => t.IsJoinTable).ToImmutableArray();

        var sequences = graph.Schemas
            .SelectMany(s => s.Sequences)
            .OrderBy(s => s.Schema, System.StringComparer.Ordinal)
            .ThenBy(s => s.Name, System.StringComparer.Ordinal)
            .ToImmutableArray();

        return new NamedSchemaModel(entities, joinTables, sequences);
    }
}

public sealed record NamedTable(
    string Schema,
    string DbTableName,
    string EntityName,
    string CollectionName,
    string ParameterName,
    string RouteSegment,
    bool IsJoinTable,
    bool IsView,
    ImmutableArray<NamedColumn> Columns,
    NamedColumn? PrimaryKey,
    ImmutableArray<NamedReferenceNavigation> ReferenceNavigations,
    ImmutableArray<NamedCollectionNavigation> CollectionNavigations,
    ImmutableArray<NamedSkipNavigation> SkipNavigations,
    Table? Source)
{
    public static NamedTable ShellFrom(Table table)
    {
        var entity = Pluralizer.Singularize(Casing.ToPascal(table.Name));
        var collection = Pluralizer.Pluralize(entity);
        var parameter = Casing.ToCamel(entity);
        var route = collection.ToLowerInvariant();

        // Build a DB-column-name -> enum type name map from CHECK IN constraints.
        // Identity/computed columns are excluded (they won't appear on write DTOs).
        var enumByDbName = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var ck in table.CheckConstraints)
        {
            var parsed = EnumCandidates.TryParseInList(ck.Expression);
            if (parsed is null) continue;

            var srcCol = table.Columns.FirstOrDefault(c =>
                string.Equals(c.Name, parsed.Column, System.StringComparison.OrdinalIgnoreCase));
            if (srcCol is null || srcCol.IsIdentity || srcCol.IsComputed) continue;

            if (!enumByDbName.ContainsKey(srcCol.Name))
            {
                enumByDbName[srcCol.Name] = Casing.ToPascal(parsed.Column);
            }
        }

        var columns = table.Columns
            .Select(c =>
            {
                var nc = NamedColumn.From(c);
                return enumByDbName.TryGetValue(c.Name, out var enumName)
                    ? nc with { EnumTypeName = enumName }
                    : nc;
            })
            .ToImmutableArray();

        NamedColumn? pk = null;
        if (table.PrimaryKey is { Columns.Length: 1 } singleKey)
        {
            var pkDbName = singleKey.Columns[0];
            pk = columns.FirstOrDefault(c => string.Equals(c.DbName, pkDbName, System.StringComparison.Ordinal));
        }

        // First pass: identity columns are always server-generated.
        columns = columns.Select(c => c.IsIdentity ? c with { IsServerGenerated = true } : c).ToImmutableArray();
        if (pk is not null && pk.IsIdentity)
        {
            pk = columns.FirstOrDefault(c => string.Equals(c.DbName, pk.DbName, System.StringComparison.Ordinal));
        }

        // Second pass: a PK with a DB default is server-generated (Guid NEWID, sequences, etc.).
        if (pk is not null && !pk.IsServerGenerated)
        {
            var pkSource = table.Columns.FirstOrDefault(c => string.Equals(c.Name, pk.DbName, System.StringComparison.Ordinal));
            if (pkSource?.DefaultValue is not null)
            {
                var updatedPk = pk with { IsServerGenerated = true };
                columns = columns.Replace(pk, updatedPk);
                pk = updatedPk;
            }
        }

        return new NamedTable(
            Schema: table.Schema,
            DbTableName: table.Name,
            EntityName: entity,
            CollectionName: collection,
            ParameterName: parameter,
            RouteSegment: route,
            IsJoinTable: table.IsJoinTable,
            IsView: false,
            Columns: columns,
            PrimaryKey: pk,
            ReferenceNavigations: ImmutableArray<NamedReferenceNavigation>.Empty,
            CollectionNavigations: ImmutableArray<NamedCollectionNavigation>.Empty,
            SkipNavigations: ImmutableArray<NamedSkipNavigation>.Empty,
            Source: table);
    }

    public static NamedTable FromView(View view)
    {
        var entity = Pluralizer.Singularize(Casing.ToPascal(view.Name));
        var collection = Pluralizer.Pluralize(entity);
        var columns = view.Columns.Select(NamedColumn.From).ToImmutableArray();

        return new NamedTable(
            Schema: view.Schema,
            DbTableName: view.Name,
            EntityName: entity,
            CollectionName: collection,
            ParameterName: Casing.ToCamel(entity),
            RouteSegment: collection.ToLowerInvariant(),
            IsJoinTable: false,
            IsView: true,
            Columns: columns,
            PrimaryKey: null,
            ReferenceNavigations: ImmutableArray<NamedReferenceNavigation>.Empty,
            CollectionNavigations: ImmutableArray<NamedCollectionNavigation>.Empty,
            SkipNavigations: ImmutableArray<NamedSkipNavigation>.Empty,
            Source: null);
    }
}

public sealed record NamedColumn(
    string DbName,
    string PropertyName,
    string ClrTypeName,
    string ClrTypeWithNullability,
    bool IsNullable,
    bool IsIdentity,
    int? MaxLength,
    string? EnumTypeName = null,
    bool IsServerGenerated = false)
{
    public static NamedColumn From(Column column)
    {
        var clr = SqlTypeMapper.ToClrTypeName(column.SqlType);
        var nullable = column.IsNullable ? clr + "?" : clr;

        return new NamedColumn(
            DbName: column.Name,
            PropertyName: Casing.ToPascal(column.Name),
            ClrTypeName: clr,
            ClrTypeWithNullability: nullable,
            IsNullable: column.IsNullable,
            IsIdentity: column.IsIdentity,
            MaxLength: column.MaxLength,
            EnumTypeName: null);
    }
}

public sealed record NamedReferenceNavigation(
    string Name,
    string TargetSchema,
    string TargetTable,
    string TargetEntityName,
    string FkColumnName,
    string FkPropertyName,
    string PkColumnName,
    bool IsOptional);

public sealed record NamedCollectionNavigation(
    string Name,
    string SourceSchema,
    string SourceTable,
    string SourceEntityName,
    string FkColumnName);

public sealed record NamedSkipNavigation(
    string Name,
    string OtherEntityName,
    string TargetSchema,
    string JoinSchema,
    string JoinTable);
