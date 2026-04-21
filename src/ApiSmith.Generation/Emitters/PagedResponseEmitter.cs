using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;

namespace ApiSmith.Generation.Emitters;

public static class PagedResponseEmitter
{
    public static EmittedFile? Emit(ApiSmithConfig config, IArchitectureLayout layout)
    {
        if (config.ApiVersion != ApiVersion.V2) return null;

        var ns = $"{layout.SharedNamespace(config)}.Responses";
        var path = $"{layout.SharedProjectFolder(config)}/Responses/PagedResponse.cs";

        var content = $$"""
            using System.Collections.Generic;

            namespace {{ns}};

            public sealed class PagedResponse<T>
            {
                public IReadOnlyList<T> Items { get; init; } = System.Array.Empty<T>();
                public int Page { get; init; }
                public int PageSize { get; init; }
                public int TotalCount { get; init; }
                public int TotalPages => PageSize == 0 ? 0 : (int)System.Math.Ceiling((double)TotalCount / PageSize);
                public bool HasPreviousPage => Page > 1;
                public bool HasNextPage => Page < TotalPages;
            }
            """;

        return new EmittedFile(path, content);
    }
}
