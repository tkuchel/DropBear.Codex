#region

using DropBear.Codex.Core.Enums;

#endregion

namespace DropBear.Codex.Core.Results.Base;

/// <summary>
///     Result class with value and generic error type
/// </summary>
public class Result<T, TError> : Result<TError> where TError : ResultError
{
    private readonly Lazy<T> _lazyValue;

    private Result(Lazy<T> lazyValue, ResultState state, TError? error = null, Exception? exception = null)
        : base(state, error, exception)
    {
        _lazyValue = lazyValue;
    }

    protected Result(T value, ResultState state, TError? error = null, Exception? exception = null)
        : base(state, error, exception)
    {
        _lazyValue = new Lazy<T>(() => value);
    }

    protected Result(Func<T> valueFactory, ResultState state, TError? error = null, Exception? exception = null)
        : base(state, error, exception)
    {
        _lazyValue = new Lazy<T>(valueFactory);
    }

    public T? Value => IsSuccess ? _lazyValue.Value : default;

    public Result<TNew, TError> Map<TNew>(Func<T, TNew> mapper)
    {
        return IsSuccess
            ? Result<TNew, TError>.Success(mapper(Value!))
            : Result<TNew, TError>.Failure(Error!);
    }

    public async Task<Result<TNew, TError>> MapAsync<TNew>(Func<T, Task<TNew>> mapper)
    {
        if (!IsSuccess)
        {
            return Result<TNew, TError>.Failure(Error!);
        }

        var mappedValue = await mapper(Value!).ConfigureAwait(false);
        return Result<TNew, TError>.Success(mappedValue);
    }

    public Result<TNew, TError> Bind<TNew>(Func<T, Result<TNew, TError>> binder)
    {
        return IsSuccess ? binder(Value!) : Result<TNew, TError>.Failure(Error!);
    }

    public async Task<Result<TNew, TError>> BindAsync<TNew>(Func<T, Task<Result<TNew, TError>>> binder)
    {
        if (!IsSuccess)
        {
            return Result<TNew, TError>.Failure(Error!);
        }

        return await binder(Value!).ConfigureAwait(false);
    }

    public Result<T, TNewError> MapError<TNewError>(Func<TError, TNewError> errorMapper)
        where TNewError : ResultError
    {
        return IsSuccess
            ? Result<T, TNewError>.Success(Value!)
            : Result<T, TNewError>.Failure(errorMapper(Error!));
    }

    public Result<T, TError> Ensure(Func<T, bool> predicate, TError error)
    {
        if (!IsSuccess)
        {
            return this;
        }

        return predicate(Value!) ? this : Failure(error);
    }

    public T ValueOrDefault(T defaultValue = default!)
    {
        return IsSuccess ? Value! : defaultValue;
    }

    public T ValueOrThrow(string? errorMessage = null)
    {
        if (IsSuccess)
        {
            return Value!;
        }

        throw new InvalidOperationException(errorMessage ?? Error?.Message ?? "Operation failed");
    }

    public static Result<T, TError> Success(T value)
    {
        return new Result<T, TError>(value, ResultState.Success);
    }

    public static Result<T, TError> LazySuccess(Func<T> valueFactory)
    {
        return new Result<T, TError>(valueFactory, ResultState.Success);
    }

    public new static Result<T, TError> Failure(TError error, Exception? exception = null)
    {
        T defaultValue = default!;
        return new Result<T, TError>(defaultValue, ResultState.Failure, error, exception);
    }

    public static Result<T, TError> PartialSuccess(T value, TError error)
    {
        return new Result<T, TError>(value, ResultState.PartialSuccess, error);
    }

    public static implicit operator Result<T, TError>(T value)
    {
        return Success(value);
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

        if (!base.Equals(obj))
        {
            return false;
        }

        var other = (Result<T, TError>)obj;
        return IsSuccess && other.IsSuccess
            ? EqualityComparer<T>.Default.Equals(Value, other.Value)
            : true; // If both are failures, base.Equals already checked error equality
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = base.GetHashCode();
            if (IsSuccess)
            {
                hashCode = HashCode.Combine(hashCode, EqualityComparer<T>.Default.GetHashCode(Value!));
            }

            return hashCode;
        }
    }
}
