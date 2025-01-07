#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Results.Compatibility;

#endregion

namespace DropBear.Codex.Utilities.Interfaces;

/// <summary>
///     Interface for managing debounced function or action calls.
/// </summary>
public interface IDebounceService
{
    /// <summary>
    ///     Debounces a function that returns a <see cref="Result{T}" />.
    ///     Ensures that the function is not executed more frequently than the specified minimum interval.
    /// </summary>
    /// <typeparam name="T">The type of the result returned by the function.</typeparam>
    /// <param name="function">
    ///     The function to execute which returns a <see cref="Task{TResult}" /> of <see cref="Result{T}" />
    ///     .
    /// </param>
    /// <param name="key">An optional unique identifier for the function call used for debouncing purposes.</param>
    /// <param name="debounceTime">The minimum time interval between successive executions.</param>
    /// <param name="caller">Automatically filled by the compiler to provide the method name of the caller.</param>
    /// <param name="filePath">Automatically filled by the compiler to provide the source file path of the caller.</param>
    /// <param name="lineNumber">
    ///     Automatically filled by the compiler to provide the line number in the source code of the
    ///     caller.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation, containing a <see cref="Result{T}" /> indicating the outcome of
    ///     the
    ///     function execution.
    /// </returns>
    Task<Result<T>> DebounceAsync<T>(
        Func<Task<Result<T>>> function,
        string key = "",
        TimeSpan? debounceTime = null,
        [CallerMemberName] string caller = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0);

    /// <summary>
    ///     Debounces an action that does not return a value.
    ///     Ensures that the action is not executed more frequently than the specified minimum interval.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="key">An optional unique identifier for the action call used for debouncing purposes.</param>
    /// <param name="debounceTime">The minimum time interval between successive executions.</param>
    /// <param name="caller">Automatically filled by the compiler to provide the method name of the caller.</param>
    /// <param name="filePath">Automatically filled by the compiler to provide the source file path of the caller.</param>
    /// <param name="lineNumber">
    ///     Automatically filled by the compiler to provide the line number in the source code of the
    ///     caller.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation, containing a <see cref="Result" /> indicating the outcome of the
    ///     action
    ///     execution.
    /// </returns>
    Task<Result> DebounceAsync(
        Action action,
        string key = "",
        TimeSpan? debounceTime = null,
        [CallerMemberName] string caller = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0);

    /// <summary>
    ///     Debounces a function that returns a <see cref="Task" /> of <see cref="Result" />.
    ///     Ensures that the function is not executed more frequently than the specified minimum interval.
    /// </summary>
    /// <param name="function">The asynchronous function to execute.</param>
    /// <param name="key">An optional unique identifier for the function call used for debouncing purposes.</param>
    /// <param name="debounceTime">The minimum time interval between successive executions.</param>
    /// <param name="caller">Automatically filled by the compiler to provide the method name of the caller.</param>
    /// <param name="filePath">Automatically filled by the compiler to provide the source file path of the caller.</param>
    /// <param name="lineNumber">
    ///     Automatically filled by the compiler to provide the line number in the source code of the
    ///     caller.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation, containing a <see cref="Result" /> indicating the outcome of the
    ///     debounced
    ///     function execution.
    /// </returns>
    Task<Result> DebounceAsync(
        Func<Task<Result>> function,
        string key = "",
        TimeSpan? debounceTime = null,
        [CallerMemberName] string caller = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0);
}
