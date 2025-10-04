#region

using System.Collections.Frozen;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Compatibility;
using DropBear.Codex.Core.Results.Extensions;

#endregion

namespace DropBear.Codex.Core.Results.Validations;

/// <summary>
///     Represents the result of a validation operation with enhanced error aggregation.
///     Optimized for .NET 9 with modern C# features and collection expressions.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
[JsonConverter(typeof(ValidationResultJsonConverter))]
public sealed class ValidationResult : Base.Result<ValidationError>
{
    // Singleton success instance for efficiency
    private static readonly ValidationResult SuccessInstance = new(ResultState.Success);

    // Multiple errors storage (for aggregated validation) - using frozen set for performance
    private readonly FrozenSet<ValidationError>? _errors;

    private ValidationResult(
        ResultState state,
        ValidationError? error = null,
        Exception? exception = null,
        IEnumerable<ValidationError>? errors = null)
        : base(state, error, exception)
    {
        if (errors is not null)
        {
            var errorList = errors.ToList();
            if (errorList.Count > 0)
            {
                _errors = errorList.ToFrozenSet();
            }
        }
    }

    /// <summary>
    ///     Gets whether the validation was successful.
    /// </summary>
    [JsonIgnore]
    public bool IsValid => IsSuccess;

    /// <summary>
    ///     Gets the error message if validation failed.
    /// </summary>
    [JsonIgnore]
    public string ErrorMessage => Error?.Message ?? string.Empty;

    /// <summary>
    ///     Gets all validation errors (if multiple).
    ///     Uses collection expressions for modern syntax.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyCollection<ValidationError> Errors =>
        _errors ?? (Error is not null ? [Error] : []);

    /// <summary>
    ///     Gets the number of validation errors.
    /// </summary>
    [JsonIgnore]
    public int ErrorCount => _errors?.Count ?? (Error is not null ? 1 : 0);

    /// <summary>
    ///     Gets whether this result has multiple errors.
    /// </summary>
    [JsonIgnore]
    public bool HasMultipleErrors => ErrorCount > 1;

    /// <summary>
    ///     Gets a successful validation result (singleton).
    /// </summary>
    public new static ValidationResult Success => SuccessInstance;

    private string DebuggerDisplay =>
        $"IsValid = {IsValid}, ErrorCount = {ErrorCount}, Message = {ErrorMessage}";

    #region Factory Methods - Single Error

