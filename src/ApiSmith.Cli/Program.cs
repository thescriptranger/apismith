using System.Reflection;
using ApiSmith.Cli.Commands;
using ApiSmith.Core.Pipeline;
using SysConsole = System.Console;

namespace ApiSmith.Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
        {
            PrintHelp();
            return 0;
        }

        if (args[0] is "--version" or "-v" or "version")
        {
            SysConsole.WriteLine(GetVersion());
            return 0;
        }

        if (args[0] is "new")
        {
            var log = new ConsoleScaffoldLog();
            var (parsed, error) = ArgParser.ParseNew(args.AsSpan(1));
            if (error is not null)
            {
                log.Error(error);
                PrintNewHelp();
                return 64;
            }

            try
            {
                using var cts = new CancellationTokenSource();
                SysConsole.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                return await NewCommand.RunAsync(parsed, log, new ApiSmith.Console.ConsoleIO(), cts.Token).ConfigureAwait(false);
            }
            catch (System.Exception ex)
            {
                log.Error(ex.Message);
                return 1;
            }
        }

        SysConsole.Error.WriteLine($"apismith: unknown command '{args[0]}'. Try 'apismith --help'.");
        return 64;
    }

    private static string GetVersion()
    {
        var asm = typeof(Program).Assembly;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return info ?? asm.GetName().Version?.ToString() ?? "0.0.0";
    }

    private static void PrintHelp()
    {
        SysConsole.WriteLine($"apismith {GetVersion()}");
        SysConsole.WriteLine("Scaffold a .NET API from an existing SQL Server database.");
        SysConsole.WriteLine();
        SysConsole.WriteLine("Usage: apismith <command> [options]");
        SysConsole.WriteLine();
        SysConsole.WriteLine("Commands:");
        SysConsole.WriteLine("  new                Scaffold a new API.");
        SysConsole.WriteLine("  --version          Print the tool version.");
        SysConsole.WriteLine("  --help             Show this help.");
        SysConsole.WriteLine();
        PrintNewHelp();
    }

    private static void PrintNewHelp()
    {
        SysConsole.WriteLine("apismith new — scaffold an API from an existing SQL Server database.");
        SysConsole.WriteLine();
        SysConsole.WriteLine("Interactive wizard (default):");
        SysConsole.WriteLine("  apismith new");
        SysConsole.WriteLine();
        SysConsole.WriteLine("Replay a saved config:");
        SysConsole.WriteLine("  apismith new --config apismith.yaml [--connection \"...\"] [--output DIR]");
        SysConsole.WriteLine("  The connection string may also come from APISMITH_CONNECTION.");
        SysConsole.WriteLine();
        SysConsole.WriteLine("Scripted / non-interactive (no config file):");
        SysConsole.WriteLine("  apismith new --name MyApi --connection \"...\" [--output DIR] [--schema NAME]*");
    }
}
