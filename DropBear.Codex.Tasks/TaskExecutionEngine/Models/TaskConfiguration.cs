#region

using System.ComponentModel.DataAnnotations;
using ExecutionContext = DropBear.Codex.Tasks.TaskExecutionEngine.Models.ExecutionContext;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     Represents the configuration settings for a task within the execution engine.
/// </summary>
public class TaskConfiguration
{
    private string? _conditionExpression = string.Empty;
    private ICollection<string> _dependencies = new List<string>();
    private int _maxRetryCount = 3;
    private string _name = string.Empty;
    private IDictionary<string, object> _parameters = new Dictionary<string, object>(StringComparer.Ordinal);
    private TimeSpan _retryDelay = TimeSpan.FromSeconds(1);
    private string _type = string.Empty;

    /// <summary>
    ///     Gets or sets the unique name of the task.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is null or whitespace.</exception>
    public string Name
    {
        get => _name;
        set => _name = !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException("Name cannot be null or whitespace.", nameof(Name));
    }

    /// <summary>
    ///     Gets or sets the fully qualified type name of the task implementation.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is null or whitespace.</exception>
    public string Type
    {
        get => _type;
        set => _type = !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException("Type cannot be null or whitespace.", nameof(Type));
    }

    /// <summary>
    ///     Gets or sets the maximum number of retry attempts in case of task failure.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is negative.</exception>
    public int MaxRetryCount
    {
        get => _maxRetryCount;
        set => _maxRetryCount = value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(MaxRetryCount), "MaxRetryCount cannot be negative.");
    }

    /// <summary>
    ///     Gets or sets the delay between retry attempts.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is negative.</exception>
    public TimeSpan RetryDelay
    {
        get => _retryDelay;
        set => _retryDelay = value >= TimeSpan.Zero
            ? value
            : throw new ArgumentOutOfRangeException(nameof(RetryDelay), "RetryDelay cannot be negative.");
    }

    /// <summary>
    ///     Gets or sets a value indicating whether to continue executing subsequent tasks even if this task fails.
    /// </summary>
    public bool ContinueOnFailure { get; set; }

    /// <summary>
    ///     Gets or sets the collection of task names that this task depends on.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value is null.</exception>
    public ICollection<string> Dependencies
    {
        get => _dependencies;
        set => _dependencies =
            value ?? throw new ArgumentNullException(nameof(Dependencies), "Dependencies cannot be null.");
    }

    /// <summary>
    ///     Gets or sets the condition expression that determines whether the task should execute.
    /// </summary>
    public string? ConditionExpression
    {
        get => _conditionExpression;
        set => _conditionExpression = value ?? string.Empty;
    }

    /// <summary>
    ///     Gets or sets the parameters to be passed to the task.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value is null.</exception>
    public IDictionary<string, object> Parameters
    {
        get => _parameters;
        set => _parameters = value ?? throw new ArgumentNullException(nameof(Parameters), "Parameters cannot be null.");
    }

    /// <summary>
    ///     Validates the task configuration to ensure all required properties are set and valid.
    /// </summary>
    /// <exception cref="ValidationException">Thrown when the configuration is invalid.</exception>
    public void Validate()
    {
        var validationErrors = new List<ValidationResult>();
        var validationContext = new ValidationContext(this);

        if (!Validator.TryValidateObject(this, validationContext, validationErrors, true))
        {
            var errorMessages = string.Join("; ", validationErrors.Select(e => e.ErrorMessage));
            throw new ValidationException($"TaskConfiguration is invalid: {errorMessages}");
        }

        // Additional custom validation logic
        if (Dependencies.Any(string.IsNullOrWhiteSpace))
        {
            throw new ValidationException("Dependencies cannot contain null or whitespace strings.");
        }
    }
}
