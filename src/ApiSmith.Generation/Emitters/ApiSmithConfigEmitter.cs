using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;

namespace ApiSmith.Generation.Emitters;

/// <summary>Writes <c>apismith.yaml</c> via <see cref="YamlWriter"/> so identical inputs replay byte-identical (idempotent replay NFR).</summary>
public static class ApiSmithConfigEmitter
{
    public static EmittedFile Emit(ApiSmithConfig config, IArchitectureLayout layout) =>
        new(layout.ApismithConfigPath(), YamlWriter.Write(config));
}
