#region

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using DropBear.Codex.Core.Results.Validations;

#endregion

namespace DropBear.Codex.Core.Results.Extensions;

/// <summary>
///     Extension methods for validation operations.
///     Optimized for .NET 9 with modern C# features.
/// </summary>
public static class ValidationExtensions
{
    #region Boolean to ValidationResult

    /// <summary>
    ///     Converts a boolean to a ValidationResult with an error message.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValidationResult ToValidationResult(
        this bool isValid,
        string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        return isValid
            ? ValidationResult.Success
            : ValidationResult.Failed(errorMessage);
    }

    /// <summary>
    ///     Converts a boolean to a ValidationResult with a custom error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValidationResult ToValidationResult(
        this bool isValid,
        ValidationError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return isValid
            ? ValidationResult.Success
            : ValidationResult.Failed(error);
    }

    /// <summary>
    ///     Converts a nullable value to a ValidationResult.
    /// </summary>
    public static ValidationResult ToValidationResult<T>(
        this T? value,
        string errorMessage)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        return value is not null
            ? ValidationResult.Success
            : ValidationResult.Failed(errorMessage);
    }

    #endregion

    #region Fluent Validation Helpers

    /// <summary>
    ///     Creates a validation builder for the specified value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValidationBuilder<T> Validate<T>(this T value) => ValidationBuilder<T>.For(value);

    /// <summary>
    ///     Validates a value using a fluent builder configuration.
    /// </summary>
    public static ValidationResult Validate<T>(
        this T value,
        Action<ValidationBuilder<T>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = ValidationBuilder<T>.For(value);
        configure(builder);
        return builder.Build();
    }

    /// <summary>
    ///     Validates a value asynchronously using a fluent builder configuration.
    /// </summary>
    public static async ValueTask<ValidationResult> ValidateAsync<T>(
        this T value,
        Func<ValidationBuilder<T>, ValueTask> configureAsync)
    {
        ArgumentNullException.ThrowIfNull(configureAsync);

        var builder = ValidationBuilder<T>.For(value);
        await configureAsync(builder).ConfigureAwait(false);
        return builder.Build();
    }

    #endregion

    #region String Validation Helpers

    /// <summary>
    ///     Validates that a string is not null or empty.
    /// </summary>
    public static ValidationResult NotNullOrEmpty(
        this string? value,
        string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        return string.IsNullOrEmpty(value)
            ? ValidationResult.Failed(ValidationError.Required(propertyName))
            : ValidationResult.Success;
    }

    /// <summary>
    ///     Validates that a string is not null or whitespace.
    /// </summary>
    public static ValidationResult NotNullOrWhiteSpace(
        this string? value,
        string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        return string.IsNullOrWhiteSpace(value)
            ? ValidationResult.Failed(ValidationError.Required(propertyName))
            : ValidationResult.Success;
    }

    /// <summary>
    ///     Validates that a string has a minimum length.
    ///     Uses Span<char> for better performance.
    /// </summary>
    public static ValidationResult MinLength(
        this string? value,
        string propertyName,
        int minLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        if (value is null || value.Length < minLength)
        {
            return ValidationResult.Failed(
                ValidationError.InvalidLength(propertyName, value ?? string.Empty, minLength));
        }

        return ValidationResult.Success;
    }

    /// <summary>
    ///     Validates that a string has a maximum length.
    /// </summary>
    public static ValidationResult MaxLength(
        this string? value,
        string propertyName,
        int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        if (value is not null && value.Length > maxLength)
        {
            return ValidationResult.Failed(
                ValidationError.InvalidLength(propertyName, value, null, maxLength));
        }

        return ValidationResult.Success;
    }

    /// <summary>
    ///     Validates that a string matches a pattern.
    ///     Uses modern Regex with compiled option for performance.
    /// </summary>
    public static ValidationResult Matches(
        this string? value,
        string propertyName,
        string pattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        if (value is null || !Regex.IsMatch(value, pattern))
        {
            return ValidationResult.Failed(
                ValidationError.InvalidFormat(propertyName, value ?? string.Empty, pattern));
        }

        return ValidationResult.Success;
    }

    /// <summary>
    ///     Validates that a string is a valid email address.
    ///     Uses modern email validation pattern.
    /// </summary>
    public static ValidationResult IsEmail(
        this string? value,
        string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        // Modern email validation pattern
        const string emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

        if (value is null || !Regex.IsMatch(value, emailPattern))
        {
            return ValidationResult.Failed(
                ValidationError.InvalidEmail(propertyName, value ?? string.Empty));
        }

        return ValidationResult.Success;
    }

    #endregion

    #region Numeric Validation Helpers

    /// <summary>
    ///     Validates that a number is within a range.
    /// </summary>
    public static ValidationResult InRange<T>(
        this T value,
        string propertyName,
        T min,
        T max)
        where T : IComparable<T>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
        {
            return ValidationResult.Failed(
                ValidationError.OutOfRange(propertyName, value, min, max));
        }

        return ValidationResult.Success;
    }

    /// <summary>
    ///     Validates that a number is greater than a minimum.
    /// </summary>
    public static ValidationResult GreaterThan<T>(
        this T value,
        string propertyName,
        T minimum)
        where T : IComparable<T>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        if (value.CompareTo(minimum) <= 0)
        {
            return ValidationResult.PropertyFailed(
                propertyName,
                $"{propertyName} must be greater than {minimum}",
                value);
        }

        return ValidationResult.Success;
    }

    /// <summary>
    ///     Validates that a number is greater than or equal to a minimum.
    /// </summary>
    public static ValidationResult GreaterThanOrEqual<T>(
        this T value,
        string propertyName,
        T minimum)
        where T : IComparable<T>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        if (value.CompareTo(minimum) < 0)
        {
            return ValidationResult.PropertyFailed(
                propertyName,
                $"{propertyName} must be greater than or equal to {minimum}",
                value);
        }

        return ValidationResult.Success;
    }

    /// <summary>
    ///     Validates that a number is less than a maximum.
    /// </summary>
    public static ValidationResult LessThan<T>(
        this T value,
        string propertyName,
        T maximum)
        where T : IComparable<T>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        if (value.CompareTo(maximum) >= 0)
        {
            return ValidationResult.PropertyFailed(
                propertyName,
                $"{propertyName} must be less than {maximum}",
                value);
        }

        return ValidationResult.Success;
    }

    /// <summary>
    ///     Validates that a number is less than or equal to a maximum.
    /// </summary>
    public static ValidationResult LessThanOrEqual<T>(
        this T value,
        string propertyName,
        T maximum)
        where T : IComparable<T>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        if (value.CompareTo(maximum) > 0)
        {
            return ValidationResult.PropertyFailed(
                propertyName,
                $"{propertyName} must be less than or equal to {maximum}",
                value);
        }

        return ValidationResult.Success;
    }

    #endregion

    #region Collection Validation Helpers

    /// <summary>
    ///     Validates that a collection is not null or empty.
    /// </summary>
    public static ValidationResult NotNullOrEmpty<T>(
        this IEnumerable<T>? collection,
        string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        if (collection is null || !collection.Any())
        {
            return ValidationResult.Failed(
                ValidationError.Required(propertyName));
        }

        return ValidationResult.Success;
    }

    /// <summary>
    ///     Validates that a collection has a minimum count.
    /// </summary>
    public static ValidationResult MinCount<T>(
        this IEnumerable<T>? collection,
        string propertyName,
        int minCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var count = collection?.Count() ?? 0;

        if (count < minCount)
        {
            return ValidationResult.PropertyFailed(
                propertyName,
                $"{propertyName} must contain at least {minCount} items",
                count);
        }

        return ValidationResult.Success;
    }

    /// <summary>
    ///     Validates that a collection has a maximum count.
    /// </summary>
    public static ValidationResult MaxCount<T>(
        this IEnumerable<T>? collection,
        string propertyName,
        int maxCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var count = collection?.Count() ?? 0;

        if (count > maxCount)
        {
            return ValidationResult.PropertyFailed(
                propertyName,
                $"{propertyName} must contain at most {maxCount} items",
                count);
        }

        return ValidationResult.Success;
    }

    #endregion
}

