using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;

namespace ApiSmith.Generation.Emitters;

public static class ApiProblemEmitter
{
    public static EmittedFile? Emit(ApiSmithConfig config, IArchitectureLayout layout)
    {
        if (config.ApiVersion != ApiVersion.V2) return null;

        var content = $$"""
            using System.Collections.Immutable;

            namespace {{layout.SharedErrorsNamespace(config)}};

            public sealed record ApiProblem(
                string Title,
                int Status,
                string Type,
                ImmutableArray<ValidationError> Errors);
            """;

        var path = $"{layout.SharedProjectFolder(config)}/Errors/ApiProblem.cs";
        return new EmittedFile(path, content);
    }
}
