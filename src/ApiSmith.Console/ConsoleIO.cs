namespace ApiSmith.Console;

public sealed class ConsoleIO : IConsoleIO
{
    public bool IsInputRedirected => System.Console.IsInputRedirected;

    public bool IsOutputRedirected => System.Console.IsOutputRedirected;

    public void Write(string text) => System.Console.Out.Write(text);

    public void WriteLine(string text) => System.Console.Out.WriteLine(text);

    public string? ReadLine() => System.Console.In.ReadLine();

    public System.ConsoleKeyInfo ReadKey() => System.Console.ReadKey(intercept: true);
}
