using System.Collections.Immutable;

namespace ApiSmith.Core.Model;

public enum ReferentialAction { NoAction, Cascade, SetNull, SetDefault }

public sealed record ForeignKey(
    string Name,
    string FromSchema,
    string FromTable,
    ImmutableArray<string> FromColumns,
    string ToSchema,
    string ToTable,
    ImmutableArray<string> ToColumns,
    ReferentialAction OnDelete,
    ReferentialAction OnUpdate)
{
    public static ForeignKey Create(
        string name,
        string fromSchema,
        string fromTable,
        IEnumerable<string> fromColumns,
        string toSchema,
        string toTable,
        IEnumerable<string> toColumns,
        ReferentialAction onDelete = ReferentialAction.NoAction,
        ReferentialAction onUpdate = ReferentialAction.NoAction) =>
        new(name, fromSchema, fromTable, fromColumns.ToImmutableArray(),
            toSchema, toTable, toColumns.ToImmutableArray(), onDelete, onUpdate);
}
