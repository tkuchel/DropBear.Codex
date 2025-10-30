#region

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Utilities.Errors;

#endregion

namespace DropBear.Codex.Utilities.Validators;

/// <summary>
///     Provides comprehensive input validation utilities for secure data handling.
/// </summary>
/// <remarks>
///     Helps prevent injection attacks, data corruption, and invalid state by validating
///     inputs before processing. All validations use Result pattern for Railway-Oriented Programming.
/// </remarks>
public static partial class InputValidator
{
    private const int MaxSafeStringLength = 100_000; // 100KB of text
    private const int MaxEmailLength = 320; // RFC 5321
    private const int MaxUrlLength = 2048; // Browser limits

    /// <summary>
    ///     Validates that a string is not null, empty, or whitespace.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<string, UtilityError> ValidateNonEmpty(
        [NotNull] string? value,
        string parameterName = "value")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result<string, UtilityError>.Failure(
                UtilityError.ValidationFailed($"Parameter '{parameterName}' cannot be null, empty, or whitespace"));
        }

        return Result<string, UtilityError>.Success(value);
    }

    /// <summary>
    ///     Validates string length is within acceptable bounds.
    /// </summary>
    public static Result<string, UtilityError> ValidateLength(
        string value,
        int minLength = 0,
        int? maxLength = null,
        string parameterName = "value")
    {
        var nonEmptyResult = ValidateNonEmpty(value, parameterName);
        if (!nonEmptyResult.IsSuccess)
        {
            return nonEmptyResult;
        }

        var actualLength = value.Length;
        var max = maxLength ?? MaxSafeStringLength;

        if (actualLength < minLength)
        {
            return Result<string, UtilityError>.Failure(
                UtilityError.ValidationFailed(
                    $"Parameter '{parameterName}' must be at least {minLength} characters (actual: {actualLength})"));
        }

        if (actualLength > max)
        {
            return Result<string, UtilityError>.Failure(
                UtilityError.ValidationFailed(
                    $"Parameter '{parameterName}' must not exceed {max} characters (actual: {actualLength})"));
        }

        return Result<string, UtilityError>.Success(value);
    }

    /// <summary>
    ///     Validates an email address format.
    /// </summary>
    public static Result<string, UtilityError> ValidateEmail(string email)
    {
        var nonEmptyResult = ValidateNonEmpty(email, "email");
        if (!nonEmptyResult.IsSuccess)
        {
            return nonEmptyResult;
        }

        if (email.Length > MaxEmailLength)
        {
            return Result<string, UtilityError>.Failure(
                UtilityError.ValidationFailed($"Email address exceeds maximum length of {MaxEmailLength} characters"));
        }

        if (!EmailRegex().IsMatch(email))
        {
            return Result<string, UtilityError>.Failure(
                UtilityError.ValidationFailed("Invalid email address format"));
        }

        return Result<string, UtilityError>.Success(email);
    }

    /// <summary>
    ///     Validates a URL format.
    /// </summary>
    public static Result<Uri, UtilityError> ValidateUrl(
        string url,
        UriKind uriKind = UriKind.Absolute,
        string[]? allowedSchemes = null)
    {
        var nonEmptyResult = ValidateNonEmpty(url, "url");
        if (!nonEmptyResult.IsSuccess)
        {
            return Result<Uri, UtilityError>.Failure(nonEmptyResult.Error!);
        }

        if (url.Length > MaxUrlLength)
        {
            return Result<Uri, UtilityError>.Failure(
                UtilityError.ValidationFailed($"URL exceeds maximum length of {MaxUrlLength} characters"));
        }

        if (!Uri.TryCreate(url, uriKind, out var uri))
        {
            return Result<Uri, UtilityError>.Failure(
                UtilityError.ValidationFailed("Invalid URL format"));
        }

        // Validate scheme if restrictions specified
        if (allowedSchemes is { Length: > 0 })
        {
            var isAllowed = Array.Exists(allowedSchemes, scheme =>
                string.Equals(uri.Scheme, scheme, StringComparison.OrdinalIgnoreCase));

            if (!isAllowed)
            {
                return Result<Uri, UtilityError>.Failure(
                    UtilityError.ValidationFailed(
                        $"URL scheme '{uri.Scheme}' is not allowed. Allowed schemes: {string.Join(", ", allowedSchemes)}"));
            }
        }

        return Result<Uri, UtilityError>.Success(uri);
    }

    /// <summary>
    ///     Validates that a string contains only alphanumeric characters.
    /// </summary>
    public static Result<string, UtilityError> ValidateAlphanumeric(
        string value,
        bool allowSpaces = false,
        string parameterName = "value")
    {
        var nonEmptyResult = ValidateNonEmpty(value, parameterName);
        if (!nonEmptyResult.IsSuccess)
        {
            return nonEmptyResult;
        }

        var pattern = allowSpaces ? AlphanumericWithSpacesRegex() : AlphanumericRegex();
        if (!pattern.IsMatch(value))
        {
            var allowed = allowSpaces ? "letters, digits, and spaces" : "letters and digits";
            return Result<string, UtilityError>.Failure(
                UtilityError.ValidationFailed($"Parameter '{parameterName}' must contain only {allowed}"));
        }

        return Result<string, UtilityError>.Success(value);
    }

    /// <summary>
    ///     Validates a numeric range.
    /// </summary>
    public static Result<TNumber, UtilityError> ValidateRange<TNumber>(
        TNumber value,
        TNumber min,
        TNumber max,
        string parameterName = "value")
        where TNumber : struct, IComparable<TNumber>
    {
        if (value.CompareTo(min) < 0)
        {
            return Result<TNumber, UtilityError>.Failure(
                UtilityError.ValidationFailed(
                    $"Parameter '{parameterName}' must be at least {min} (actual: {value})"));
        }

        if (value.CompareTo(max) > 0)
        {
            return Result<TNumber, UtilityError>.Failure(
                UtilityError.ValidationFailed(
                    $"Parameter '{parameterName}' must not exceed {max} (actual: {value})"));
        }

        return Result<TNumber, UtilityError>.Success(value);
    }

    /// <summary>
    ///     Validates that a collection is not null or empty.
    /// </summary>
    public static Result<IEnumerable<T>, UtilityError> ValidateNonEmptyCollection<T>(
        IEnumerable<T>? collection,
        string parameterName = "collection")
    {
        if (collection is null)
        {
            return Result<IEnumerable<T>, UtilityError>.Failure(
                UtilityError.ValidationFailed($"Parameter '{parameterName}' cannot be null"));
        }

        // Use optimized check for common collection types
        var isEmpty = collection switch
        {
            ICollection<T> c => c.Count == 0,
            IReadOnlyCollection<T> rc => rc.Count == 0,
            _ => !collection.Any()
        };

        if (isEmpty)
        {
            return Result<IEnumerable<T>, UtilityError>.Failure(
                UtilityError.ValidationFailed($"Parameter '{parameterName}' cannot be empty"));
        }

        return Result<IEnumerable<T>, UtilityError>.Success(collection);
    }

    /// <summary>
    ///     Validates that a string does not contain dangerous characters for injection attacks.
    /// </summary>
    /// <remarks>
    ///     Checks for common SQL injection, XSS, and command injection patterns.
    ///     This is a basic check and should be combined with parameterized queries and proper encoding.
    /// </remarks>
    public static Result<string, UtilityError> ValidateSafe(
        string value,
        string parameterName = "value")
    {
        var nonEmptyResult = ValidateNonEmpty(value, parameterName);
        if (!nonEmptyResult.IsSuccess)
        {
            return nonEmptyResult;
        }

        // Check for dangerous SQL injection patterns
        if (SqlInjectionRegex().IsMatch(value))
        {
            return Result<string, UtilityError>.Failure(
                UtilityError.ValidationFailed(
                    $"Parameter '{parameterName}' contains potentially unsafe SQL patterns"));
        }

        // Check for XSS patterns
        if (XssPatternRegex().IsMatch(value))
        {
            return Result<string, UtilityError>.Failure(
                UtilityError.ValidationFailed(
                    $"Parameter '{parameterName}' contains potentially unsafe script patterns"));
        }

        // Check for command injection patterns
        if (CommandInjectionRegex().IsMatch(value))
        {
            return Result<string, UtilityError>.Failure(
                UtilityError.ValidationFailed(
                    $"Parameter '{parameterName}' contains potentially unsafe command patterns"));
        }

        return Result<string, UtilityError>.Success(value);
    }

    /// <summary>
    ///     Validates a phone number format (flexible international format).
    /// </summary>
    public static Result<string, UtilityError> ValidatePhoneNumber(string phoneNumber)
    {
        var nonEmptyResult = ValidateNonEmpty(phoneNumber, "phoneNumber");
        if (!nonEmptyResult.IsSuccess)
        {
            return nonEmptyResult;
        }

        // Remove common formatting characters
        var digitsOnly = new string(phoneNumber.Where(char.IsDigit).ToArray());

        if (digitsOnly.Length < 7 || digitsOnly.Length > 15)
        {
            return Result<string, UtilityError>.Failure(
                UtilityError.ValidationFailed("Phone number must contain between 7 and 15 digits"));
        }

        if (!PhoneRegex().IsMatch(phoneNumber))
        {
            return Result<string, UtilityError>.Failure(
                UtilityError.ValidationFailed("Invalid phone number format"));
        }

        return Result<string, UtilityError>.Success(phoneNumber);
    }

    /// <summary>
    ///     Validates a file path for safety (prevents path traversal attacks).
    /// </summary>
    public static Result<string, UtilityError> ValidateFilePath(
        string filePath,
        string? basePath = null)
    {
        var nonEmptyResult = ValidateNonEmpty(filePath, "filePath");
        if (!nonEmptyResult.IsSuccess)
        {
            return nonEmptyResult;
        }

        // Check for path traversal patterns
        if (filePath.Contains("..", StringComparison.Ordinal) ||
            filePath.Contains("~", StringComparison.Ordinal))
        {
            return Result<string, UtilityError>.Failure(
                UtilityError.ValidationFailed("File path contains potentially unsafe traversal patterns"));
        }

        // If base path provided, ensure resolved path is within it
        if (!string.IsNullOrEmpty(basePath))
        {
            try
            {
                var fullPath = Path.GetFullPath(Path.Combine(basePath, filePath));
                var fullBasePath = Path.GetFullPath(basePath);

                if (!fullPath.StartsWith(fullBasePath, StringComparison.Ordinal))
                {
                    return Result<string, UtilityError>.Failure(
                        UtilityError.ValidationFailed("File path attempts to access outside allowed directory"));
                }
            }
            catch (Exception ex)
            {
                return Result<string, UtilityError>.Failure(
                    UtilityError.ValidationFailed($"Invalid file path: {ex.Message}"));
            }
        }

        return Result<string, UtilityError>.Success(filePath);
    }

    // Compiled regex patterns for performance
    [GeneratedRegex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"^[a-zA-Z0-9]+$", RegexOptions.Compiled)]
    private static partial Regex AlphanumericRegex();

    [GeneratedRegex(@"^[a-zA-Z0-9\s]+$", RegexOptions.Compiled)]
    private static partial Regex AlphanumericWithSpacesRegex();

    [GeneratedRegex(@"(?i)(union|select|insert|update|delete|drop|create|alter|exec|execute|script|javascript|onerror|onload)\s*[\(\[]", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex SqlInjectionRegex();

    [GeneratedRegex(@"(?i)<\s*script|javascript:|onerror\s*=|onload\s*=|<\s*iframe", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex XssPatternRegex();

    [GeneratedRegex(@"[;&|`$\(\)><]", RegexOptions.Compiled)]
    private static partial Regex CommandInjectionRegex();

    [GeneratedRegex(@"^[\d\s\-\+\(\)\.]+$", RegexOptions.Compiled)]
    private static partial Regex PhoneRegex();
}
