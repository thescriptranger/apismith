using System.Collections.Immutable;

namespace ApiSmith.Core.Model;

public enum FunctionKind { Scalar, InlineTableValued, MultiStatementTableValued }

public sealed record DbFunction(
    string Schema,
    string Name,
    FunctionKind Kind,
    ImmutableArray<SprocParameter> Parameters,
    string? ReturnSqlType,
    ImmutableArray<ResultColumn> ResultColumns)
{
    public static DbFunction Create(
        string schema,
        string name,
        FunctionKind kind,
        IEnumerable<SprocParameter> parameters,
        string? returnSqlType = null,
        IEnumerable<ResultColumn>? resultColumns = null) =>
        new(schema, name, kind,
            parameters.OrderBy(p => p.OrdinalPosition).ToImmutableArray(),
            returnSqlType,
            (resultColumns ?? System.Array.Empty<ResultColumn>())
                .OrderBy(c => c.OrdinalPosition).ToImmutableArray());
}
