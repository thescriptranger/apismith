using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;

namespace ApiSmith.Generation.Emitters;

/// <summary>FR-12: emits API-version middleware for Header/QueryString; UrlSegment goes through route prefixes; None is a no-op.</summary>
public static class VersioningEmitter
{
    public static IEnumerable<EmittedFile> Emit(ApiSmithConfig config, IArchitectureLayout layout)
    {
        if (config.Versioning is VersioningStyle.None or VersioningStyle.UrlSegment)
        {
            yield break;
        }

        var ns = layout.ApiNamespace(config);
        var folder = layout.ApiProjectFolder(config);

        var sourceField = config.Versioning switch
        {
            VersioningStyle.Header       => "X-Api-Version header",
            VersioningStyle.QueryString  => "api-version query string parameter",
            _ => "version",
        };

        var extractor = config.Versioning is VersioningStyle.Header
            ? """
                var value = context.Request.Headers["X-Api-Version"].ToString();
                """
            : """
                var value = context.Request.Query["api-version"].ToString();
                """;

        var middleware = $$"""
            using Microsoft.AspNetCore.Http;

            namespace {{ns}}.Versioning;

            /// <summary>
            /// Requires every request (outside the OpenAPI / Scalar endpoints) to declare
            /// an API version via the {{sourceField}}. Rejects missing or unsupported
            /// versions with HTTP 400.
            /// </summary>
            public sealed class ApiVersionMiddleware
            {
                private static readonly string[] SupportedVersions = { "1" };

                private readonly RequestDelegate _next;

                public ApiVersionMiddleware(RequestDelegate next)
                {
                    _next = next;
                }

                public async System.Threading.Tasks.Task InvokeAsync(HttpContext context)
                {
                    var path = context.Request.Path.Value ?? string.Empty;
                    if (path.StartsWith("/openapi", System.StringComparison.OrdinalIgnoreCase)
                        || path.StartsWith("/scalar", System.StringComparison.OrdinalIgnoreCase))
                    {
                        await _next(context).ConfigureAwait(false);
                        return;
                    }

            {{extractor}}

                    if (string.IsNullOrWhiteSpace(value))
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await context.Response.WriteAsync("Missing API version.").ConfigureAwait(false);
                        return;
                    }

                    if (System.Array.IndexOf(SupportedVersions, value) < 0)
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await context.Response.WriteAsync($"Unsupported API version '{value}'.").ConfigureAwait(false);
                        return;
                    }

                    context.Items["ApiVersion"] = value;
                    await _next(context).ConfigureAwait(false);
                }
            }
            """;

        yield return new EmittedFile($"{folder}/Versioning/ApiVersionMiddleware.cs", middleware);
    }

    public static string RoutePrefix(ApiSmithConfig config) => config.Versioning switch
    {
        VersioningStyle.UrlSegment => "api/v1",
        _                          => "api",
    };

    public static string MinimalApiGroupPrefix(ApiSmithConfig config) => config.Versioning switch
    {
        VersioningStyle.UrlSegment => "/api/v1",
        _                          => "/api",
    };
}
