#region

using System.ComponentModel.DataAnnotations;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;
using DropBear.Codex.Tasks.TaskExecutionEngine.Models;
using Newtonsoft.Json;
using Serilog;
using ExecutionContext = DropBear.Codex.Tasks.TaskExecutionEngine.Models.ExecutionContext;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine;

/// <summary>
///     Responsible for loading task configurations from a file and creating task instances.
/// </summary>
public class ConfigurationLoader
{
    private readonly ILogger _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ConfigurationLoader" /> class.
    /// </summary>
    public ConfigurationLoader()
    {
        _logger = LoggerFactory.Logger.ForContext<ConfigurationLoader>();
    }

    /// <summary>
    ///     Loads tasks from the specified configuration file.
    /// </summary>
    /// <param name="filePath">The path to the configuration file.</param>
    /// <returns>An enumerable of tasks loaded from the configuration.</returns>
    /// <exception cref="ArgumentException">Thrown when the file path is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is an error loading tasks.</exception>
    public IEnumerable<ITask> LoadTasksFromConfiguration(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            _logger.Error("Configuration file path is null or empty.");
            throw new ArgumentException("Configuration file path cannot be null or empty.", nameof(filePath));
        }

        _logger.Information("Loading task configurations from file: {FilePath}", filePath);

        List<TaskConfiguration>? configurations;
        try
        {
            var fileContent = File.ReadAllText(filePath);
            configurations = JsonConvert.DeserializeObject<List<TaskConfiguration>>(fileContent);

            if (configurations == null || configurations.Count == 0)
            {
                _logger.Warning("No task configurations found in file: {FilePath}", filePath);
                yield break;
            }
        }
        catch (FileNotFoundException ex)
        {
            _logger.Error(ex, "Configuration file not found: {FilePath}", filePath);
            throw;
        }
        catch (IOException ex)
        {
            _logger.Error(ex, "IO error while reading configuration file: {FilePath}", filePath);
            throw new InvalidOperationException($"Error reading configuration file: {filePath}", ex);
        }
        catch (JsonException ex)
        {
            _logger.Error(ex, "Error parsing JSON in configuration file: {FilePath}", filePath);
            throw new InvalidOperationException($"Invalid JSON in configuration file: {filePath}", ex);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Unexpected error while loading task configurations.");
            throw new InvalidOperationException("An unexpected error occurred while loading task configurations.", ex);
        }

        foreach (var config in configurations)
        {
            try
            {
                config.Validate();
            }
            catch (ValidationException ex)
            {
                _logger.Error(ex, "Invalid task configuration for task '{TaskName}'. Skipping task.", config.Name);
                continue; // Skip invalid configurations
            }

            var taskType = Type.GetType(config.Type);
            if (taskType == null)
            {
                _logger.Error("Task type '{TaskType}' not found for task '{TaskName}'. Skipping task.", config.Type,
                    config.Name);
                continue; // Skip tasks with invalid type
            }

            ITask task;
            try
            {
                // Assuming the task has a constructor that accepts (string name)
                task = Activator.CreateInstance(taskType, config.Name) as ITask;
            }
            catch (Exception ex)
            {
                _logger.Error(ex,
                    "Error creating instance of task type '{TaskType}' for task '{TaskName}'. Skipping task.",
                    config.Type, config.Name);
                continue; // Skip tasks that cannot be instantiated
            }

            try
            {
                task.MaxRetryCount = config.MaxRetryCount;
                task.RetryDelay = config.RetryDelay;
                task.ContinueOnFailure = config.ContinueOnFailure;
                task.SetDependencies(config.Dependencies);
                task.Condition = ParseConditionExpression(config.ConditionExpression);
                // Set other properties as needed
                // Optionally set parameters from config.Parameters
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error setting properties for task '{TaskName}'. Skipping task.", config.Name);
                continue; // Skip tasks with errors in setting properties
            }

            _logger.Information("Successfully loaded task '{TaskName}'.", task.Name);
            yield return task;
        }
    }

    /// <summary>
    ///     Parses the condition expression and returns a function that evaluates the condition.
    /// </summary>
    /// <param name="conditionExpression">The condition expression to parse.</param>
    /// <returns>A function that evaluates the condition, or null if the expression is empty.</returns>
    private Func<ExecutionContext, bool>? ParseConditionExpression(string? conditionExpression)
    {
        if (string.IsNullOrWhiteSpace(conditionExpression))
        {
            return null;
        }

        // Implement parsing of the condition expression.
        // This could involve compiling the expression or using a scripting engine.
        // For this example, we'll log a warning and return null.
        _logger.Warning(
            "Condition expressions are not implemented. Returning null for condition expression: {ConditionExpression}",
            conditionExpression);
        return null;
    }
}
