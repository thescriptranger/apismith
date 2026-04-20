namespace ApiSmith.Core.Model;

public sealed record Column(
    string Name,
    int OrdinalPosition,
    string SqlType,
    bool IsNullable,
    bool IsIdentity,
    bool IsComputed,
    int? MaxLength,
    int? Precision,
    int? Scale,
    string? DefaultValue);
