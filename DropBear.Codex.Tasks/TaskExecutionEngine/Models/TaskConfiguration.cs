#region

using System.ComponentModel.DataAnnotations;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

public sealed class TaskConfiguration
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public int MaxRetryCount { get; init; } = 3;
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);
    public bool ContinueOnFailure { get; set; }
    public HashSet<string> Dependencies { get; init; } = new(StringComparer.Ordinal);
    public string? ConditionExpression { get; set; } = string.Empty;

    public IDictionary<string, object> Parameters { get; set; } =
        new Dictionary<string, object>(StringComparer.Ordinal);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ValidationException("Name cannot be null or whitespace.");
        }

        if (Dependencies.Any(string.IsNullOrWhiteSpace))
        {
            throw new ValidationException("Dependencies cannot contain null or whitespace strings.");
        }
    }
}
