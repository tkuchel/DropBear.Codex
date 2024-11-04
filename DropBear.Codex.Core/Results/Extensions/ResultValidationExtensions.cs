#region

using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Errors;

#endregion

namespace DropBear.Codex.Core.Results.Extensions;

/// <summary>
///     Provides validation extension methods for Result types
/// </summary>
public static class ResultValidationExtensions
{
    public static Result<T, ValidationError> Validate<T>(
        this T value,
        params Func<T, Result<T, ValidationError>>[] validators)
    {
        var errors = new List<ValidationError>();
        var current = value;

        foreach (var validator in validators)
        {
            var result = validator(current);
            if (!result.IsSuccess)
            {
                if (result.Error != null)
                {
                    errors.Add(result.Error);
                }
            }
            else
            {
                current = result.Value;
            }
        }

        return errors.Any()
            ? Result<T, ValidationError>.Failure(errors.Combine())
            : Result<T, ValidationError>.Success(current);
    }

    public static async Task<Result<T, ValidationError>> ValidateAsync<T>(
        this T value,
        params Func<T, Task<Result<T, ValidationError>>>[] validators)
    {
        var errors = new List<ValidationError>();
        var current = value;

        foreach (var validator in validators)
        {
            var result = await validator(current).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                if (result.Error != null)
                {
                    errors.Add(result.Error);
                }
            }
            else
            {
                current = result.Value;
            }
        }

        return errors.Any()
            ? Result<T, ValidationError>.Failure(errors.Combine())
            : Result<T, ValidationError>.Success(current);
    }

    public static Result<T, ValidationError> Ensure<T>(
        this Result<T, ValidationError> result,
        Func<T, bool> predicate,
        string field,
        string message)
    {
        if (!result.IsSuccess)
        {
            return result;
        }

        return predicate(result.Value)
            ? result
            : Result<T, ValidationError>.Failure(new ValidationError(field, message));
    }

    public static async Task<Result<T, ValidationError>> EnsureAsync<T>(
        this Result<T, ValidationError> result,
        Func<T, Task<bool>> predicate,
        string field,
        string message)
    {
        if (!result.IsSuccess)
        {
            return result;
        }

        return await predicate(result.Value)
            .ConfigureAwait(false)
            ? result
            : Result<T, ValidationError>.Failure(new ValidationError(field, message));
    }

    public static Result<T, ValidationError> NotNull<T>(
        this T? value,
        string field)
        where T : class
    {
        return value != null
            ? Result<T, ValidationError>.Success(value)
            : Result<T, ValidationError>.Failure(new ValidationError(field, "Value cannot be null"));
    }

    public static Result<T, ValidationError> NotNull<T>(
        this T? value,
        string field)
        where T : struct
    {
        return value.HasValue
            ? Result<T, ValidationError>.Success(value.Value)
            : Result<T, ValidationError>.Failure(new ValidationError(field, "Value cannot be null"));
    }

    public static Result<string, ValidationError> NotNullOrEmpty(
        this string? value,
        string field)
    {
        return !string.IsNullOrEmpty(value)
            ? Result<string, ValidationError>.Success(value)
            : Result<string, ValidationError>.Failure(new ValidationError(field, "Value cannot be empty"));
    }

    public static Result<string, ValidationError> NotNullOrWhiteSpace(
        this string? value,
        string field)
    {
        return !string.IsNullOrWhiteSpace(value)
            ? Result<string, ValidationError>.Success(value)
            : Result<string, ValidationError>.Failure(new ValidationError(field, "Value cannot be whitespace"));
    }

    public static Result<T, ValidationError> InRange<T>(
        this T value,
        string field,
        T min,
        T max)
        where T : IComparable<T>
    {
        if (value.CompareTo(min) >= 0 && value.CompareTo(max) <= 0)
        {
            return Result<T, ValidationError>.Success(value);
        }

        return Result<T, ValidationError>.Failure(
            new ValidationError(field, $"Value must be between {min} and {max}"));
    }
}
