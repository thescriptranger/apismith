using System.Collections.Immutable;

namespace ApiSmith.Core.Model;

public sealed record UniqueConstraint(string Name, ImmutableArray<string> Columns)
{
    public static UniqueConstraint Create(string name, IEnumerable<string> columns) =>
        new(name, columns.ToImmutableArray());
}
