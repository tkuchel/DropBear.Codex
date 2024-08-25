namespace DropBear.Codex.Core.Interfaces;

public interface ILogger
{
    void LogError(Exception ex, string message);
    void LogDebug(string message);
    void LogInformation(string message);
    void LogWarning(string message);
    void LogCritical(string message);
}

