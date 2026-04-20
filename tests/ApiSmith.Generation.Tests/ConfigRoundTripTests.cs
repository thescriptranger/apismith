using ApiSmith.Config;
using ApiSmith.Core.Pipeline;

namespace ApiSmith.Generation.Tests;

public sealed class ConfigRoundTripTests
{
    [Fact]
    public void Scaffold_yaml_replay_is_byte_identical()
    {
        var graph = SchemaGraphFixtures.Relational();

        var first = new ApiSmithConfig
        {
            ProjectName = "RoundTrip",
            OutputDirectory = "./RoundTrip",
            ConnectionString = "Server=x;Database=x;Trusted_Connection=True;",
        };

        var dir1 = Path.Combine(Path.GetTempPath(), "apismith-tests", "RT1-" + System.Guid.NewGuid().ToString("N")[..8]);
        var dir2 = Path.Combine(Path.GetTempPath(), "apismith-tests", "RT2-" + System.Guid.NewGuid().ToString("N")[..8]);

        try
        {
            new Generator(new NullLog()).Generate(first, graph, dir1);

            var yaml = File.ReadAllText(Path.Combine(dir1, "apismith.yaml"));
            var replay = YamlReader.Read(yaml);
            replay.ConnectionString = first.ConnectionString;   // connection isn't written; re-supply for scaffold

            new Generator(new NullLog()).Generate(replay, graph, dir2);

            var files1 = Directory.GetFiles(dir1, "*", SearchOption.AllDirectories)
                .Select(p => Path.GetRelativePath(dir1, p).Replace('\\', '/'))
                .OrderBy(p => p, System.StringComparer.Ordinal).ToArray();

            var files2 = Directory.GetFiles(dir2, "*", SearchOption.AllDirectories)
                .Select(p => Path.GetRelativePath(dir2, p).Replace('\\', '/'))
                .OrderBy(p => p, System.StringComparer.Ordinal).ToArray();

            Assert.Equal(files1, files2);

            foreach (var rel in files1)
            {
                var a = File.ReadAllBytes(Path.Combine(dir1, rel.Replace('/', Path.DirectorySeparatorChar)));
                var b = File.ReadAllBytes(Path.Combine(dir2, rel.Replace('/', Path.DirectorySeparatorChar)));
                Assert.True(a.SequenceEqual(b), $"File differs across round-trip: {rel}");
            }
        }
        finally
        {
            try { if (Directory.Exists(dir1)) Directory.Delete(dir1, recursive: true); } catch { /* ignore */ }
            try { if (Directory.Exists(dir2)) Directory.Delete(dir2, recursive: true); } catch { /* ignore */ }
        }
    }

    private sealed class NullLog : IScaffoldLog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
