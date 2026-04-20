namespace ApiSmith.Console;

/// <summary>Test seam for <see cref="System.Console"/>; prompts use this so stdin/stdout can be faked.</summary>
public interface IConsoleIO
{
    bool IsInputRedirected { get; }

    bool IsOutputRedirected { get; }

    void Write(string text);

    void WriteLine(string text);

    string? ReadLine();

    /// <summary>TTY only.</summary>
    System.ConsoleKeyInfo ReadKey();
}
