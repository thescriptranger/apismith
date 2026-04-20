using System.Collections.Immutable;

namespace ApiSmith.Core.Model;

public sealed record TableIndex(string Name, bool IsUnique, ImmutableArray<string> Columns)
{
    public static TableIndex Create(string name, bool isUnique, IEnumerable<string> columns) =>
        new(name, isUnique, columns.ToImmutableArray());
}
