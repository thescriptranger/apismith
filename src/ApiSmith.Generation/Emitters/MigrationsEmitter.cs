using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;

namespace ApiSmith.Generation.Emitters;

/// <summary>FR-24: emits a helper script to add InitialCreate and bootstrap __EFMigrationsHistory — avoids coupling scaffold to the dev machine's <c>dotnet ef</c>.</summary>
public static class MigrationsEmitter
{
    public static IEnumerable<EmittedFile> Emit(ApiSmithConfig config, IArchitectureLayout layout)
    {
        if (!config.GenerateInitialMigration || config.DataAccess is not DataAccessStyle.EfCore)
        {
            yield break;
        }

        var apiProjectFile = layout.ApiProjectFolder(config) + "/" + layout.ApiProjectAssemblyName(config) + ".csproj";

        var ps1Script = $$"""
            # Creates the InitialCreate migration from the existing schema and marks the
            # live database as already-applied. Run once after scaffold.
            #
            # Usage (from scaffold root):
            #   pwsh ./scripts/add-initial-migration.ps1

            $ErrorActionPreference = 'Stop'

            Write-Host 'Installing dotnet-ef if missing…'
            $tool = dotnet tool list -g | Select-String -Pattern 'dotnet-ef'
            if (-not $tool) {
                dotnet tool install -g dotnet-ef
            }

            Write-Host 'Adding InitialCreate migration…'
            dotnet ef migrations add InitialCreate `
                --project {{apiProjectFile}} `
                --startup-project {{apiProjectFile}}

            Write-Host 'Bootstrapping __EFMigrationsHistory so the existing DB is treated as applied…'
            dotnet ef migrations script `
                --project {{apiProjectFile}} `
                --startup-project {{apiProjectFile}} `
                --idempotent `
                --output _ef_bootstrap.sql

            Write-Host 'Done. Review _ef_bootstrap.sql before running it against the live database.'
            Write-Host 'Typical command:'
            Write-Host '  sqlcmd -S <server> -d <database> -i _ef_bootstrap.sql'
            """;

        var shScript = $$"""
            #!/usr/bin/env bash
            # Creates the InitialCreate migration from the existing schema and marks the
            # live database as already-applied. Run once after scaffold.
            #
            # Usage (from scaffold root):
            #   bash ./scripts/add-initial-migration.sh
            # (or `chmod +x scripts/add-initial-migration.sh` once, then run directly)
            #
            # Requires: dotnet SDK on PATH. The bootstrap SQL is applied separately with
            # sqlcmd — on Linux/macOS install mssql-tools so `sqlcmd` resolves on PATH.

            set -euo pipefail

            echo 'Installing dotnet-ef if missing…'
            dotnet tool list --global | grep -q dotnet-ef || dotnet tool install --global dotnet-ef

            echo 'Adding InitialCreate migration…'
            dotnet ef migrations add InitialCreate \
                --project {{apiProjectFile}} \
                --startup-project {{apiProjectFile}}

            echo 'Bootstrapping __EFMigrationsHistory so the existing DB is treated as applied…'
            dotnet ef migrations script \
                --project {{apiProjectFile}} \
                --startup-project {{apiProjectFile}} \
                --idempotent \
                --output _ef_bootstrap.sql

            echo 'Done. Review _ef_bootstrap.sql before running it against the live database.'
            echo 'Typical command:'
            echo '  sqlcmd -S <server> -d <database> -i _ef_bootstrap.sql'
            """;

        yield return new EmittedFile("scripts/add-initial-migration.ps1", ps1Script);
        yield return new EmittedFile("scripts/add-initial-migration.sh", shScript);
    }
}