    /// <summary>
    ///     Creates a failed validation result with the specified error message.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValidationResult Failed(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new ValidationResult(ResultState.Failure, new ValidationError(message));
    }

    /// <summary>
    ///     Creates a failed validation result with the specified error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValidationResult Failed(ValidationError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ValidationResult(ResultState.Failure, error);
    }

    /// <summary>
    ///     Creates a failed validation result for a specific property.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    #endregion

    #region Factory Methods - Multiple Errors (Modern params collections)

    /// <summary>
    ///     Creates a failed validation result with multiple errors.
    ///     Uses params collection for modern syntax (.NET 9).
    /// </summary>
    public static ValidationResult Failed(params IEnumerable<ValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        var errorList = errors.ToList();

        if (errorList.Count == 0)
        {
            throw new ArgumentException("At least one error is required", nameof(errors));
        }

        if (errorList.Count == 1)
        {
            return new ValidationResult(ResultState.Failure, errorList[0]);
        }

        // Create aggregated error message
        var aggregatedError = CreateAggregatedError(errorList);
        return new ValidationResult(ResultState.Failure, aggregatedError, null, errorList);
    }

    #endregion

    #region Aggregation Methods

    /// <summary>
    ///     Combines multiple validation results into a single result.
    ///     All errors are aggregated if any validation failed.
    ///     Uses params collection for modern syntax.
    /// </summary>
    public static ValidationResult Combine(params IEnumerable<ValidationResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var resultList = results.ToList();

        if (resultList.Count == 0)
        {
            return Success;
        }

        if (resultList.Count == 1)
        {
            return resultList[0];
        }

        var allErrors = new List<ValidationError>();

        foreach (var result in resultList)
        {
            if (!result.IsValid)
            {
                if (result.HasMultipleErrors)
                {
                    allErrors.AddRange(result.Errors);
                }
                else if (result.Error is not null)
                {
                    allErrors.Add(result.Error);
                }
            }
        }

        return allErrors.Count == 0
            ? Success
            : Failed(allErrors);
    }

    /// <summary>
    ///     Combines multiple validation results asynchronously.
    ///     Uses modern async patterns with ValueTask.
    /// </summary>
    public static async ValueTask<ValidationResult> CombineAsync(
        IEnumerable<ValueTask<ValidationResult>> results,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(results);

        var resultArray = results.ToArray();
        if (resultArray.Length == 0)
        {
            return Success;
        }

        var completedResults = new ValidationResult[resultArray.Length];

        for (var i = 0; i < resultArray.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            completedResults[i] = await resultArray[i].ConfigureAwait(false);
        }

        return Combine(completedResults);
    }

    /// <summary>
    ///     Combines Task-based validation results.
    /// </summary>
    public static async ValueTask<ValidationResult> CombineAsync(
        IEnumerable<Task<ValidationResult>> results,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(results);

        var resultArray = await Task.WhenAll(results).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        return Combine(resultArray);
    }

    #endregion

    #region Error Filtering

    /// <summary>
    ///     Gets all errors for a specific property.
    ///     Uses Span-based comparison for better performance.
    /// </summary>
    public IEnumerable<ValidationError> GetErrorsForProperty(ReadOnlySpan<char> propertyName)
    {
        if (propertyName.IsEmpty)
        {
            throw new ArgumentException("Property name cannot be empty", nameof(propertyName));
        }

        // Convert to string for LINQ operations (unavoidable for now)
        var propertyNameStr = propertyName.ToString();
        return Errors.Where(e =>
            string.Equals(e.PropertyName, propertyNameStr, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Gets all errors for a specific validation rule.
    /// </summary>
    public IEnumerable<ValidationError> GetErrorsForRule(ReadOnlySpan<char> rule)
    {
        if (rule.IsEmpty)
        {
            throw new ArgumentException("Rule name cannot be empty", nameof(rule));
        }

        var ruleStr = rule.ToString();
        return Errors.Where(e =>
            string.Equals(e.ValidationRule, ruleStr, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Checks if there are errors for a specific property.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasErrorsForProperty(ReadOnlySpan<char> propertyName)
    {
        if (propertyName.IsEmpty)
        {
            throw new ArgumentException("Property name cannot be empty", nameof(propertyName));
        }

        var propertyNameStr = propertyName.ToString();
        return Errors.Any(e =>
            string.Equals(e.PropertyName, propertyNameStr, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Gets errors grouped by property name.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<ValidationError>> GetErrorsByProperty()
    {
        return Errors
            .Where(e => e.PropertyName is not null)
            .GroupBy(e => e.PropertyName!)
            .ToFrozenDictionary(
                g => g.Key,
                g => (IReadOnlyList<ValidationError>)g.ToList(),
                StringComparer.Ordinal);
    }

    /// <summary>
    ///     Gets errors grouped by validation rule.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<ValidationError>> GetErrorsByRule()
    {
        return Errors
            .Where(e => e.ValidationRule is not null)
            .GroupBy(e => e.ValidationRule!)
            .ToFrozenDictionary(
                g => g.Key,
                g => (IReadOnlyList<ValidationError>)g.ToList(),
                StringComparer.Ordinal);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Creates an aggregated error from multiple validation errors.
    ///     Uses modern string interpolation handler for better performance.
    /// </summary>
    private static ValidationError CreateAggregatedError(List<ValidationError> errors)
    {
        var errorCount = errors.Count;
        var message = $"Validation failed with {errorCount} error(s)";

        var aggregatedError = new ValidationError(message)
        {
            ValidationRule = "Aggregate", Severity = errors.Max(e => e.Severity)
        };

        // Add metadata about the aggregated errors
        return (ValidationError)aggregatedError
            .WithMetadata("ErrorCount", errorCount)
            .WithMetadata("ErrorMessages", errors.Select(e => e.Message).ToArray());
    }

    #endregion

    #region Conversion Methods

    /// <summary>
    ///     Throws a ValidationException if this result is not valid.
    /// </summary>
    public void ThrowIfInvalid()
    {
        if (!IsValid)
        {
            throw new ValidationException(this);
        }
    }

    /// <summary>
    ///     Converts this ValidationResult to a Result{T, ValidationError} with a value.
    /// </summary>
    public Result<T, ValidationError> WithValue<T>(T value)
    {
        return IsValid
            ? Result<T, ValidationError>.Success(value)
            : Result<T, ValidationError>.Failure(Error!);
    }

    #endregion
}
