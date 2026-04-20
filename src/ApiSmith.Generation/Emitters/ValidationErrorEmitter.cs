using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;

namespace ApiSmith.Generation.Emitters;

public static class ValidationErrorEmitter
{
    public static EmittedFile? Emit(ApiSmithConfig config, IArchitectureLayout layout)
    {
        if (config.ApiVersion != ApiVersion.V2) return null;

        var content = $$"""
            namespace {{layout.SharedErrorsNamespace(config)}};

            public sealed record ValidationError(string PropertyName, string Message);
            """;

        var path = $"{layout.SharedProjectFolder(config)}/Errors/ValidationError.cs";
        return new EmittedFile(path, content);
    }
}
