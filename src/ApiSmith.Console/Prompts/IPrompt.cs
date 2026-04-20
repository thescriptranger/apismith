namespace ApiSmith.Console.Prompts;

public interface IPrompt<T>
{
    T Ask(IConsoleIO io);
}
