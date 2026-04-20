using System.Collections.Immutable;

namespace ApiSmith.Generation.Architectures;

/// <summary>One generated .csproj; layouts yield one or more so the Sln emitter doesn't special-case.</summary>
public sealed record ProjectDefinition(
    string AssemblyName,
    string RelativeCsprojPath,
    string CsprojContent,
    bool IsWebProject,
    ImmutableArray<string> ReferencedAssemblies);
