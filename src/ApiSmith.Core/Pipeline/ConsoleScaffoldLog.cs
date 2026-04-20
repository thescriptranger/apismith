namespace ApiSmith.Core.Pipeline;

public sealed class ConsoleScaffoldLog : IScaffoldLog
{
    public void Info(string message) => Console.Out.WriteLine(message);

    public void Warn(string message) => WriteWithColor(ConsoleColor.Yellow, $"warn: {message}");

    public void Error(string message) => WriteWithColor(ConsoleColor.Red, $"error: {message}");

    private static void WriteWithColor(ConsoleColor color, string message)
    {
        var previous = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = color;
            Console.Error.WriteLine(message);
        }
        finally
        {
            Console.ForegroundColor = previous;
        }
    }
}
