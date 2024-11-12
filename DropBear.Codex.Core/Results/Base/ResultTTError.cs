#region

using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;

#endregion

namespace DropBear.Codex.Core.Results.Base;

/// <summary>
///     Result class with value and generic error type
/// </summary>
public class Result<T, TError> : Result<TError>, IResult<T, TError>
    where TError : ResultError
{
    private readonly Lazy<T> _lazyValue;

    #region Properties

    public T? Value => IsSuccess ? _lazyValue.Value : default;

    #endregion

    #region Operators

    public static implicit operator Result<T, TError>(T value)
    {
        return Success(value);
    }

    #endregion

    #region Constructors

    private Result(Lazy<T> lazyValue, ResultState state, TError? error = null, Exception? exception = null)
        : base(state, error, exception)
    {
        _lazyValue = lazyValue;
    }

    protected Result(T value, ResultState state, TError? error = null, Exception? exception = null)
        : this(new Lazy<T>(() => value), state, error, exception)
    {
    }

    protected Result(Func<T> valueFactory, ResultState state, TError? error = null, Exception? exception = null)
        : this(new Lazy<T>(valueFactory), state, error, exception)
    {
    }

    #endregion

    #region Public Methods

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

    public Result<TNew, TError> Map<TNew>(Func<T, TNew> mapper)
    {
        if (!IsSuccess)
        {
            return Result<TNew, TError>.Failure(Error!);
        }

        try
        {
            var mappedValue = mapper(Value!);
            return Result<TNew, TError>.Success(mappedValue);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during map operation");
            return Result<TNew, TError>.Failure(Error!, ex);
        }
    }

    public async ValueTask<Result<TNew, TError>> MapAsync<TNew>(
        Func<T, ValueTask<TNew>> mapper,
        CancellationToken cancellationToken = default)
    {
        if (!IsSuccess)
        {
            return Result<TNew, TError>.Failure(Error!);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await mapper(Value!).ConfigureAwait(false);
            return Result<TNew, TError>.Success(result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.Error(ex, "Exception during async map operation");
            return Result<TNew, TError>.Failure(Error!, ex);
        }
    }

    public Result<TNew, TError> Bind<TNew>(Func<T, Result<TNew, TError>> binder)
    {
        if (!IsSuccess)
        {
            return Result<TNew, TError>.Failure(Error!);
        }

        try
        {
            return binder(Value!);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during bind operation");
            return Result<TNew, TError>.Failure(Error!, ex);
        }
    }

    public async ValueTask<Result<TNew, TError>> BindAsync<TNew>(
        Func<T, ValueTask<Result<TNew, TError>>> binder)
    {
        if (!IsSuccess)
        {
            return Result<TNew, TError>.Failure(Error!);
        }

        try
        {
            return await binder(Value!).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during async bind operation");
            return Result<TNew, TError>.Failure(Error!, ex);
        }
    }

    public Result<T, TNewError> MapError<TNewError>(Func<TError, TNewError> errorMapper)
        where TNewError : ResultError
    {
        return IsSuccess
            ? Result<T, TNewError>.Success(Value!)
            : Result<T, TNewError>.Failure(errorMapper(Error!));
    }

    public IResult<T, TError> Ensure(Func<T, bool> predicate, TError error)
    {
        if (!IsSuccess)
        {
            return this;
        }

        try
        {
            return predicate(Value!) ? this : Failure(error);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during ensure operation");
            return Failure(error, ex);
        }
    }

    #endregion

    #region Factory Methods

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
        return new Result<T, TError>(
            new Lazy<T>(() => default!),
            ResultState.Failure,
            error,
            exception);
    }

    public static Result<T, TError> PartialSuccess(T value, TError error)
    {
        return new Result<T, TError>(value, ResultState.PartialSuccess, error);
    }

    #endregion

    #region Equality Members

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
        return !IsSuccess || !other.IsSuccess ||
               EqualityComparer<T>.Default.Equals(Value,
                   other.Value); // If both are failures, base.Equals already checked error equality
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

    #endregion
}
