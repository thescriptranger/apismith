namespace ApiSmith.Templating;

public sealed class TemplateException : System.Exception
{
    public TemplateException(string message) : base(message)
    {
    }

    public TemplateException(string message, System.Exception inner) : base(message, inner)
    {
    }

    public static TemplateException At(string templateName, int line, int column, string message) =>
        new($"{templateName}({line},{column}): {message}");
}
