using System.Diagnostics;
using ApiSmith.Config;
using ApiSmith.Core.Pipeline;

namespace ApiSmith.Generation.Tests;

public sealed class OrthogonalAxisTests
{
    public static TheoryData<string, ApiSmithConfig> Paragons()
    {
        ApiSmithConfig MakeDefault(string name) => new()
        {
            ProjectName = name,
            Architecture = ArchitectureStyle.Flat,
            EndpointStyle = EndpointStyle.Controllers,
            DataAccess = DataAccessStyle.EfCore,
            ConnectionString = "Server=x;Database=x;",
        };

        var minimalApi = MakeDefault("MinApi");
        minimalApi.EndpointStyle = EndpointStyle.MinimalApi;

        var dapperEf = MakeDefault("DapperApi");
        dapperEf.DataAccess = DataAccessStyle.Dapper;

        var dapperMin = MakeDefault("DapperMin");
        dapperMin.EndpointStyle = EndpointStyle.MinimalApi;
        dapperMin.DataAccess = DataAccessStyle.Dapper;

        var withTests = MakeDefault("TestsApi");
        withTests.IncludeTestsProject = true;

        var withDocker = MakeDefault("DockerApi");
        withDocker.IncludeDockerAssets = true;

        var vsa = MakeDefault("VsaApi");
        vsa.Architecture = ArchitectureStyle.VerticalSlice;

        var cleanDapper = MakeDefault("CleanDapper");
        cleanDapper.Architecture = ArchitectureStyle.Clean;
        cleanDapper.DataAccess = DataAccessStyle.Dapper;

        var everything = MakeDefault("Everything");
        everything.Architecture = ArchitectureStyle.Clean;
        everything.EndpointStyle = EndpointStyle.MinimalApi;
        everything.DataAccess = DataAccessStyle.EfCore;
        everything.IncludeTestsProject = true;
        everything.IncludeDockerAssets = true;

        return new()
        {
            { "MinimalApi_EfCore_Flat", minimalApi },
            { "Controllers_Dapper_Flat", dapperEf },
            { "MinimalApi_Dapper_Flat", dapperMin },
            { "WithTestsProject", withTests },
            { "WithDocker", withDocker },
            { "VerticalSlice_Dispatcher", vsa },
            { "Clean_Dapper", cleanDapper },
            { "Clean_MinApi_EfCore_Tests_Docker", everything },
        };
    }

    [Theory]
    [MemberData(nameof(Paragons))]
    public void Paragon_compiles(string label, ApiSmithConfig config)
    {
        if (System.Environment.GetEnvironmentVariable("APISMITH_SKIP_NESTED_BUILD") is { Length: > 0 })
        {
            return;
        }

        var output = Path.Combine(Path.GetTempPath(), "apismith-tests",
            $"{label}-{System.Guid.NewGuid().ToString("N")[..8]}");
        config.OutputDirectory = output;

        try
        {
            new Generator(new NullLog()).Generate(config, SchemaGraphFixtures.Relational(), output);

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

    private sealed class NullLog : IScaffoldLog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
