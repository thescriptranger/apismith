using System.Text;
using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;

namespace ApiSmith.Generation.Emitters;

public static class AppSettingsEmitter
{
    public static IEnumerable<EmittedFile> Emit(ApiSmithConfig config, IArchitectureLayout layout)
    {
        var connSnippet = string.IsNullOrWhiteSpace(config.ConnectionString)
            ? "Server=localhost;Database=YourDb;Trusted_Connection=True;TrustServerCertificate=True;"
            : EscapeForJson(config.ConnectionString);

        var authFragment = AuthEmitter.AppSettingsFragment(config);

        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"Logging\": {");
        sb.AppendLine("    \"LogLevel\": {");
        sb.AppendLine("      \"Default\": \"Information\",");
        sb.AppendLine("      \"Microsoft.AspNetCore\": \"Warning\"");
        sb.AppendLine("    }");
        sb.AppendLine("  },");
        sb.AppendLine("  \"AllowedHosts\": \"*\",");
        if (authFragment is not null)
        {
            sb.Append(authFragment);
        }
        sb.AppendLine("  \"ConnectionStrings\": {");
        sb.AppendLine($"    \"DefaultConnection\": \"{connSnippet}\"");
        sb.AppendLine("  }");
        sb.AppendLine("}");

        var devSettings = """
            {
              "Logging": {
                "LogLevel": {
                  "Default": "Debug",
                  "Microsoft.AspNetCore": "Information"
                }
              }
            }
            """;

        yield return new EmittedFile(layout.AppSettingsPath(config), sb.ToString());
        yield return new EmittedFile(layout.AppSettingsDevPath(config), devSettings);
    }

    private static string EscapeForJson(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
