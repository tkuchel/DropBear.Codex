#region

using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents the result of a validation operation,
///     including any <see cref="ValidationError" /> objects and an optional exception.
/// </summary>
public sealed class ValidationResult
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<ValidationResult>();

    private readonly List<ValidationError> _errors;

    private ValidationResult(
        IEnumerable<ValidationError> errors,
        string? message,
        Exception? exception,
        ResultState state)
    {
        _errors = errors.ToList();
        Message = message;
        Exception = exception;

        // If we have any errors, we override success to Failure
        State = state == ResultState.Success && _errors.Any()
            ? ResultState.Failure
            : state;
    }

    /// <summary>
    ///     Gets the result state (Success, Failure, etc.).
    /// </summary>
    public ResultState State { get; }

    /// <summary>
    ///     Indicates whether the validation succeeded (i.e., <see cref="State" /> == Success).
    /// </summary>
    public bool IsSuccess => State == ResultState.Success;

    /// <summary>
    ///     Indicates whether the validation failed (i.e., not <see cref="IsSuccess" />).
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    ///     Indicates whether the validation has any errors recorded.
    /// </summary>
    public bool HasErrors => _errors.Any();

    /// <summary>
    ///     Gets an optional message describing the validation result or errors.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    ///     Gets an optional exception that occurred during validation.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    ///     Gets the collection of validation errors associated with this result.
    /// </summary>
    public IReadOnlyList<ValidationError> Errors => _errors.AsReadOnly();

    #region Static Factory Methods

    /// <summary>
    ///     Creates a new validation result representing success (no errors).
    /// </summary>
    public static ValidationResult Success()
    {
        return new ValidationResult(
            Array.Empty<ValidationError>(),
            null,
            null,
            ResultState.Success);
    }

    /// <summary>
    ///     Creates a new validation result representing failure with multiple errors.
    /// </summary>
    /// <param name="errors">A collection of <see cref="ValidationError" /> objects.</param>
    /// <param name="message">An optional message describing the failure.</param>
    /// <param name="exception">An optional exception that caused or contributed to the failure.</param>
    public static ValidationResult Failure(
        IEnumerable<ValidationError> errors,
        string? message = null,
        Exception? exception = null)
    {
        return new ValidationResult(errors, message, exception, ResultState.Failure);
    }

    /// <summary>
    ///     Creates a new validation result representing failure with a single error.
    /// </summary>
    /// <param name="parameter">The parameter or field that caused the validation error.</param>
    /// <param name="errorMessage">A message describing the validation failure.</param>
    /// <param name="exception">An optional exception that caused or contributed to the failure.</param>
    public static ValidationResult Failure(
        string parameter,
        string errorMessage,
        Exception? exception = null)
    {
        return new ValidationResult(
            [new ValidationError(parameter, errorMessage)],
            errorMessage,
            exception,
            ResultState.Failure);
    }

    /// <summary>
    ///     Combines multiple <see cref="ValidationResult" /> objects into one.
    ///     If any contain errors, the combined result is failure.
    /// </summary>
    public static ValidationResult Combine(params ValidationResult[] results)
    {
        var combinedErrors = results.SelectMany(r => r.Errors).ToList();
        var message = combinedErrors.Any()
            ? string.Join("\n", combinedErrors.Select(e => $"{e.Parameter}: {e.ErrorMessage}"))
            : null;

        // If there are multiple exceptions, only the first is stored
        var exception = results.Select(r => r.Exception).FirstOrDefault(e => e != null);

        var finalState = combinedErrors.Any() ? ResultState.Failure : ResultState.Success;

        return new ValidationResult(combinedErrors, message, exception, finalState);
    }

    #endregion

    #region Instance Methods

    /// <summary>
    ///     Adds additional <paramref name="errors" /> to this validation result,
    ///     returning a new <see cref="ValidationResult" /> with merged state.
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
    ///     Adds a single error to this validation result, returning a new combined result.
    /// </summary>
    public ValidationResult AddError(string parameter, string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameter);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        return AddErrors([new ValidationError(parameter, errorMessage)]);
    }

    /// <summary>
    ///     Executes one of two functions based on whether the result is success or failure.
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
    ///     Executes one of two async functions based on whether the result is success or failure.
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
    ///     Ensures a specified condition is true, otherwise adds an error for the given <paramref name="parameter" />.
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
    ///     Returns all validation errors as a single string, joined by <paramref name="separator" />.
    /// </summary>
    public string GetErrorsAsString(string separator = "\n")
    {
        return string.Join(separator, _errors.Select(e => $"{e.Parameter}: {e.ErrorMessage}"));
    }

    /// <summary>
    ///     Retrieves all errors related to a specific <paramref name="parameter" />.
    /// </summary>
    public IEnumerable<ValidationError> GetErrorsFor(string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameter);
        return _errors.Where(e => e.Parameter.Equals(parameter, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Returns a human-readable string indicating success or listing all errors.
    /// </summary>
    public override string ToString()
    {
        return IsSuccess
            ? "Validation succeeded"
            : $"Validation failed: {GetErrorsAsString()}";
    }

    #endregion
}
