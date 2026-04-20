using System.Collections.Immutable;

namespace ApiSmith.Console.Prompts;

/// <summary>Multi-choice prompt. TTY: space toggles, enter commits. Non-TTY: comma-separated indices.</summary>
public sealed class MultiSelectPrompt<T> : IPrompt<ImmutableArray<T>> where T : notnull
{
    public required string Label { get; init; }

    public required ImmutableArray<T> Options { get; init; }

    /// <summary>Preselected indices. Empty = none.</summary>
    public ImmutableArray<int> DefaultSelection { get; init; } = ImmutableArray<int>.Empty;

    public System.Func<T, string>? Describe { get; init; }

    public ImmutableArray<T> Ask(IConsoleIO io)
    {
        if (Options.Length == 0)
        {
            return ImmutableArray<T>.Empty;
        }

        return io.IsInputRedirected || io.IsOutputRedirected
            ? AskLineBased(io)
            : AskInteractive(io);
    }

    private ImmutableArray<T> AskLineBased(IConsoleIO io)
    {
        io.WriteLine($"{Ansi.Bold}{Label}{Ansi.Reset}");

        var defaultSet = new HashSet<int>(DefaultSelection.IsDefault ? System.Array.Empty<int>() : DefaultSelection);
        for (var i = 0; i < Options.Length; i++)
        {
            var marker = defaultSet.Contains(i) ? "*" : " ";
            io.WriteLine($"  {marker} [{i + 1}] {Display(Options[i])}");
        }

        while (true)
        {
            io.Write("  Indices (comma-separated), * for all, blank for defaults: ");
            var line = (io.ReadLine() ?? string.Empty).Trim();

            if (line.Length == 0)
            {
                return DefaultSelection.IsDefault
                    ? ImmutableArray<T>.Empty
                    : DefaultSelection.Select(i => Options[i]).ToImmutableArray();
            }

            if (line == "*")
            {
                return Options;
            }

            var parts = line.Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
            var indices = new List<int>();
            var ok = true;

            foreach (var p in parts)
            {
                if (!int.TryParse(p, out var idx) || idx < 1 || idx > Options.Length)
                {
                    io.WriteLine($"{Ansi.Red}  '{p}' is not between 1 and {Options.Length}.{Ansi.Reset}");
                    ok = false;
                    break;
                }
                indices.Add(idx - 1);
            }

            if (ok)
            {
                return indices
                    .Distinct()
                    .OrderBy(i => i)
                    .Select(i => Options[i])
                    .ToImmutableArray();
            }
        }
    }

    private ImmutableArray<T> AskInteractive(IConsoleIO io)
    {
        var selected = new HashSet<int>(DefaultSelection.IsDefault ? System.Array.Empty<int>() : DefaultSelection);
        var cursor = 0;
        io.WriteLine($"{Ansi.Bold}{Label}{Ansi.Reset} {Ansi.Dim}(↑/↓ to move, space to toggle, Enter to confirm){Ansi.Reset}");

        for (var i = 0; i < Options.Length; i++)
        {
            WriteRow(io, i, cursor, selected);
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
                case System.ConsoleKey.Spacebar:
                    if (!selected.Add(cursor))
                    {
                        selected.Remove(cursor);
                    }
                    break;
                case System.ConsoleKey.Enter:
                    return selected
                        .OrderBy(i => i)
                        .Select(i => Options[i])
                        .ToImmutableArray();
                default:
                    continue;
            }

            io.Write(Ansi.MoveUp(Options.Length));
            for (var i = 0; i < Options.Length; i++)
            {
                io.Write("\r" + Ansi.ClearLineFromCursor());
                WriteRow(io, i, cursor, selected);
            }
        }
    }

    private void WriteRow(IConsoleIO io, int i, int cursor, HashSet<int> selected)
    {
        var mark = selected.Contains(i) ? $"{Ansi.Green}[x]{Ansi.Reset}" : "[ ]";
        if (i == cursor)
        {
            io.WriteLine($"  {Ansi.Cyan}> {mark} {Display(Options[i])}{Ansi.Reset}");
        }
        else
        {
            io.WriteLine($"    {mark} {Display(Options[i])}");
        }
    }

    private string Display(T value) => Describe?.Invoke(value) ?? value.ToString() ?? string.Empty;
}
