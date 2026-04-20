using System.Collections.Immutable;

namespace ApiSmith.Core.Model;

public sealed record Table(
    string Schema,
    string Name,
    ImmutableArray<Column> Columns,
    PrimaryKey? PrimaryKey,
    ImmutableArray<ForeignKey> ForeignKeys,
    ImmutableArray<UniqueConstraint> UniqueConstraints,
    ImmutableArray<TableIndex> Indexes,
    ImmutableArray<CheckConstraint> CheckConstraints,
    bool IsJoinTable)
{
    public static Table Create(
        string schema,
        string name,
        IEnumerable<Column> columns,
        PrimaryKey? primaryKey,
        IEnumerable<ForeignKey>? foreignKeys = null,
        IEnumerable<UniqueConstraint>? uniqueConstraints = null,
        IEnumerable<TableIndex>? indexes = null,
        IEnumerable<CheckConstraint>? checkConstraints = null,
        bool isJoinTable = false) =>
        new(schema, name,
            columns.OrderBy(c => c.OrdinalPosition).ToImmutableArray(),
            primaryKey,
            (foreignKeys ?? System.Array.Empty<ForeignKey>())
                .OrderBy(f => f.Name, System.StringComparer.Ordinal).ToImmutableArray(),
            (uniqueConstraints ?? System.Array.Empty<UniqueConstraint>())
                .OrderBy(u => u.Name, System.StringComparer.Ordinal).ToImmutableArray(),
            (indexes ?? System.Array.Empty<TableIndex>())
                .OrderBy(i => i.Name, System.StringComparer.Ordinal).ToImmutableArray(),
            (checkConstraints ?? System.Array.Empty<CheckConstraint>())
                .OrderBy(c => c.Name, System.StringComparer.Ordinal).ToImmutableArray(),
            isJoinTable);

    public string FullName => $"[{Schema}].[{Name}]";

    public Table WithJoinTableFlag(bool isJoinTable) => this with { IsJoinTable = isJoinTable };
}
