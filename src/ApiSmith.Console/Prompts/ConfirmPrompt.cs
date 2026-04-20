namespace ApiSmith.Console.Prompts;

public sealed class ConfirmPrompt : IPrompt<bool>
{
    public required string Label { get; init; }

    public bool Default { get; init; } = true;

    public bool Ask(IConsoleIO io)
    {
        var hint = Default ? "[Y/n]" : "[y/N]";

        while (true)
        {
            io.Write($"{Ansi.Bold}{Label}{Ansi.Reset} {hint}: ");
            var line = (io.ReadLine() ?? string.Empty).Trim().ToLowerInvariant();

            if (line.Length == 0)
            {
                return Default;
            }

            if (line is "y" or "yes")
            {
                return true;
            }

            if (line is "n" or "no")
            {
                return false;
            }

            io.WriteLine($"{Ansi.Red}  Please answer y or n.{Ansi.Reset}");
        }
    }
}
