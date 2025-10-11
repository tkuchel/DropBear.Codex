#region

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using DropBear.Codex.Core.Results.Validations;

#endregion

namespace DropBear.Codex.Core.Results.Extensions;

/// <summary>
///     Extension methods for validation operations.
///     Optimized for .NET 9 with modern C# features and regex source generators.
/// </summary>
public static partial class ValidationExtensions
{
    #region Regex Patterns (Source Generated for Performance)

    /// <summary>
    ///     Compiled regex for email validation with timeout protection.
    ///     Uses modern email pattern that balances simplicity and correctness.
    /// </summary>
    [GeneratedRegex(@"^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EmailRegex();

    #endregion

    #region Boolean to ValidationResult

    /// <summary>
    ///     Converts a boolean to a ValidationResult with an error message.
    /// </summary>
    /// <param name="isValid">Whether the validation passed.</param>
    /// <param name="errorMessage">The error message if validation failed.</param>
    /// <returns>A ValidationResult indicating success or failure.</returns>
    /// <exception cref="ArgumentException">Thrown when errorMessage is null or whitespace.</exception>
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
    /// <param name="isValid">Whether the validation passed.</param>
    /// <param name="error">The validation error if validation failed.</param>
    /// <returns>A ValidationResult indicating success or failure.</returns>
    /// <exception cref="ArgumentNullException">Thrown when error is null.</exception>
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
    ///     Converts a nullable reference type to a ValidationResult.
    /// </summary>
    /// <typeparam name="T">The reference type to validate.</typeparam>
    /// <param name="value">The value to check for null.</param>
    /// <param name="errorMessage">The error message if value is null.</param>
    /// <returns>A ValidationResult indicating whether the value is non-null.</returns>
    /// <exception cref="ArgumentException">Thrown when errorMessage is null or whitespace.</exception>
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
    /// <typeparam name="T">The type of value to validate.</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <returns>A ValidationBuilder for fluent validation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValidationBuilder<T> Validate<T>(this T value) => ValidationBuilder<T>.For(value);

    /// <summary>
    ///     Validates a value using a fluent builder configuration.
    /// </summary>
    /// <typeparam name="T">The type of value to validate.</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="configure">Action to configure the validation builder.</param>
    /// <returns>The validation result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when configure is null.</exception>
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
    /// <typeparam name="T">The type of value to validate.</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="configureAsync">Async function to configure the validation builder.</param>
    /// <returns>A task that resolves to the validation result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when configureAsync is null.</exception>
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
    /// <param name="value">The string value to validate.</param>
    /// <param name="propertyName">The name of the property being validated.</param>
    /// <returns>A ValidationResult indicating whether the string is valid.</returns>
    /// <exception cref="ArgumentException">Thrown when propertyName is null or whitespace.</exception>
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
    /// <param name="value">The string value to validate.</param>
    /// <param name="propertyName">The name of the property being validated.</param>
    /// <returns>A ValidationResult indicating whether the string is valid.</returns>
    /// <exception cref="ArgumentException">Thrown when propertyName is null or whitespace.</exception>
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
    ///     Uses Span&lt;char&gt; for better performance.
    /// </summary>
    /// <param name="value">The string value to validate.</param>
    /// <param name="propertyName">The name of the property being validated.</param>
    /// <param name="minLength">The minimum required length.</param>
    /// <returns>A ValidationResult indicating whether the string meets the minimum length.</returns>
    /// <exception cref="ArgumentException">Thrown when propertyName is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when minLength is negative.</exception>
    public static ValidationResult MinLength(
        this string? value,
        string propertyName,
        int minLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentOutOfRangeException.ThrowIfNegative(minLength);

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
    /// <param name="value">The string value to validate.</param>
    /// <param name="propertyName">The name of the property being validated.</param>
    /// <param name="maxLength">The maximum allowed length.</param>
    /// <returns>A ValidationResult indicating whether the string meets the maximum length.</returns>
    /// <exception cref="ArgumentException">Thrown when propertyName is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when maxLength is negative.</exception>
    public static ValidationResult MaxLength(
        this string? value,
        string propertyName,
        int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentOutOfRangeException.ThrowIfNegative(maxLength);

        if (value is not null && value.Length > maxLength)
        {
            return ValidationResult.Failed(
                ValidationError.InvalidLength(propertyName, value, null, maxLength));
        }

        return ValidationResult.Success;
    }

    /// <summary>
    ///     Validates that a string matches a regex pattern with timeout protection.
    ///     Uses regex matching with a 1-second timeout to prevent ReDoS attacks.
    /// </summary>
    /// <param name="value">The string value to validate.</param>
    /// <param name="propertyName">The name of the property being validated.</param>
    /// <param name="pattern">The regex pattern to match against.</param>
    /// <param name="options">Optional regex options.</param>
    /// <param name="timeoutMilliseconds">Timeout in milliseconds (default: 1000ms).</param>
    /// <returns>A ValidationResult indicating whether the string matches the pattern.</returns>
    /// <exception cref="ArgumentException">Thrown when propertyName or pattern is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when timeoutMilliseconds is not positive.</exception>
    /// <remarks>
    ///     This method includes timeout protection to prevent Regular Expression Denial of Service (ReDoS) attacks.
    ///     If the regex evaluation exceeds the timeout, the validation will fail.
    /// </remarks>
    public static ValidationResult Matches(
        this string? value,
        string propertyName,
        string pattern,
        RegexOptions options = RegexOptions.None,
        int timeoutMilliseconds = 1000)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMilliseconds);

        if (value is null)
        {
            return ValidationResult.Failed(
                ValidationError.InvalidFormat(propertyName, string.Empty, pattern));
        }

        try
        {
            // FIXED: Added timeout to prevent ReDoS attacks
            var isMatch = Regex.IsMatch(
                value,
                pattern,
                options | RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(timeoutMilliseconds));

            if (!isMatch)
            {
                return ValidationResult.Failed(
                    ValidationError.InvalidFormat(propertyName, value, pattern));
            }

            return ValidationResult.Success;
        }
        catch (RegexMatchTimeoutException)
        {
            return ValidationResult.Failed(
                ValidationError.ForPropertyAndRule(
                    propertyName,
                    "RegexTimeout",
                    $"{propertyName} validation timed out. The pattern may be too complex.",
                    value));
        }
    }

    /// <summary>
    ///     Validates that a string is a valid email address using a generated regex pattern.
    ///     Uses regex source generator for optimal performance and includes timeout protection.
    /// </summary>
    /// <param name="value">The string value to validate as an email.</param>
    /// <param name="propertyName">The name of the property being validated.</param>
    /// <returns>A ValidationResult indicating whether the string is a valid email.</returns>
    /// <exception cref="ArgumentException">Thrown when propertyName is null or whitespace.</exception>
    /// <remarks>
    ///     This method uses a regex pattern that balances simplicity with RFC 5322 compliance.
    ///     For stricter validation, consider using a specialized email validation library.
    ///     The regex includes a 1-second timeout to prevent ReDoS attacks.
    /// </remarks>
    public static ValidationResult IsEmail(
        this string? value,
        string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        if (value is null)
        {
            return ValidationResult.Failed(
                ValidationError.InvalidEmail(propertyName, string.Empty));
        }

        try
        {
            // FIXED: Using source-generated regex with built-in timeout protection
            if (!EmailRegex().IsMatch(value))
            {
                return ValidationResult.Failed(
                    ValidationError.InvalidEmail(propertyName, value));
            }

            return ValidationResult.Success;
        }
        catch (RegexMatchTimeoutException)
        {
            return ValidationResult.Failed(
                ValidationError.ForPropertyAndRule(
                    propertyName,
                    "RegexTimeout",
                    $"{propertyName} email validation timed out.",
                    value));
        }
    }

    #endregion

    #region Numeric Validation Helpers

    /// <summary>
    ///     Validates that a comparable value is within an inclusive range.
    /// </summary>
    /// <typeparam name="T">The type of value to validate (must implement IComparable&lt;T&gt;).</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="propertyName">The name of the property being validated.</param>
    /// <param name="min">The minimum allowed value (inclusive).</param>
    /// <param name="max">The maximum allowed value (inclusive).</param>
    /// <returns>A ValidationResult indicating whether the value is within range.</returns>
    /// <exception cref="ArgumentException">Thrown when propertyName is null or whitespace.</exception>
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
    ///     Validates that a comparable value is greater than a minimum (exclusive).
    /// </summary>
    /// <typeparam name="T">The type of value to validate (must implement IComparable&lt;T&gt;).</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="propertyName">The name of the property being validated.</param>
    /// <param name="minimum">The exclusive minimum value.</param>
    /// <returns>A ValidationResult indicating whether the value is greater than minimum.</returns>
    /// <exception cref="ArgumentException">Thrown when propertyName is null or whitespace.</exception>
    public static ValidationResult GreaterThan<T>(
        this T value,
        string propertyName,
        T minimum)
        where T : IComparable<T>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        if (value.CompareTo(minimum) <= 0)
        {
            return ValidationResult.Failed(
                ValidationError.ForPropertyAndRule(
                    propertyName,
                    "GreaterThan",
                    $"{propertyName} must be greater than {minimum}",
                    value));
        }

        return ValidationResult.Success;
    }

