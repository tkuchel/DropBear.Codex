#region

using System.Text.RegularExpressions;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Errors;

#endregion

namespace DropBear.Codex.Core.Results.Extensions;

/// <summary>
///     Provides validation extension methods for Result types
/// </summary>
public static class ResultValidationExtensions
{
    #region Validation Pipeline

    /// <summary>
    ///     Validates a value using multiple validators
    /// </summary>
    public static Result<T, ValidationError> Validate<T>(
        this T value,
        params Func<T, Result<T, ValidationError>>[] validators)
    {
        ArgumentNullException.ThrowIfNull(validators);
        return ValidateAsync(value, validators.Select(v =>
            new Func<T, ValueTask<Result<T, ValidationError>>>(x =>
                new ValueTask<Result<T, ValidationError>>(v(x))))).Result;
    }

    /// <summary>
    ///     Validates a value asynchronously using multiple validators
    /// </summary>
    public static async ValueTask<Result<T, ValidationError>> ValidateAsync<T>(
        this T value,
        IEnumerable<Func<T, ValueTask<Result<T, ValidationError>>>> validators,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(validators);

        var errors = new List<ValidationError>();
        var current = value;

        foreach (var validator in validators)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await validator(current).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                if (result.Error is not null)
                {
                    errors.Add(result.Error);
                }
            }
            else
            {
                current = result.Value;
            }
        }

        return errors.Count == 0
            ? Result<T, ValidationError>.Success(current)
            : Result<T, ValidationError>.Failure(errors.Combine());
    }

    #endregion

    #region Predicate Validation

    /// <summary>
    ///     Ensures a Result value meets a specified condition
    /// </summary>
    public static Result<T, ValidationError> Ensure<T>(
        this Result<T, ValidationError> result,
        Func<T, bool> predicate,
        string field,
        string message)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (!result.IsSuccess)
        {
            return result;
        }

        try
        {
            return predicate(result.Value)
                ? result
                : Result<T, ValidationError>.Failure(new ValidationError(field, message));
        }
        catch (Exception ex)
        {
            return Result<T, ValidationError>.Failure(
                new ValidationError(field, $"Validation failed: {ex.Message}"));
        }
    }

    /// <summary>
    ///     Ensures a Result value meets a specified condition asynchronously
    /// </summary>
    public static async ValueTask<Result<T, ValidationError>> EnsureAsync<T>(
        this Result<T, ValidationError> result,
        Func<T, ValueTask<bool>> predicate,
        string field,
        string message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (!result.IsSuccess)
        {
            return result;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await predicate(result.Value).ConfigureAwait(false)
                ? result
                : Result<T, ValidationError>.Failure(new ValidationError(field, message));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<T, ValidationError>.Failure(
                new ValidationError(field, $"Validation failed: {ex.Message}"));
        }
    }

    #endregion

    #region Null Validation

    /// <summary>
    ///     Validates that a reference type value is not null
    /// </summary>
    public static Result<T, ValidationError> NotNull<T>(
        this T? value,
        string field)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        return value is not null
            ? Result<T, ValidationError>.Success(value)
            : Result<T, ValidationError>.Failure(new ValidationError(field, "Value cannot be null"));
    }

    /// <summary>
    ///     Validates that a nullable value type is not null
    /// </summary>
    public static Result<T, ValidationError> NotNull<T>(
        this T? value,
        string field)
        where T : struct
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        return value.HasValue
            ? Result<T, ValidationError>.Success(value.Value)
            : Result<T, ValidationError>.Failure(new ValidationError(field, "Value cannot be null"));
    }

    #endregion

    #region String Validation

    /// <summary>
    ///     Validates that a string is not null or empty
    /// </summary>
    public static Result<string, ValidationError> NotNullOrEmpty(
        this string? value,
        string field)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        return !string.IsNullOrEmpty(value)
            ? Result<string, ValidationError>.Success(value)
            : Result<string, ValidationError>.Failure(new ValidationError(field, "Value cannot be empty"));
    }

    /// <summary>
    ///     Validates that a string is not null or whitespace
    /// </summary>
    public static Result<string, ValidationError> NotNullOrWhiteSpace(
        this string? value,
        string field)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        return !string.IsNullOrWhiteSpace(value)
            ? Result<string, ValidationError>.Success(value)
            : Result<string, ValidationError>.Failure(new ValidationError(field, "Value cannot be whitespace"));
    }

    /// <summary>
    ///     Validates that a string matches a regex pattern
    /// </summary>
    public static Result<string, ValidationError> MatchesPattern(
        this string? value,
        string field,
        string pattern,
        string? message = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        if (string.IsNullOrEmpty(value))
        {
            return Result<string, ValidationError>.Failure(
                new ValidationError(field, "Value cannot be empty"));
        }

        return Regex.IsMatch(value, pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1))
            ? Result<string, ValidationError>.Success(value)
            : Result<string, ValidationError>.Failure(
                new ValidationError(field, message ?? $"Value must match pattern: {pattern}"));
    }

    #endregion

    #region Range Validation

    /// <summary>
    ///     Validates that a value is within a specified range
    /// </summary>
    public static Result<T, ValidationError> InRange<T>(
        this T value,
        string field,
        T min,
        T max)
        where T : IComparable<T>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentNullException.ThrowIfNull(min);
        ArgumentNullException.ThrowIfNull(max);

        if (min.CompareTo(max) > 0)
        {
            throw new ArgumentException("Minimum value must be less than or equal to maximum value");
        }

        return value.CompareTo(min) >= 0 && value.CompareTo(max) <= 0
            ? Result<T, ValidationError>.Success(value)
            : Result<T, ValidationError>.Failure(
                new ValidationError(field, $"Value must be between {min} and {max}"));
    }

    /// <summary>
    ///     Validates that a value is greater than a specified minimum
    /// </summary>
    public static Result<T, ValidationError> GreaterThan<T>(
        this T value,
        string field,
        T min)
        where T : IComparable<T>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentNullException.ThrowIfNull(min);

        return value.CompareTo(min) > 0
            ? Result<T, ValidationError>.Success(value)
            : Result<T, ValidationError>.Failure(
                new ValidationError(field, $"Value must be greater than {min}"));
    }

    #endregion
}
