using System.Text.RegularExpressions;
using ApiSmith.Config;
using ApiSmith.Core.Pipeline;

namespace ApiSmith.Generation.Tests;

public sealed class PackagePolicyTests
{
    [Theory]
    [InlineData(DataAccessStyle.EfCore,  false)]
    [InlineData(DataAccessStyle.Dapper,  false)]
    [InlineData(DataAccessStyle.EfCore,  true)]  // with tests project
    [InlineData(DataAccessStyle.Dapper,  true)]
    public void Emitted_csprojs_reference_only_allow_listed_packages(DataAccessStyle dataAccess, bool includeTests)
    {
        var (config, output) = Setup($"Pkg{dataAccess}{(includeTests ? "T" : "")}");
        config.DataAccess = dataAccess;
        config.IncludeTestsProject = includeTests;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);

            var violations = new List<string>();
            var csprojs = Directory.GetFiles(output, "*.csproj", SearchOption.AllDirectories);
            Assert.NotEmpty(csprojs);

            foreach (var path in csprojs)
            {
                var content = File.ReadAllText(path);
                var isTestsProject = path.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}")
                    || path.Contains("/tests/");

                foreach (Match m in Regex.Matches(content, "<PackageReference\\s+Include=\"([^\"]+)\""))
                {
                    var name = m.Groups[1].Value;
                    if (!IsAllowed(name, dataAccess, isTestsProject))
                    {
                        violations.Add($"{Path.GetFileName(path)}: {name}");
                    }
                }
            }

            Assert.True(violations.Count == 0, "Disallowed packages: " + string.Join(", ", violations));
        }
        finally
        {
            CleanupBestEffort(output);
        }
    }

    private static bool IsAllowed(string name, DataAccessStyle dataAccess, bool isTestsProject)
    {
        if (name.StartsWith("Microsoft.", System.StringComparison.Ordinal)) return true;
        if (name == "Scalar.AspNetCore") return true;
        if (name == "Dapper") return dataAccess == DataAccessStyle.Dapper;
        if (isTestsProject && (name.StartsWith("xunit", System.StringComparison.Ordinal) || name == "coverlet.collector")) return true;
        return false;
    }

    private static (ApiSmithConfig Config, string Output) Setup(string projectName)
    {
        var output = Path.Combine(Path.GetTempPath(), "apismith-tests", projectName + "-" + System.Guid.NewGuid().ToString("N")[..8]);
        var config = new ApiSmithConfig
        {
            ProjectName = projectName,
            OutputDirectory = output,
            ConnectionString = "Server=test;Database=test;Trusted_Connection=True;",
        };
        return (config, output);
    }

    private static void CleanupBestEffort(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Don't fail the test because of cleanup; CI temp dirs get reaped anyway.
        }
    }

    private sealed class NullLog : IScaffoldLog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
