using ApiSmith.Config;
using ApiSmith.Console;
using ApiSmith.Console.Wizard;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation;
using ApiSmith.Introspection;

namespace ApiSmith.Cli.Commands;

internal static class NewCommand
{
    public static async Task<int> RunAsync(NewCommandArgs args, IScaffoldLog log, IConsoleIO console, CancellationToken ct)
    {
        ApiSmithConfig config;

        if (!string.IsNullOrWhiteSpace(args.ConfigPath))
        {
            if (!File.Exists(args.ConfigPath))
            {
                log.Error($"Config file not found: {args.ConfigPath}");
                return 66;
            }

            try
            {
                config = YamlReader.Read(File.ReadAllText(args.ConfigPath));
            }
            catch (YamlException ex)
            {
                log.Error($"Parse error in {args.ConfigPath}: {ex.Message}");
                return 65;
            }

            // precedence: CLI flag > env var > config file
            var connection = args.ConnectionString
                             ?? System.Environment.GetEnvironmentVariable("APISMITH_CONNECTION")
                             ?? (string.IsNullOrWhiteSpace(config.ConnectionString) ? null : config.ConnectionString);

            if (string.IsNullOrWhiteSpace(connection))
            {
                log.Error("No connection string. Supply --connection or set APISMITH_CONNECTION.");
                return 64;
            }

            config.ConnectionString = connection;

            if (!string.IsNullOrWhiteSpace(args.Output))
            {
                config.OutputDirectory = args.Output;
            }
        }
        else if (args.WizardRequested(requireConnection: false))
        {
            var wizard = new WizardRunner(console);
            config = wizard.GatherStaticChoices();

            if (!string.IsNullOrWhiteSpace(args.Output))
            {
                config.OutputDirectory = args.Output;
            }
        }
        else if (!string.IsNullOrWhiteSpace(args.Name) && !string.IsNullOrWhiteSpace(args.ConnectionString))
        {
            // scriptable path: no config file
            config = new ApiSmithConfig
            {
                ProjectName = args.Name!,
                OutputDirectory = string.IsNullOrWhiteSpace(args.Output) ? $"./{args.Name}" : args.Output!,
                ConnectionString = args.ConnectionString!,
            };

            if (args.Schemas.Count > 0)
            {
                config.Schemas = args.Schemas.ToList();
            }
        }
        else
        {
            log.Error("Supply --config FILE, or --name + --connection, or run `apismith new` in an interactive terminal.");
            return 64;
        }

        return await ScaffoldAsync(config, log, ct).ConfigureAwait(false);
    }

    private static async Task<int> ScaffoldAsync(ApiSmithConfig config, IScaffoldLog log, CancellationToken ct)
    {
        log.Info("Validating connection…");
        var probe = await SqlServerSchemaReader.ValidateAsync(config.ConnectionString, ct).ConfigureAwait(false);
        if (!probe.IsValid)
        {
            log.Error($"Cannot connect to SQL Server: {probe.ErrorMessage}");
            return 69; // sysexits EX_UNAVAILABLE
        }

        log.Info("Reading schema from SQL Server…");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        SchemaGraphResult graph;
        try
        {
            var reader = new SqlServerSchemaReader();
            var schema = await reader.ReadAsync(
                config.ConnectionString,
                config.Schemas.Count == 0 ? null : config.Schemas,
                ct).ConfigureAwait(false);
            graph = new SchemaGraphResult(schema, sw.Elapsed);
        }
        catch (System.Exception ex)
        {
            log.Error($"Schema introspection failed: {ex.Message}");
            return 70;
        }

        log.Info($"Introspection: {graph.Schema.Schemas.Sum(s => s.Tables.Length)} tables across {graph.Schema.Schemas.Length} schemas in {graph.Duration.TotalMilliseconds:F0} ms.");

        var generator = new Generator(log);
        var report = generator.Generate(config, graph.Schema, config.OutputDirectory);
        var totalMs = (graph.Duration + report.TotalDuration).TotalMilliseconds;

        log.Info($"Output: {Path.GetFullPath(config.OutputDirectory)}");
        log.Info($"Total time (introspect + scaffold): {totalMs:F0} ms.");
        return 0;
    }

    private sealed record SchemaGraphResult(ApiSmith.Core.Model.SchemaGraph Schema, System.TimeSpan Duration);
}

internal sealed class NewCommandArgs
{
    public string? Name { get; set; }
    public string? Output { get; set; }
    public string? ConnectionString { get; set; }
    public string? ConfigPath { get; set; }
    public List<string> Schemas { get; } = new();

    public bool WizardRequested(bool requireConnection) =>
        string.IsNullOrWhiteSpace(ConfigPath)
        && string.IsNullOrWhiteSpace(Name)
        && (!requireConnection || string.IsNullOrWhiteSpace(ConnectionString));
}
