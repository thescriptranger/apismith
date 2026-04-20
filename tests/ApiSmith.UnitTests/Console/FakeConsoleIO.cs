using System.Text;
using ApiSmith.Console;

namespace ApiSmith.UnitTests.Console;

/// <summary>Scripted-input test double; <see cref="ReadKey"/> is unsupported (line-based fallback only).</summary>
internal sealed class FakeConsoleIO : IConsoleIO
{
    private readonly Queue<string> _lines;
    private readonly StringBuilder _output = new();

    public FakeConsoleIO(params string[] lines)
    {
        _lines = new Queue<string>(lines);
    }

    public bool IsInputRedirected => true;

    public bool IsOutputRedirected => true;

    public string Output => _output.ToString();

    public void Write(string text) => _output.Append(text);

    public void WriteLine(string text) => _output.AppendLine(text);

    public string? ReadLine() => _lines.Count == 0 ? null : _lines.Dequeue();

    public System.ConsoleKeyInfo ReadKey() =>
        throw new System.InvalidOperationException("FakeConsoleIO does not support keystroke reads; tests should exercise the line-based fallback.");
}
