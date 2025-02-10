#region

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents an immutable validation result optimized for Blazor Server.
/// </summary>
public sealed class ValidationResult
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<ValidationResult>();

    private static readonly ValidationResult SuccessInstance = new(
        ImmutableArray<ValidationError>.Empty,
        null,
        null,
        ResultState.Success
    );

    private readonly ImmutableArray<ValidationError> _errors;
    private string? _cachedErrorString;

    private ValidationResult(
        ImmutableArray<ValidationError> errors,
        string? message,
        Exception? exception,
        ResultState state)
    {
        _errors = errors;
        Message = message;
        Exception = exception;
        State = state == ResultState.Success && !errors.IsEmpty
            ? ResultState.Failure
            : state;
    }

    public ResultState State { get; }
    public bool IsSuccess => State == ResultState.Success;
    public bool IsFailure => !IsSuccess;
    public bool HasErrors => !_errors.IsEmpty;
    public string? Message { get; }
    public Exception? Exception { get; }
    public IReadOnlyList<ValidationError> Errors => _errors;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValidationResult Success()
    {
        return SuccessInstance;
    }

    public static ValidationResult Failure(
        IEnumerable<ValidationError> errors,
        string? message = null,
        Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(errors);

        return new ValidationResult(
            errors.ToImmutableArray(),
            message,
            exception,
            ResultState.Failure
        );
    }

    public static ValidationResult Failure(
        string parameter,
        string errorMessage,
        Exception? exception = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameter);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        return new ValidationResult(
            ImmutableArray.Create(new ValidationError(parameter, errorMessage)),
            errorMessage,
            exception,
            ResultState.Failure
        );
    }

    public static ValidationResult Combine(IEnumerable<ValidationResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var resultsList = results.ToList();
        if (resultsList.Count == 0)
        {
            return Success();
        }

        if (resultsList.Count == 1)
        {
            return resultsList[0];
        }

        var combinedErrors = resultsList
            .SelectMany(r => r.Errors)
            .ToImmutableArray();

        if (combinedErrors.IsEmpty)
        {
            return Success();
        }

        var message = string.Join(
            Environment.NewLine,
            combinedErrors.Select(e => $"{e.Parameter}: {e.ErrorMessage}")
        );

        var exception = resultsList
            .Select(r => r.Exception)
            .FirstOrDefault(e => e != null);

        return new ValidationResult(
            combinedErrors,
            message,
            exception,
            ResultState.Failure
        );
    }

    public ValidationResult AddErrors(IEnumerable<ValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        var newErrors = _errors.AddRange(errors);
        if (newErrors == _errors)
        {
            return this;
        }

        return new ValidationResult(
            newErrors,
            Message,
            Exception,
            ResultState.Failure
        );
    }

    public ValidationResult AddError(string parameter, string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameter);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        return new ValidationResult(
            _errors.Add(new ValidationError(parameter, errorMessage)),
            Message,
            Exception,
            ResultState.Failure
        );
    }

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
            Logger.Error(ex, "Match operation failed");
            throw;
        }
    }

    public async Task<TResult> MatchAsync<TResult>(
        Func<Task<TResult>> onSuccess,
        Func<IReadOnlyList<ValidationError>, string?, Exception?, Task<TResult>> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            return IsSuccess
                ? await onSuccess()
                : await onFailure(Errors, Message, Exception);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Async match operation failed");
            throw;
        }
    }

    public ValidationResult Ensure(
        Func<bool> predicate,
        string parameter,
        string errorMessage)
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
            Logger.Error(ex, "Ensure operation failed");
            return AddError(parameter, $"Validation check failed: {ex.Message}");
        }
    }

    public string GetErrorsAsString(string separator = "\n")
    {
        if (_errors.IsEmpty)
        {
            return string.Empty;
        }

        return _cachedErrorString ??= string.Join(
            separator,
            _errors.Select(e => $"{e.Parameter}: {e.ErrorMessage}")
        );
    }

    public IEnumerable<ValidationError> GetErrorsFor(string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameter);

        return _errors.Where(e =>
            e.Parameter.Equals(parameter, StringComparison.OrdinalIgnoreCase));
    }

    public override string ToString()
    {
        return IsSuccess
            ? "Validation succeeded"
            : $"Validation failed: {GetErrorsAsString()}";
    }
}
