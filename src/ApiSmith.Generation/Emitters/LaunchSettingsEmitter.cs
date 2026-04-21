using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;

namespace ApiSmith.Generation.Emitters;

/// <summary>Emits Properties/launchSettings.json so `dotnet run` / IDE F5 opens the Scalar UI at /scalar/v1.</summary>
public static class LaunchSettingsEmitter
{
    public static EmittedFile Emit(ApiSmithConfig config, IArchitectureLayout layout)
    {
        var path = $"{layout.ApiProjectFolder(config)}/Properties/launchSettings.json";

        var content = """
            {
              "$schema": "https://json.schemastore.org/launchsettings.json",
              "profiles": {
                "https": {
                  "commandName": "Project",
                  "launchBrowser": true,
                  "launchUrl": "scalar/v1",
                  "applicationUrl": "https://localhost:5001;http://localhost:5000",
                  "environmentVariables": {
                    "ASPNETCORE_ENVIRONMENT": "Development"
                  }
                },
                "http": {
                  "commandName": "Project",
                  "launchBrowser": true,
                  "launchUrl": "scalar/v1",
                  "applicationUrl": "http://localhost:5000",
                  "environmentVariables": {
                    "ASPNETCORE_ENVIRONMENT": "Development"
                  }
                }
              }
            }
            """;

        return new EmittedFile(path, content);
    }
}
