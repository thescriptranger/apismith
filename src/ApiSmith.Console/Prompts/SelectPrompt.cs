using System.Collections.Immutable;

namespace ApiSmith.Console.Prompts;

/// <summary>Single-choice prompt. TTY: arrow-key loop. Non-TTY: numbered list.</summary>
public sealed class SelectPrompt<T> : IPrompt<T> where T : notnull
{
    public required string Label { get; init; }

    public required ImmutableArray<T> Options { get; init; }

    public int DefaultIndex { get; init; } = 0;

    public System.Func<T, string>? Describe { get; init; }

    public T Ask(IConsoleIO io)
    {
        if (Options.Length == 0)
        {
            throw new System.InvalidOperationException($"SelectPrompt '{Label}' has no options.");
        }

        return io.IsInputRedirected || io.IsOutputRedirected
            ? AskLineBased(io)
            : AskInteractive(io);
    }

    private T AskLineBased(IConsoleIO io)
    {
        io.WriteLine($"{Ansi.Bold}{Label}{Ansi.Reset}");
        for (var i = 0; i < Options.Length; i++)
        {
            var marker = i == DefaultIndex ? "*" : " ";
            io.WriteLine($"  {marker} [{i + 1}] {Display(Options[i])}");
        }

        while (true)
        {
            io.Write($"  Pick 1-{Options.Length} (default {DefaultIndex + 1}): ");
            var line = (io.ReadLine() ?? string.Empty).Trim();
            if (line.Length == 0)
            {
                return Options[DefaultIndex];
            }

            if (int.TryParse(line, out var idx) && idx >= 1 && idx <= Options.Length)
            {
                return Options[idx - 1];
            }

            io.WriteLine($"{Ansi.Red}  Enter a number from 1 to {Options.Length}.{Ansi.Reset}");
        }
    }

    private T AskInteractive(IConsoleIO io)
    {
        var cursor = System.Math.Clamp(DefaultIndex, 0, Options.Length - 1);
        io.WriteLine($"{Ansi.Bold}{Label}{Ansi.Reset} {Ansi.Dim}(use ↑/↓ then Enter){Ansi.Reset}");

        for (var i = 0; i < Options.Length; i++)
        {
            WriteRow(io, i, cursor);
        }

        while (true)
        {
            var key = io.ReadKey();
            switch (key.Key)
            {
                case System.ConsoleKey.UpArrow:
                    cursor = (cursor - 1 + Options.Length) % Options.Length;
                    break;
                case System.ConsoleKey.DownArrow:
                    cursor = (cursor + 1) % Options.Length;
                    break;
                case System.ConsoleKey.Enter:
                    return Options[cursor];
                default:
                    continue;
            }

            io.Write(Ansi.MoveUp(Options.Length));
            for (var i = 0; i < Options.Length; i++)
            {
                io.Write("\r" + Ansi.ClearLineFromCursor());
                WriteRow(io, i, cursor);
            }
        }
    }

    private void WriteRow(IConsoleIO io, int i, int cursor)
    {
        if (i == cursor)
        {
            io.WriteLine($"  {Ansi.Cyan}> {Display(Options[i])}{Ansi.Reset}");
        }
        else
        {
            io.WriteLine($"    {Display(Options[i])}");
        }
    }

    private string Display(T value) => Describe?.Invoke(value) ?? value.ToString() ?? string.Empty;
}
