using System.Collections.Immutable;

namespace ApiSmith.Core.Model;

public sealed record View(string Schema, string Name, ImmutableArray<Column> Columns)
{
    public static View Create(string schema, string name, IEnumerable<Column> columns) =>
        new(schema, name, columns.OrderBy(c => c.OrdinalPosition).ToImmutableArray());
}
