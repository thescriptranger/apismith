using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;

namespace ApiSmith.Generation.Emitters;

/// <summary>Emits Dockerfile + docker-compose.yml (api + SQL Server) when <c>IncludeDockerAssets</c> is set.</summary>
public static class DockerEmitter
{
    public static IEnumerable<EmittedFile> Emit(ApiSmithConfig config, IArchitectureLayout layout)
    {
        var apiProject = layout.ApiProjectAssemblyName(config);
        var apiFolder = layout.ApiProjectFolder(config);
        var tfm = config.TargetFramework;
        var dotnetTag = tfm switch
        {
            "net8.0" => "8.0",
            "net9.0" => "9.0",
            "net10.0" => "10.0",
            _ => "9.0",
        };

        // Copy entire src/ so multi-project references resolve.
        var dockerfile = $"""
            # syntax=docker/dockerfile:1

            FROM mcr.microsoft.com/dotnet/sdk:{dotnetTag} AS build
            WORKDIR /source

            COPY Directory.Build.props* ./
            COPY Directory.Packages.props* ./
            COPY global.json* ./
            COPY src/ ./src/

            WORKDIR /source/{apiFolder}
            RUN dotnet restore
            RUN dotnet publish -c Release -o /app --no-restore

            FROM mcr.microsoft.com/dotnet/aspnet:{dotnetTag} AS final
            WORKDIR /app
            COPY --from=build /app ./

            ENV ASPNETCORE_URLS=http://+:8080
            EXPOSE 8080

            ENTRYPOINT ["dotnet", "{apiProject}.dll"]
            """;

        var compose = $$""""
            services:
              api:
                build:
                  context: .
                  dockerfile: Dockerfile
                ports:
                  - "8080:8080"
                environment:
                  ASPNETCORE_ENVIRONMENT: Development
                  ConnectionStrings__DefaultConnection: "Server=db,1433;Database={{config.ProjectName}};User Id=sa;Password=${SA_PASSWORD:-Strong!Passw0rd};TrustServerCertificate=True;"
                depends_on:
                  db:
                    condition: service_healthy

              db:
                image: mcr.microsoft.com/mssql/server:2022-latest
                environment:
                  ACCEPT_EULA: "Y"
                  MSSQL_SA_PASSWORD: "${SA_PASSWORD:-Strong!Passw0rd}"
                  MSSQL_PID: "Developer"
                ports:
                  - "1433:1433"
                volumes:
                  - mssql-data:/var/opt/mssql
                healthcheck:
                  test: ["CMD-SHELL", "/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P \"$${SA_PASSWORD:-Strong!Passw0rd}\" -Q 'SELECT 1' || exit 1"]
                  interval: 10s
                  timeout: 5s
                  retries: 10

            volumes:
              mssql-data:
            """";

        yield return new EmittedFile(layout.DockerfilePath(), dockerfile);
        yield return new EmittedFile(layout.DockerComposePath(), compose);
    }
}
