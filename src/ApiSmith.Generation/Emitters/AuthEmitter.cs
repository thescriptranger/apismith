using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;

namespace ApiSmith.Generation.Emitters;

/// <summary>FR-13: emits auth wiring (Microsoft packages only) via Program.cs fragments, appsettings keys, and an API-key middleware.</summary>
public static class AuthEmitter
{
    public static IEnumerable<EmittedFile> Emit(ApiSmithConfig config, IArchitectureLayout layout)
    {
        if (config.Auth is AuthStyle.ApiKey)
        {
            var ns = layout.ApiNamespace(config);
            var folder = layout.ApiProjectFolder(config);
            var content = $$"""
                using Microsoft.AspNetCore.Http;
                using Microsoft.Extensions.Configuration;

                namespace {{ns}}.Auth;

                /// <summary>
                /// Requires every request (outside Dev-only OpenAPI/Scalar endpoints) to
                /// carry a correct <c>X-Api-Key</c> header. Configure the expected key in
                /// <c>ApiKey:Value</c>.
                /// </summary>
                public sealed class ApiKeyMiddleware
                {
                    private readonly RequestDelegate _next;
                    private readonly string? _expected;

                    public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
                    {
                        _next = next;
                        _expected = configuration["ApiKey:Value"];
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

                        if (string.IsNullOrEmpty(_expected))
                        {
                            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                            await context.Response.WriteAsync("Server API key not configured.").ConfigureAwait(false);
                            return;
                        }

                        var provided = context.Request.Headers["X-Api-Key"].ToString();
                        if (!string.Equals(provided, _expected, System.StringComparison.Ordinal))
                        {
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return;
                        }

                        await _next(context).ConfigureAwait(false);
                    }
                }
                """;
            yield return new EmittedFile($"{folder}/Auth/ApiKeyMiddleware.cs", content);
        }
    }

    /// <summary>Lines contributed to Program.cs between AddControllers/AddOpenApi and app.Build().</summary>
    public static IEnumerable<string> ServiceRegistrations(ApiSmithConfig config) => config.Auth switch
    {
        AuthStyle.None       => System.Array.Empty<string>(),
        AuthStyle.JwtBearer  => JwtRegistrations(),
        AuthStyle.Auth0      => JwtRegistrations(),   // JWT bearer, different authority
        AuthStyle.AzureAd    => JwtRegistrations(),   // JWT bearer, different authority
        AuthStyle.ApiKey     => System.Array.Empty<string>(), // registered in PipelineUsage
        _ => System.Array.Empty<string>(),
    };

    /// <summary>Lines contributed to Program.cs between app.Build() and MapControllers/MapXxxEndpoints.</summary>
    public static IEnumerable<string> PipelineUsage(ApiSmithConfig config) => config.Auth switch
    {
        AuthStyle.None       => System.Array.Empty<string>(),
        AuthStyle.JwtBearer or AuthStyle.Auth0 or AuthStyle.AzureAd =>
            new[]
            {
                "app.UseAuthentication();",
                "app.UseAuthorization();",
            },
        AuthStyle.ApiKey     => new[] { "app.UseMiddleware<ApiKeyMiddleware>();" },
        _ => System.Array.Empty<string>(),
    };

    public static IEnumerable<string> ExtraUsings(ApiSmithConfig config, IArchitectureLayout layout) => config.Auth switch
    {
        AuthStyle.JwtBearer or AuthStyle.Auth0 or AuthStyle.AzureAd =>
            new[]
            {
                "Microsoft.AspNetCore.Authentication.JwtBearer",
                "Microsoft.IdentityModel.Tokens",
                "System.Text",
            },
        AuthStyle.ApiKey     => new[] { $"{layout.ApiNamespace(config)}.Auth" },
        _ => System.Array.Empty<string>(),
    };

    public static IEnumerable<string> CsprojPackageRefs(ApiSmithConfig config) => config.Auth switch
    {
        AuthStyle.JwtBearer or AuthStyle.Auth0 or AuthStyle.AzureAd =>
            new[]
            {
                $"<PackageReference Include=\"Microsoft.AspNetCore.Authentication.JwtBearer\" Version=\"{CsprojTemplates.EfCoreVersion}\" />",
            },
        _ => System.Array.Empty<string>(),
    };

    /// <summary>Extra keys for appsettings.json, structured as JSON fragments per strategy.</summary>
    public static string? AppSettingsFragment(ApiSmithConfig config) => config.Auth switch
    {
        AuthStyle.JwtBearer => """
              "Jwt": {
                "Authority": "https://issuer.example.com",
                "Audience": "apismith"
              },
            """,
        AuthStyle.Auth0 => """
              "Auth0": {
                "Domain": "your-tenant.us.auth0.com",
                "Audience": "apismith"
              },
            """,
        AuthStyle.AzureAd => """
              "AzureAd": {
                "Instance": "https://login.microsoftonline.com/",
                "TenantId": "00000000-0000-0000-0000-000000000000",
                "ClientId": "00000000-0000-0000-0000-000000000000"
              },
            """,
        AuthStyle.ApiKey => """
              "ApiKey": {
                "Value": "replace-me-with-a-real-secret"
              },
            """,
        _ => null,
    };

    private static IEnumerable<string> JwtRegistrations() => new[]
    {
        "builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)",
        "    .AddJwtBearer(options =>",
        "    {",
        "        options.Authority = builder.Configuration[\"Jwt:Authority\"] ?? builder.Configuration[\"Auth0:Domain\"] ?? builder.Configuration[\"AzureAd:Instance\"];",
        "        options.Audience  = builder.Configuration[\"Jwt:Audience\"]  ?? builder.Configuration[\"Auth0:Audience\"]  ?? builder.Configuration[\"AzureAd:ClientId\"];",
        "        options.TokenValidationParameters = new TokenValidationParameters",
        "        {",
        "            ValidateIssuer = true,",
        "            ValidateAudience = true,",
        "            ValidateLifetime = true,",
        "            ValidateIssuerSigningKey = true,",
        "        };",
        "    });",
        "builder.Services.AddAuthorization();",
    };
}
