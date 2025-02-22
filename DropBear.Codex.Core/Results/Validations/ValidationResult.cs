#region

using System.Diagnostics;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Compatibility;

#endregion

namespace DropBear.Codex.Core.Results.Validations;

/// <summary>
///     Represents the result of a validation operation.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
[JsonConverter(typeof(ValidationResultJsonConverter))]
public sealed class ValidationResult : Base.Result<ValidationError>
{
    private static readonly ValidationError DefaultError = new("Validation failed");

    private ValidationResult(ResultState state, ValidationError? error = null, Exception? exception = null)
        : base(state, error, exception)
    {
    }

    /// <summary>
    ///     Gets a value indicating whether the validation was successful.
    /// </summary>
    [JsonIgnore]
    public bool IsValid => IsSuccess;

    /// <summary>
    ///     Gets the error message if validation failed.
    /// </summary>
    [JsonIgnore]
    public string ErrorMessage => Error?.Message ?? string.Empty;

    /// <summary>
    ///     Gets a successful validation result.
    /// </summary>
    public new static ValidationResult Success => new(ResultState.Success);

    private string DebuggerDisplay => $"IsValid = {IsValid}, Message = {ErrorMessage}";

    /// <summary>
    ///     Creates a failed validation result with the specified error message.
    /// </summary>
    public static ValidationResult Failed(string message)
    {
        return new ValidationResult(ResultState.Failure, new ValidationError(message));
    }

    /// <summary>
    ///     Creates a failed validation result for a specific property.
    /// </summary>
    public static ValidationResult PropertyFailed(string propertyName, string message, object? attemptedValue = null)
    {
        var error = new ValidationError(message) { PropertyName = propertyName, AttemptedValue = attemptedValue };
        return new ValidationResult(ResultState.Failure, error);
    }

    /// <summary>
    ///     Creates a failed validation result for a specific validation rule.
    /// </summary>
    public static ValidationResult RuleFailed(string rule, string message, object? attemptedValue = null)
    {
        var error = new ValidationError(message) { ValidationRule = rule, AttemptedValue = attemptedValue };
        return new ValidationResult(ResultState.Failure, error);
    }

    /// <summary>
    ///     Combines multiple validation results into a single result.
    /// </summary>
    public static ValidationResult Combine(IEnumerable<ValidationResult> results)
    {
        var errors = results
            .Where(r => !r.IsValid)
            .Select(r => r.Error)
            .Where(e => e != null)
            .ToList();

        if (errors.Count == 0)
        {
            return Success;
        }

        var combinedMessage = string.Join(
            Environment.NewLine,
            errors.Select(e => e!.Message));

        return Failed(combinedMessage);
    }

    /// <summary>
    ///     Ensures a condition is met, returning a failed result if not.
    /// </summary>
    public static ValidationResult Ensure(bool condition, string message)
    {
        return condition ? Success : Failed(message);
    }

    /// <summary>
    ///     Ensures a condition is met for a specific property.
    /// </summary>
    public static ValidationResult EnsureProperty(bool condition, string propertyName, string message,
        object? value = null)
    {
        return condition ? Success : PropertyFailed(propertyName, message, value);
    }

    /// <summary>
    ///     Validates using a predicate function.
    /// </summary>
    public static async ValueTask<ValidationResult> ValidateAsync(
        Func<CancellationToken, ValueTask<bool>> validator,
        string failureMessage,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await validator(cancellationToken).ConfigureAwait(false)
                ? Success
                : Failed(failureMessage);
        }
        catch (Exception ex)
        {
            return new ValidationResult(ResultState.Failure, DefaultError, ex);
        }
    }

    /// <summary>
    ///     Returns the validation result as a Result{T, ValidationError}.
    /// </summary>
    public Result<T, ValidationError> ToResult<T>(T value)
    {
        return IsValid
            ? Result<T, ValidationError>.Success(value)
            : Result<T, ValidationError>.Failure(Error!);
    }

    /// <summary>
    ///     Returns the validation result as a Result{T, ValidationError} with a default value.
    /// </summary>
    public Result<T, ValidationError> ToResult<T>()
    {
        return IsValid
            ? Result<T, ValidationError>.Success(default!)
            : Result<T, ValidationError>.Failure(Error!);
    }
}