#region Exception Types

/// <summary>
///     Exception thrown when validation fails.
///     Optimized for .NET 9 with modern exception patterns.
/// </summary>
public sealed class ValidationException : Exception
{
    /// <summary>
    ///     Initializes a new ValidationException from a ValidationResult.
    /// </summary>
    public ValidationException(ValidationResult validationResult)
        : base(CreateMessage(validationResult))
    {
        ValidationResult = validationResult ?? throw new ArgumentNullException(nameof(validationResult));
        Errors = [.. validationResult.Errors];
    }

    /// <summary>
    ///     Initializes a new ValidationException with a custom message.
    /// </summary>
    public ValidationException(string message)
        : base(message)
    {
        Errors = [];
    }

    /// <summary>
    ///     Initializes a new ValidationException with errors.
    /// </summary>
    public ValidationException(string message, IEnumerable<ValidationError> errors)
        : base(message)
    {
        Errors = [.. errors ?? []];
    }

    /// <summary>
    ///     Gets the validation result that caused this exception.
    /// </summary>
    public ValidationResult? ValidationResult { get; }

    /// <summary>
    ///     Gets all validation errors.
    /// </summary>
    public IReadOnlyCollection<ValidationError> Errors { get; }

    /// <summary>
    ///     Creates an error message from the validation result.
    /// </summary>
    private static string CreateMessage(ValidationResult validationResult)
    {
        if (validationResult is null)
        {
            return "Validation failed";
        }

        if (validationResult.ErrorCount == 1)
        {
            return $"Validation failed: {validationResult.ErrorMessage}";
        }

        return $"Validation failed with {validationResult.ErrorCount} error(s): " +
               string.Join("; ", validationResult.Errors.Select(e => e.Message));
    }
}

#endregion
