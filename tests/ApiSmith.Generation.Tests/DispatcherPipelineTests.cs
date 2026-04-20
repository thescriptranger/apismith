using ApiSmith.Config;
using ApiSmith.Core.Pipeline;

namespace ApiSmith.Generation.Tests;

public sealed class DispatcherPipelineTests
{
    [Fact]
    public void Dispatcher_sendasync_composes_pipeline_behaviors()
    {
        var (config, output) = Setup("Pipe1");
        config.Architecture = ArchitectureStyle.VerticalSlice;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var disp = File.ReadAllText(Path.Combine(output, "src", "Pipe1", "Shared", "Dispatcher.cs"));

            // SendAsync must resolve behaviors from DI and invoke them around the handler.
            // The exact implementation is flexible, but SOMETHING that calls GetServices
            // (or similar) on the IPipelineBehavior<,> type must be present.
            Assert.Contains("IPipelineBehavior", disp);
            Assert.Contains("GetServices", disp);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void Pipeline_behavior_interface_has_next_delegate_parameter()
    {
        var (config, output) = Setup("Pipe2");
        config.Architecture = ArchitectureStyle.VerticalSlice;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var disp = File.ReadAllText(Path.Combine(output, "src", "Pipe2", "Shared", "Dispatcher.cs"));

            // The IPipelineBehavior<TRequest, TResponse>.HandleAsync must accept a
            // Func<Task<TResponse>> next parameter for composition to work.
            Assert.Contains("Func<Task<TResponse>> next", disp);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void VerticalSlice_emits_logging_behavior_example()
    {
        var (config, output) = Setup("Pipe3");
        config.Architecture = ArchitectureStyle.VerticalSlice;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var behaviorPath = Path.Combine(output, "src", "Pipe3", "Shared", "LoggingBehavior.cs");
            Assert.True(File.Exists(behaviorPath), $"Missing {behaviorPath}");
            var content = File.ReadAllText(behaviorPath);
            Assert.Contains("public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>", content);
            Assert.Contains("ILogger", content);
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void Non_verticalslice_does_not_emit_logging_behavior()
    {
        var (config, output) = Setup("Pipe4");
        config.Architecture = ArchitectureStyle.Flat;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            // Flat doesn't have Shared/ — LoggingBehavior.cs shouldn't exist anywhere.
            var path = Path.Combine(output, "src", "Pipe4", "Shared", "LoggingBehavior.cs");
            Assert.False(File.Exists(path));
        }
        finally { CleanupBestEffort(output); }
    }

    [Fact]
    public void Program_cs_registers_logging_behavior_when_verticalslice()
    {
        var (config, output) = Setup("Pipe5");
        config.Architecture = ArchitectureStyle.VerticalSlice;
        var graph = SchemaGraphFixtures.SmallBlog();

        try
        {
            new Generator(new NullLog()).Generate(config, graph, output);
            var program = File.ReadAllText(Path.Combine(output, "src", "Pipe5", "Program.cs"));
            Assert.Contains("AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>))", program);
        }
        finally { CleanupBestEffort(output); }
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
