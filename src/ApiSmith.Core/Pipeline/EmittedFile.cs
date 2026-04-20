namespace ApiSmith.Core.Pipeline;

/// <summary>In-memory file awaiting flush; <see cref="RelativePath"/> always uses forward slashes.</summary>
public sealed record EmittedFile(string RelativePath, string Content);
