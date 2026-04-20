using System.Collections.Immutable;

namespace ApiSmith.Core.Model;

public sealed record PrimaryKey(string Name, ImmutableArray<string> Columns)
{
    public static PrimaryKey Create(string name, IEnumerable<string> columns) =>
        new(name, columns.ToImmutableArray());
}
