#region

using System.Collections;
using System.Collections.ObjectModel;
using DropBear.Codex.Core.Enums;

#endregion

namespace DropBear.Codex.Core.Results.Compatibility;

/// <summary>
///     Backwards-compatible Result{T} class with a generic value type
/// </summary>
public class Result<T> : Result, IEnumerable<T>, IEquatable<Result<T>>
{
    private readonly T _value;

    protected Result(T value, string? error, Exception? exception, ResultState state)
        : base(state, error, exception)
    {
        _value = value;
    }

    public T Value =>
        IsSuccess ? _value : throw new InvalidOperationException("Cannot access Value on a failed result.");

    public IEnumerator<T> GetEnumerator()
    {
        if (IsSuccess)
        {
            yield return Value;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public bool Equals(Result<T>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (!base.Equals(other))
        {
            return false;
        }

        // Only compare values if both results are successful
        if (IsSuccess && other.IsSuccess)
        {
            return EqualityComparer<T>.Default.Equals(Value, other.Value);
        }

        // If both are failures, they're equal if their base properties are equal
        return !IsSuccess && !other.IsSuccess;
    }

    public new Result<T> OnFailure(Action<string, Exception?> action)
    {
        base.OnFailure(action);
        return this;
    }

    public Result<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        return IsSuccess ? Result<TResult>.Success(mapper(Value)) : Result<TResult>.Failure(ErrorMessage, Exception);
    }

    public Result<TResult> Bind<TResult>(Func<T, Result<TResult>> func)
    {
        return IsSuccess ? func(Value) : Result<TResult>.Failure(ErrorMessage, Exception);
    }

    public Result<T> Ensure(Func<T, bool> predicate, string errorMessage)
    {
        if (!IsSuccess)
        {
            return this;
        }

        return predicate(Value) ? this : Failure(errorMessage);
    }

    public T ValueOrDefault(T defaultValue = default!)
    {
        return IsSuccess ? Value : defaultValue;
    }

    public T ValueOrThrow(string? errorMessage = null)
    {
        if (IsSuccess)
        {
            return Value;
        }

        throw new InvalidOperationException(errorMessage ?? ErrorMessage);
    }

    public static Result<T> Success(T value)
    {
        return new Result<T>(value, null, null, ResultState.Success);
    }

    public new static Result<T> Failure(string error, Exception? exception = null)
    {
        return new Result<T>(default!, error, exception, ResultState.Failure);
    }

    public static Result<T> Failure(Exception exception)
    {
        return new Result<T>(default!, exception.Message, exception, ResultState.Failure);
    }

    public new static Result<T> Failure(IEnumerable<Exception> exceptions)
    {
        var exceptionList = exceptions.ToList();
        var errorMessage = exceptionList.Count > 0 ? exceptionList[0].Message : "Multiple errors occurred.";
        return new Result<T>(default!, errorMessage, exceptionList.FirstOrDefault(), ResultState.Failure)
        {
            Exceptions = new ReadOnlyCollection<Exception>(exceptionList)
        };
    }

    public static Result<T> PartialSuccess(T value, string error)
    {
        return new Result<T>(value, error, null, ResultState.PartialSuccess);
    }

    public static Result<T> Try(Func<T> func)
    {
        try
        {
            return Success(func());
        }
        catch (Exception ex)
        {
            return Failure(ex);
        }
    }

    public static implicit operator Result<T>(T value)
    {
        return Success(value);
    }

    public static implicit operator Result<T>(Exception exception)
    {
        return Failure(exception);
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != GetType())
        {
            return false;
        }

        return Equals(obj as Result<T>);
    }

    public override int GetHashCode()
    {
        // Include base hash code and Value hash code, but only if successful
        if (IsSuccess)
        {
            return HashCode.Combine(base.GetHashCode(), Value);
        }

        return base.GetHashCode();
    }
}
