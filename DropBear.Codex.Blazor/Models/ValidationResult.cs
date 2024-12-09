#region

using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents the result of a validation operation.
/// </summary>
public sealed class ValidationResult
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<ValidationResult>();
    private readonly List<ValidationError> _errors;

    private ValidationResult(IEnumerable<ValidationError> errors, string? message, Exception? exception,
        ResultState state)
    {
        _errors = errors.ToList();
        Message = message;
        Exception = exception;
        State = state == ResultState.Success && _errors.Any() ? ResultState.Failure : state;
    }

    public ResultState State { get; }

    /// <summary>
    ///     Gets whether the validation was successful.
    /// </summary>
    public bool IsSuccess => State == ResultState.Success;

    /// <summary>
    ///     Gets whether the validation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    ///     Gets whether the validation has any errors.
    /// </summary>
    public bool HasErrors => _errors.Any();

    /// <summary>
    ///     Gets the validation error message if any.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    ///     Gets the exception that occurred during validation if any.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    ///     Gets the collection of validation errors.
    /// </summary>
    public IReadOnlyList<ValidationError> Errors => _errors.AsReadOnly();

    /// <summary>
    ///     Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success()
    {
        return new ValidationResult(
            [], // No errors
            null, // No message
            null, // No exception
            ResultState.Success // Explicit success state
        );
    }


    /// <summary>
    ///     Creates a failed validation result with multiple errors.
    /// </summary>
    public static ValidationResult Failure(
        IEnumerable<ValidationError> errors,
        string? message = null,
        Exception? exception = null)
    {
        return new ValidationResult(errors, message, exception, ResultState.Failure);
    }

    /// <summary>
    ///     Creates a failed validation result with a single error.
    /// </summary>
    public static ValidationResult Failure(
        string parameter,
        string errorMessage,
        Exception? exception = null)
    {
        return new ValidationResult(
            new[] { new ValidationError(parameter, errorMessage) },
            errorMessage,
            exception,
            ResultState.Failure);
    }

    /// <summary>
    ///     Creates a new ValidationResult by combining multiple results.
    /// </summary>
    public static ValidationResult Combine(params ValidationResult[] results)
    {
        var errors = results.SelectMany(r => r.Errors).ToList();
        var message = errors.Any() ? string.Join("\n", errors.Select(e => $"{e.Parameter}: {e.ErrorMessage}")) : null;
        var exception = results.Select(r => r.Exception).FirstOrDefault();
        return new ValidationResult(errors, message, exception,
            errors.Any() ? ResultState.Failure : ResultState.Success);
    }

    /// <summary>
    ///     Adds additional errors to the current validation result.
    /// </summary>
    public ValidationResult AddErrors(IEnumerable<ValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        var newErrors = new List<ValidationError>(_errors);
        newErrors.AddRange(errors);

        return new ValidationResult(
            newErrors,
            Message,
            Exception,
            ResultState.Failure);
    }

    /// <summary>
    ///     Adds a single error to the current validation result.
    /// </summary>
    public ValidationResult AddError(string parameter, string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameter);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        return AddErrors(new[] { new ValidationError(parameter, errorMessage) });
    }

    /// <summary>
    ///     Executes different actions based on the validation state.
    /// </summary>
    public TResult Match<TResult>(
        Func<TResult> onSuccess,
        Func<IReadOnlyList<ValidationError>, string?, Exception?, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            return IsSuccess
                ? onSuccess()
                : onFailure(Errors, Message, Exception);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during validation result match operation");
            throw;
        }
    }

    /// <summary>
    ///     Executes different async actions based on the validation state.
    /// </summary>
    public async Task<TResult> MatchAsync<TResult>(
        Func<Task<TResult>> onSuccess,
        Func<IReadOnlyList<ValidationError>, string?, Exception?, Task<TResult>> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            return IsSuccess
                ? await onSuccess().ConfigureAwait(false)
                : await onFailure(Errors, Message, Exception).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during validation result async match operation");
            throw;
        }
    }

    /// <summary>
    ///     Ensures a condition is met or adds an error.
    /// </summary>
    public ValidationResult Ensure(Func<bool> predicate, string parameter, string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentException.ThrowIfNullOrWhiteSpace(parameter);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        try
        {
            return predicate()
                ? this
                : AddError(parameter, errorMessage);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during validation ensure operation");
            return AddError(parameter, $"Validation check failed: {ex.Message}");
        }
    }

    /// <summary>
    ///     Gets a formatted string containing all validation errors.
    /// </summary>
    public string GetErrorsAsString(string separator = "\n")
    {
        return string.Join(separator,
            _errors.Select(e => $"{e.Parameter}: {e.ErrorMessage}"));
    }

    /// <summary>
    ///     Gets all errors for a specific parameter.
    /// </summary>
    public IEnumerable<ValidationError> GetErrorsFor(string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameter);
        return _errors.Where(e => e.Parameter.Equals(parameter, StringComparison.OrdinalIgnoreCase));
    }

    public override string ToString()
    {
        return IsSuccess
            ? "Validation succeeded"
            : $"Validation failed: {GetErrorsAsString()}";
    }
}
