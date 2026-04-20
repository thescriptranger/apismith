using System.Collections.Immutable;
using System.Diagnostics;
using ApiSmith.Config;
using ApiSmith.Core.Model;
using ApiSmith.Core.Pipeline;
using ApiSmith.Introspection;

namespace ApiSmith.Generation.Tests;

public sealed class AuthAndVersioningTests
{
    public static TheoryData<AuthStyle> AllAuthStyles() =>
        new()
        {
            AuthStyle.None,
            AuthStyle.JwtBearer,
            AuthStyle.Auth0,
            AuthStyle.AzureAd,
            AuthStyle.ApiKey,
        };

    public static TheoryData<VersioningStyle> AllVersioningStyles() =>
        new()
        {
            VersioningStyle.None,
            VersioningStyle.UrlSegment,
            VersioningStyle.Header,
            VersioningStyle.QueryString,
        };

    [Theory]
    [MemberData(nameof(AllAuthStyles))]
    public void Each_auth_variant_compiles(AuthStyle auth)
    {
        RunParagon("Auth", c => c.Auth = auth);
    }

    [Theory]
    [MemberData(nameof(AllVersioningStyles))]
    public void Each_versioning_variant_compiles(VersioningStyle versioning)
    {
        RunParagon("Ver", c => c.Versioning = versioning);
    }

    [Fact]
    public void Sprocs_and_functions_generate_compilable_services()
    {
        var schema = SchemaWithSprocsAndFunctions();

        RunParagonWithSchema("SpFn", _ => { }, schema);
    }

    [Fact]
    public void Initial_migration_flag_emits_script()
    {
        var output = Path.Combine(Path.GetTempPath(), "apismith-tests",
            "Migr-" + System.Guid.NewGuid().ToString("N")[..8]);
        var config = new ApiSmithConfig
        {
            ProjectName = "MigrApi",
            OutputDirectory = output,
            ConnectionString = "Server=x;Database=x;",
            GenerateInitialMigration = true,
        };

        try
        {
            new Generator(new NullLog()).Generate(config, SchemaGraphFixtures.Relational(), output);

            var ps1Path = Path.Combine(output, "scripts", "add-initial-migration.ps1");
            var shPath = Path.Combine(output, "scripts", "add-initial-migration.sh");

            Assert.True(File.Exists(ps1Path));
            Assert.True(File.Exists(shPath));

            var sh = File.ReadAllText(shPath);
            Assert.StartsWith("#!/usr/bin/env bash", sh);

            var ps1Commands = ExtractMigrationCommands(File.ReadAllText(ps1Path));
            var shCommands = ExtractMigrationCommands(sh);
            Assert.Equal(ps1Commands, shCommands);
        }
        finally
        {
            CleanupBestEffort(output);
        }
    }

    [Fact]
    public void Initial_migration_flag_false_emits_no_scripts()
    {
        var output = Path.Combine(Path.GetTempPath(), "apismith-tests",
            "NoMigr-" + System.Guid.NewGuid().ToString("N")[..8]);
        var config = new ApiSmithConfig
        {
            ProjectName = "NoMigrApi",
            OutputDirectory = output,
            ConnectionString = "Server=x;Database=x;",
            GenerateInitialMigration = false,
        };

        try
        {
            new Generator(new NullLog()).Generate(config, SchemaGraphFixtures.Relational(), output);
            Assert.False(File.Exists(Path.Combine(output, "scripts", "add-initial-migration.ps1")));
            Assert.False(File.Exists(Path.Combine(output, "scripts", "add-initial-migration.sh")));
        }
        finally
        {
            CleanupBestEffort(output);
        }
    }

