namespace ApiSmith.Console.Prompts;

public sealed class TextPrompt : IPrompt<string>
{
    public required string Label { get; init; }

    public string? Default { get; init; }

    /// <summary>Returns null on success, otherwise the error to show.</summary>
    public System.Func<string, string?>? Validate { get; init; }

    public string Ask(IConsoleIO io)
    {
        while (true)
        {
            var suffix = Default is null ? ": " : $" [{Default}]: ";
            io.Write($"{Ansi.Bold}{Label}{Ansi.Reset}{suffix}");

            var line = io.ReadLine();
            var value = string.IsNullOrWhiteSpace(line) ? (Default ?? string.Empty) : line;

            if (Validate is { } v)
            {
                var err = v(value);
                if (err is not null)
                {
                    io.WriteLine($"{Ansi.Red}  {err}{Ansi.Reset}");
                    continue;
                }
            }

            return value;
        }
    }
}
