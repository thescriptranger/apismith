using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;

namespace ApiSmith.Generation.Emitters;

/// <summary>Pipes each layout <see cref="ProjectDefinition"/>'s csproj content out as an <see cref="EmittedFile"/>.</summary>
public static class CsProjEmitter
{
    public static IEnumerable<EmittedFile> Emit(ApiSmithConfig config, IArchitectureLayout layout)
    {
        foreach (var project in layout.Projects(config))
        {
            yield return new EmittedFile(project.RelativeCsprojPath, project.CsprojContent);
        }
    }
}
