#region

using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Common;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Base;
using Microsoft.Extensions.ObjectPool;

#endregion

namespace DropBear.Codex.Core.Results.Compatibility;

/// <summary>
///     A backwards-compatible, untyped Result class.
///     Use Result&lt;TError&gt; with custom error types instead.
/// </summary>
[Obsolete(
    "Use Result<TError> with custom error types instead of string-based errors. " +
    "This type will be removed in a future version.",
    DiagnosticId = "DROPBEAR002")]
[ExcludeFromCodeCoverage] // Legacy code
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class Result : Base.Result<LegacyError>, IPooledResult
{
    // FIXED: Changed ObjectPool<r> to ObjectPool<Result>
    private static readonly ObjectPool<Result> Pool =
        ObjectPoolManager.GetPool(() => new Result(ResultState.Success, null, null));

    #region Constructors

    /// <summary>
    ///     Protected constructor for internal use.
    /// </summary>
    protected Result(
        ResultState state,
        string? error,
        Exception? exception)
        : base(state, error is null ? null : new LegacyError(error), exception)
    {
    }

    #endregion

    #region Properties

    /// <summary>
    ///     Gets the error message, maintaining backwards compatibility.
    /// </summary>
    public string ErrorMessage => Error?.Message ?? string.Empty;

    private string DebuggerDisplay =>
        $"State = {State}, Success = {IsSuccess}, Error = {ErrorMessage}";

    #endregion

    #region Factory Methods

    /// <summary>
    ///     Creates a successful result using object pooling.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new static Result Success()
    {
        var result = Pool.Get();
        result.Initialize(ResultState.Success, null, null);
        return result;
    }

    /// <summary>
    ///     Creates a failed result using object pooling.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result Failure(string error, Exception? exception = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        var result = Pool.Get();
        result.Initialize(ResultState.Failure, error, exception);
        return result;
    }

    /// <summary>
    ///     Creates a failed result from multiple exceptions.
    /// </summary>
    public static Result Failure(IEnumerable<Exception> exceptions)
    {
        ArgumentNullException.ThrowIfNull(exceptions);

        var exceptionList = exceptions.ToList();
        if (exceptionList.Count == 0)
        {
            throw new ArgumentException("Exception collection cannot be empty", nameof(exceptions));
        }

        var primaryException = exceptionList.Count == 1
            ? exceptionList[0]
            : new AggregateException(exceptionList);

        var combinedErrorMessage = primaryException.Message;
        return Failure(combinedErrorMessage, primaryException);
    }

    /// <summary>
    ///     Creates a warning result using object pooling.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result Warning(string warning)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(warning);

        var result = Pool.Get();
        result.Initialize(ResultState.Warning, warning, null);
        return result;
    }

    /// <summary>
    ///     Creates a cancelled result using object pooling.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result Cancelled(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var result = Pool.Get();
        result.Initialize(ResultState.Cancelled, message, null);
        return result;
    }

    /// <summary>
    ///     Creates a pending result using object pooling.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result Pending(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var result = Pool.Get();
        result.Initialize(ResultState.Pending, message, null);
        return result;
    }

    /// <summary>
    ///     Creates a no-op result using object pooling.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result NoOp(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var result = Pool.Get();
        result.Initialize(ResultState.NoOp, message, null);
        return result;
    }

    #endregion

    #region Pooling Support

    /// <summary>
    ///     Initializes the pooled result instance with new state.
    ///     Internal method for use by the object pool.
    /// </summary>
    private void Initialize(ResultState state, string? error, Exception? exception)
    {
        // Call the base class initialization method
        InitializeInternal(state, error is null ? null : new LegacyError(error), exception);
    }

    /// <summary>
    ///     Resets this result to its default state before returning to the pool.
    ///     Implements IPooledResult.
    /// </summary>
    void IPooledResult.Reset()
    {
        // The base result doesn't hold mutable state that needs explicit cleanup
        // This is a no-op for now, but can be extended if needed
    }

    /// <summary>
    ///     Returns this result instance to the object pool for reuse.
    ///     Call this when you're done with a pooled result to improve performance.
    /// </summary>
    public void ReturnToPool()
    {
        Pool.Return(this);
    }

    #endregion

    #region Operators

    /// <summary>
    ///     Implicit conversion from exception to failed result.
    /// </summary>
    public static implicit operator Result(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return Failure(exception.Message, exception);
    }

    #endregion
}