    /// <summary>
    ///     Validates that a comparable value is greater than or equal to a minimum (inclusive).
    /// </summary>
    /// <typeparam name="T">The type of value to validate (must implement IComparable&lt;T&gt;).</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="propertyName">The name of the property being validated.</param>
    /// <param name="minimum">The inclusive minimum value.</param>
    /// <returns>A ValidationResult indicating whether the value is greater than or equal to minimum.</returns>
    /// <exception cref="ArgumentException">Thrown when propertyName is null or whitespace.</exception>
    public static ValidationResult GreaterThanOrEqual<T>(
        this T value,
        string propertyName,
        T minimum)
        where T : IComparable<T>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        if (value.CompareTo(minimum) < 0)
        {
            return ValidationResult.Failed(
                ValidationError.ForPropertyAndRule(
                    propertyName,
                    "GreaterThanOrEqual",
                    $"{propertyName} must be greater than or equal to {minimum}",
                    value));
        }

        return ValidationResult.Success;
    }

    /// <summary>
    ///     Validates that a comparable value is less than a maximum (exclusive).
    /// </summary>
    /// <typeparam name="T">The type of value to validate (must implement IComparable&lt;T&gt;).</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="propertyName">The name of the property being validated.</param>
    /// <param name="maximum">The exclusive maximum value.</param>
    /// <returns>A ValidationResult indicating whether the value is less than maximum.</returns>
    /// <exception cref="ArgumentException">Thrown when propertyName is null or whitespace.</exception>
    public static ValidationResult LessThan<T>(
        this T value,
        string propertyName,
        T maximum)
        where T : IComparable<T>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        if (value.CompareTo(maximum) >= 0)
        {
            return ValidationResult.Failed(
                ValidationError.ForPropertyAndRule(
                    propertyName,
                    "LessThan",
                    $"{propertyName} must be less than {maximum}",
                    value));
        }

        return ValidationResult.Success;
    }

    /// <summary>
    ///     Validates that a comparable value is less than or equal to a maximum (inclusive).
    /// </summary>
    /// <typeparam name="T">The type of value to validate (must implement IComparable&lt;T&gt;).</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="propertyName">The name of the property being validated.</param>
    /// <param name="maximum">The inclusive maximum value.</param>
    /// <returns>A ValidationResult indicating whether the value is less than or equal to maximum.</returns>
    /// <exception cref="ArgumentException">Thrown when propertyName is null or whitespace.</exception>
    public static ValidationResult LessThanOrEqual<T>(
        this T value,
        string propertyName,
        T maximum)
        where T : IComparable<T>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        if (value.CompareTo(maximum) > 0)
        {
            return ValidationResult.Failed(
                ValidationError.ForPropertyAndRule(
                    propertyName,
                    "LessThanOrEqual",
                    $"{propertyName} must be less than or equal to {maximum}",
                    value));
        }

        return ValidationResult.Success;
    }

    #endregion

    #region Collection Validation Helpers

    /// <summary>
    ///     Validates that a collection is not null or empty.
    ///     Optimized to avoid full enumeration when possible.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="collection">The collection to validate.</param>
    /// <param name="propertyName">The name of the property being validated.</param>
    /// <returns>A ValidationResult indicating whether the collection is valid.</returns>
    /// <exception cref="ArgumentException">Thrown when propertyName is null or whitespace.</exception>
    public static ValidationResult NotNullOrEmpty<T>(
        this IEnumerable<T>? collection,
        string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        if (collection is null)
        {
            return ValidationResult.Failed(ValidationError.Required(propertyName));
        }

        // Optimize for common collection types
        if (collection is ICollection<T> coll)
        {
            return coll.Count > 0
                ? ValidationResult.Success
                : ValidationResult.Failed(ValidationError.Required(propertyName));
        }

        // Fallback: Check if enumerable has any elements
        return collection.Any()
            ? ValidationResult.Success
            : ValidationResult.Failed(ValidationError.Required(propertyName));
    }

    /// <summary>
    ///     Validates that a collection has a minimum count.
    ///     Optimized to avoid unnecessary enumeration using TryGetNonEnumeratedCount (.NET 6+).
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="collection">The collection to validate.</param>
    /// <param name="propertyName">The name of the property being validated.</param>
    /// <param name="minCount">The minimum required count.</param>
    /// <returns>A ValidationResult indicating whether the collection meets the minimum count.</returns>
    /// <exception cref="ArgumentException">Thrown when propertyName is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when minCount is negative.</exception>
    public static ValidationResult MinCount<T>(
        this IEnumerable<T>? collection,
        string propertyName,
        int minCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentOutOfRangeException.ThrowIfNegative(minCount);

        if (collection is null)
        {
            return minCount == 0
                ? ValidationResult.Success
                : ValidationResult.Failed(
                    ValidationError.ForPropertyAndRule(
                        propertyName,
                        "MinCount",
                        $"{propertyName} must contain at least {minCount} items"));
        }

        // FIXED: Use TryGetNonEnumeratedCount for better performance
        var count = collection.TryGetNonEnumeratedCount(out var nonEnumeratedCount) ? nonEnumeratedCount :
            // Only enumerate if necessary
            collection.Count();

        if (count < minCount)
        {
            return ValidationResult.Failed(
                ValidationError.ForPropertyAndRule(
                    propertyName,
                    "MinCount",
                    $"{propertyName} must contain at least {minCount} items",
                    count));
        }

        return ValidationResult.Success;
    }

    /// <summary>
    ///     Validates that a collection has a maximum count.
    ///     Optimized to avoid unnecessary enumeration using TryGetNonEnumeratedCount (.NET 6+).
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="collection">The collection to validate.</param>
    /// <param name="propertyName">The name of the property being validated.</param>
    /// <param name="maxCount">The maximum allowed count.</param>
    /// <returns>A ValidationResult indicating whether the collection meets the maximum count.</returns>
    /// <exception cref="ArgumentException">Thrown when propertyName is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when maxCount is negative.</exception>
    public static ValidationResult MaxCount<T>(
        this IEnumerable<T>? collection,
        string propertyName,
        int maxCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentOutOfRangeException.ThrowIfNegative(maxCount);

        if (collection is null)
        {
            return ValidationResult.Success;
        }

        // FIXED: Use TryGetNonEnumeratedCount for better performance
        var count = collection.TryGetNonEnumeratedCount(out var nonEnumeratedCount) ? nonEnumeratedCount :
            // Only enumerate if necessary
            collection.Count();

        if (count > maxCount)
        {
            return ValidationResult.Failed(
                ValidationError.ForPropertyAndRule(
                    propertyName,
                    "MaxCount",
                    $"{propertyName} must contain at most {maxCount} items",
                    count));
        }

        return ValidationResult.Success;
    }

    #endregion
}
