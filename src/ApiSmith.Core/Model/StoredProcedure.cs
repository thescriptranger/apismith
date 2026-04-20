using System.Collections.Immutable;

namespace ApiSmith.Core.Model;

public enum ParameterDirection { In, Out, InOut, ReturnValue }

public sealed record SprocParameter(
    string Name,
    int OrdinalPosition,
    string SqlType,
    bool IsNullable,
    ParameterDirection Direction,
    int? MaxLength,
    int? Precision,
    int? Scale);

public sealed record ResultColumn(
    string Name,
    int OrdinalPosition,
    string SqlType,
    bool IsNullable);

public sealed record StoredProcedure(
    string Schema,
    string Name,
    ImmutableArray<SprocParameter> Parameters,
    ImmutableArray<ResultColumn> ResultColumns,
    bool ResultIsIndeterminate,
    string? IndeterminateReason)
{
    public static StoredProcedure Create(
        string schema,
        string name,
        IEnumerable<SprocParameter> parameters,
        IEnumerable<ResultColumn>? resultColumns = null,
        bool resultIsIndeterminate = false,
        string? indeterminateReason = null) =>
        new(schema, name,
            parameters.OrderBy(p => p.OrdinalPosition).ToImmutableArray(),
            (resultColumns ?? System.Array.Empty<ResultColumn>())
                .OrderBy(c => c.OrdinalPosition).ToImmutableArray(),
            resultIsIndeterminate,
            indeterminateReason);
}
