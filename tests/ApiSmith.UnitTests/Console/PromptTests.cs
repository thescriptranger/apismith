using System.Collections.Immutable;
using ApiSmith.Console.Prompts;

namespace ApiSmith.UnitTests.Console;

public sealed class PromptTests
{
    [Fact]
    public void TextPrompt_returns_default_when_blank()
    {
        var io = new FakeConsoleIO(string.Empty);
        var v = new TextPrompt { Label = "Name", Default = "MyApi" }.Ask(io);
        Assert.Equal("MyApi", v);
    }

    [Fact]
    public void TextPrompt_takes_user_value()
    {
        var io = new FakeConsoleIO("Bookings");
        var v = new TextPrompt { Label = "Name", Default = "MyApi" }.Ask(io);
        Assert.Equal("Bookings", v);
    }

    [Fact]
    public void TextPrompt_re_asks_on_validation_error()
    {
        var io = new FakeConsoleIO(string.Empty, "ok");
        var v = new TextPrompt
        {
            Label = "Name",
            Validate = s => string.IsNullOrWhiteSpace(s) ? "required" : null,
        }.Ask(io);
        Assert.Equal("ok", v);
    }

    [Theory]
    [InlineData("y", true)]
    [InlineData("yes", true)]
    [InlineData("n", false)]
    [InlineData("no", false)]
    [InlineData("", true)]          // empty → default
    public void ConfirmPrompt_parses_common_inputs(string input, bool expected)
    {
        var io = new FakeConsoleIO(input);
        Assert.Equal(expected, new ConfirmPrompt { Label = "go?", Default = true }.Ask(io));
    }

    [Fact]
    public void SelectPrompt_numbered_fallback_picks_index()
    {
        var io = new FakeConsoleIO("2");
        var pick = new SelectPrompt<string>
        {
            Label = "choose",
            Options = ImmutableArray.Create("a", "b", "c"),
        }.Ask(io);
        Assert.Equal("b", pick);
    }

    [Fact]
    public void SelectPrompt_blank_uses_default()
    {
        var io = new FakeConsoleIO(string.Empty);
        var pick = new SelectPrompt<string>
        {
            Label = "choose",
            Options = ImmutableArray.Create("a", "b", "c"),
            DefaultIndex = 2,
        }.Ask(io);
        Assert.Equal("c", pick);
    }

    [Fact]
    public void SelectPrompt_re_asks_on_invalid()
    {
        var io = new FakeConsoleIO("99", "1");
        var pick = new SelectPrompt<string>
        {
            Label = "choose",
            Options = ImmutableArray.Create("a", "b"),
        }.Ask(io);
        Assert.Equal("a", pick);
    }

    [Fact]
    public void MultiSelect_star_picks_all()
    {
        var io = new FakeConsoleIO("*");
        var pick = new MultiSelectPrompt<string>
        {
            Label = "x",
            Options = ImmutableArray.Create("a", "b", "c"),
        }.Ask(io);
        Assert.Equal(new[] { "a", "b", "c" }, pick);
    }

    [Fact]
    public void MultiSelect_csv_indices()
    {
        var io = new FakeConsoleIO("1, 3");
        var pick = new MultiSelectPrompt<string>
        {
            Label = "x",
            Options = ImmutableArray.Create("a", "b", "c"),
        }.Ask(io);
        Assert.Equal(new[] { "a", "c" }, pick);
    }

    [Fact]
    public void MultiSelect_blank_uses_defaults()
    {
        var io = new FakeConsoleIO(string.Empty);
        var pick = new MultiSelectPrompt<string>
        {
            Label = "x",
            Options = ImmutableArray.Create("a", "b", "c"),
            DefaultSelection = ImmutableArray.Create(0, 2),
        }.Ask(io);
        Assert.Equal(new[] { "a", "c" }, pick);
    }
}
