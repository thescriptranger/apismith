namespace ApiSmith.Core.Model;

public sealed record Sequence(
    string Schema,
    string Name,
    string TypeName,
    long StartValue,
    long Increment,
    long? MinValue,
    long? MaxValue,
    bool Cycle);
