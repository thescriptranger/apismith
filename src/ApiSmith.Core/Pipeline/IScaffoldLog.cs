namespace ApiSmith.Core.Pipeline;

public interface IScaffoldLog
{
    void Info(string message);
    void Warn(string message);
    void Error(string message);
}