    /// <summary>Strips shell/PS syntax so only the underlying `dotnet ef` invocations remain for byte comparison.</summary>
    private static string ExtractMigrationCommands(string script)
    {
        var lines = script.Replace("\r\n", "\n").Split('\n');
        var normalized = new List<string>();
        foreach (var raw in lines)
        {
            var line = raw.Trim().TrimEnd('`', '\\').Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("#", System.StringComparison.Ordinal)) continue;
            if (line.StartsWith("echo ", System.StringComparison.Ordinal)) continue;
            if (line.StartsWith("Write-Host", System.StringComparison.Ordinal)) continue;
            if (line.StartsWith("$", System.StringComparison.Ordinal)) continue;
            if (line.StartsWith("set -", System.StringComparison.Ordinal)) continue;
            if (line.StartsWith("if ", System.StringComparison.Ordinal)) continue;
            if (line.Contains("tool list", System.StringComparison.Ordinal)) continue;
            if (line.Contains("tool install", System.StringComparison.Ordinal)) continue;
            if (line == "}" || line == "{") continue;
            normalized.Add(line);
        }
        return string.Join("\n", normalized);
    }

    private static void CleanupBestEffort(string directory)
    {
        try { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); } catch { /* ignore */ }
    }

    private static void RunParagon(string label, System.Action<ApiSmithConfig> tweak)
    {
        RunParagonWithSchema(label, tweak, SchemaGraphFixtures.Relational());
    }

    private static void RunParagonWithSchema(string label, System.Action<ApiSmithConfig> tweak, SchemaGraph schema)
    {
        if (System.Environment.GetEnvironmentVariable("APISMITH_SKIP_NESTED_BUILD") is { Length: > 0 })
        {
            return;
        }

        var output = Path.Combine(Path.GetTempPath(), "apismith-tests",
            $"{label}-{System.Guid.NewGuid().ToString("N")[..8]}");
        var config = new ApiSmithConfig
        {
            ProjectName = "Acme",
            OutputDirectory = output,
            ConnectionString = "Server=x;Database=x;",
        };
        tweak(config);

        try
        {
            new Generator(new NullLog()).Generate(config, schema, output);

            var psi = new ProcessStartInfo("dotnet", $"build \"{output}\" --nologo -clp:NoSummary")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            using var proc = Process.Start(psi)!;
            var stdout = proc.StandardOutput.ReadToEnd();
            var stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            Assert.True(proc.ExitCode == 0,
                $"{label} failed to build. exit={proc.ExitCode}\n{stdout}\n{stderr}");
        }
        finally
        {
            try { if (Directory.Exists(output)) Directory.Delete(output, recursive: true); } catch { /* ignore */ }
        }
    }

    private static SchemaGraph SchemaWithSprocsAndFunctions()
    {
        var users = Table.Create("dbo", "users",
            new[]
            {
                new Column("id", 1, "int", IsNullable: false, IsIdentity: true, IsComputed: false, MaxLength: null, Precision: null, Scale: null, DefaultValue: null),
                new Column("email", 2, "nvarchar", IsNullable: false, IsIdentity: false, IsComputed: false, MaxLength: 256, Precision: null, Scale: null, DefaultValue: null),
            },
            PrimaryKey.Create("PK_users", new[] { "id" }));

        var sproc = StoredProcedure.Create(
            "dbo",
            "usp_get_users_by_domain",
            new[]
            {
                new SprocParameter("domain", 1, "nvarchar", IsNullable: false, Direction: ParameterDirection.In, MaxLength: 100, Precision: null, Scale: null),
            },
            resultColumns: new[]
            {
                new ResultColumn("id", 1, "int", IsNullable: false),
                new ResultColumn("email", 2, "nvarchar", IsNullable: false),
            });

        var indeterminate = StoredProcedure.Create(
            "dbo",
            "usp_dynamic",
            ImmutableArray<SprocParameter>.Empty,
            resultIsIndeterminate: true,
            indeterminateReason: "dynamic SQL — cannot infer result shape");

        var scalarFn = DbFunction.Create("dbo", "fn_user_count", FunctionKind.Scalar,
            parameters: ImmutableArray<SprocParameter>.Empty,
            returnSqlType: "int");

        return SqlServerSchemaReader.BuildGraph(
            new[] { users },
            System.Array.Empty<ForeignKey>(),
            System.Array.Empty<View>(),
            new[] { sproc, indeterminate },
            new[] { scalarFn });
    }

    private sealed class NullLog : IScaffoldLog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
