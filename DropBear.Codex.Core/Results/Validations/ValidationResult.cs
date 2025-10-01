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
///     Optimized for .NET 9 with simplified implementation.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
[JsonConverter(typeof(ValidationResultJsonConverter))]
public sealed class ValidationResult : Base.Result<ValidationError>
{
    // Singleton success instance for efficiency
    private static readonly ValidationResult SuccessInstance = new(ResultState.Success);

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
    public new static ValidationResult Success => SuccessInstance;

    private string DebuggerDisplay => $"IsValid = {IsValid}, Message = {ErrorMessage}";

    #region Factory Methods

    /// <summary>
    ///     Creates a failed validation result with the specified error message.
    /// </summary>
    public static ValidationResult Failed(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new ValidationResult(ResultState.Failure, new ValidationError(message));
    }

    /// <summary>
    ///     Creates a failed validation result for a specific property.
    /// </summary>
    public static ValidationResult PropertyFailed(
        string propertyName,
        string message,
        object? attemptedValue = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var error = ValidationError.ForProperty(propertyName, message, attemptedValue);
        return new ValidationResult(ResultState.Failure, error);
    }

    /// <summary>
    ///     Creates a failed validation result for a specific validation rule.
    /// </summary>
    public static ValidationResult RuleFailed(
        string rule,
        string message,
        object? attemptedValue = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rule);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var error = ValidationError.ForRule(rule, message, attemptedValue);
        return new ValidationResult(ResultState.Failure, error);
    }

    /// <summary>
    ///     Combines multiple validation results into a single result.
    /// </summary>
    public static ValidationResult Combine(IEnumerable<ValidationResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var resultsList = results.ToList();
        if (resultsList.Count == 0)
        {
            return Success;
        }

        var errors = resultsList
            .Where(r => !r.IsValid)
            .Select(r => r.Error)
            .Where(e => e != null)
            .ToList();

        if (errors.Count == 0)
        {
            return Success;
        }

        if (errors.Count == 1)
        {
            return new ValidationResult(ResultState.Failure, errors[0]);
        }

        var combinedMessage = string.Join(Environment.NewLine, errors.Select(e => e!.Message));
        return Failed(combinedMessage);
    }

    /// <summary>
    ///     Combines validation results with a custom separator.
    /// </summary>
    public static ValidationResult Combine(IEnumerable<ValidationResult> results, string separator)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(separator);

        var resultsList = results.ToList();
        if (resultsList.Count == 0)
        {
            return Success;
        }

        var errors = resultsList
            .Where(r => !r.IsValid)
            .Select(r => r.Error)
            .Where(e => e != null)
            .ToList();

        if (errors.Count == 0)
        {
            return Success;
        }

        if (errors.Count == 1)
        {
            return new ValidationResult(ResultState.Failure, errors[0]);
        }

        var combinedMessage = string.Join(separator, errors.Select(e => e!.Message));
        return Failed(combinedMessage);
    }

    #endregion

    #region Validation Helpers

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
    public static ValidationResult EnsureProperty(
        bool condition,
        string propertyName,
        string message,
        object? value = null)
    {
        return condition ? Success : PropertyFailed(propertyName, message, value);
    }

    /// <summary>
    ///     Ensures a condition is met for a specific rule.
    /// </summary>
    public static ValidationResult EnsureRule(
        bool condition,
        string rule,
        string message,
        object? value = null)
    {
        return condition ? Success : RuleFailed(rule, message, value);
    }

    /// <summary>
    ///     Validates using a predicate function.
    /// </summary>
    public static ValidationResult Validate(
        Func<bool> validator,
        string failureMessage)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureMessage);

        try
        {
            return validator() ? Success : Failed(failureMessage);
        }
        catch (Exception ex)
        {
            return new ValidationResult(ResultState.Failure, new ValidationError(ex.Message), ex);
        }
    }

    /// <summary>
    ///     Validates using an async predicate function.
    /// </summary>
    public static async ValueTask<ValidationResult> ValidateAsync(
        Func<CancellationToken, ValueTask<bool>> validator,
        string failureMessage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureMessage);

        try
        {
            var isValid = await validator(cancellationToken).ConfigureAwait(false);
            return isValid ? Success : Failed(failureMessage);
        }
        catch (Exception ex)
        {
            return new ValidationResult(ResultState.Failure, new ValidationError(ex.Message), ex);
        }
    }

    #endregion

    #region Conversion Methods

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

    /// <summary>
    ///     Converts to a Unit result.
    /// </summary>
    public Result<Unit, ValidationError> ToUnitResult()
    {
        return IsValid
            ? Result<Unit, ValidationError>.Success(Unit.Value)
            : Result<Unit, ValidationError>.Failure(Error!);
    }

    #endregion

    #region Operators

    /// <summary>
    ///     Implicitly converts a bool to a ValidationResult.
    /// </summary>
    public static implicit operator bool(ValidationResult result)
    {
        return result.IsValid;
    }

    /// <summary>
    ///     Combines two validation results using the AND operator.
    /// </summary>
    public static ValidationResult operator &(ValidationResult left, ValidationResult right)
    {
        return Combine([left, right]);
    }

    #endregion
}
