namespace ApiSmith.Console;

/// <summary>ANSI escape sequences used by interactive prompts.</summary>
public static class Ansi
{
    public const string Esc     = "\u001b";
    public const string Reset   = Esc + "[0m";
    public const string Bold    = Esc + "[1m";
    public const string Dim     = Esc + "[2m";
    public const string Cyan    = Esc + "[36m";
    public const string Green   = Esc + "[32m";
    public const string Yellow  = Esc + "[33m";
    public const string Red     = Esc + "[31m";
    public const string HideCursor = Esc + "[?25l";
    public const string ShowCursor = Esc + "[?25h";

    public static string MoveUp(int n) => $"{Esc}[{n}A";

    public static string ClearLineFromCursor() => Esc + "[K";
}
