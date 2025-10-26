#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Utilities.Errors;

#endregion

namespace DropBear.Codex.Utilities.Interfaces;

/// <summary>
///     Interface for a service that manages debounced function or action calls.
/// </summary>
public interface IDebounceService
{
    /// <summary>
    ///     Debounces a function that returns a <see cref="Result{T, TError}" />.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <typeparam name="TError">The type of error that can occur.</typeparam>
    /// <param name="function">The function to execute which returns a <see cref="Result{T, TError}" />.</param>
    /// <param name="key">An optional unique identifier for the function call used for debouncing purposes.</param>
    /// <param name="debounceTime">The minimum time interval between successive executions.</param>
    /// <param name="caller">Automatically filled by the compiler to provide the method name of the caller.</param>
    /// <param name="filePath">Automatically filled by the compiler to provide the source file path of the caller.</param>
    /// <param name="lineNumber">
    ///     Automatically filled by the compiler to provide the line number in the source code of the caller.
    /// </param>
    /// <returns>
    ///     A <see cref="Task" /> containing a <see cref="Result{T, TError}" /> with the function result or a debounce
    ///     error.
    /// </returns>
    Task<Result<T, TError>> DebounceAsync<T, TError>(
        Func<Task<Result<T, TError>>> function,
        string key = "",
        TimeSpan? debounceTime = null,
        [CallerMemberName] string caller = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
        where TError : ResultError;

    /// <summary>
    ///     Debounces a function that returns a <see cref="Result{T, DebounceError}" />.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="function">The function to execute which returns a <see cref="Result{T, DebounceError}" />.</param>
    /// <param name="key">An optional unique identifier for the function call used for debouncing purposes.</param>
    /// <param name="debounceTime">The minimum time interval between successive executions.</param>
    /// <param name="caller">Automatically filled by the compiler to provide the method name of the caller.</param>
    /// <param name="filePath">Automatically filled by the compiler to provide the source file path of the caller.</param>
    /// <param name="lineNumber">
    ///     Automatically filled by the compiler to provide the line number in the source code of the caller.
    /// </param>
    /// <returns>
    ///     A <see cref="Task" /> containing a <see cref="Result{T, DebounceError}" /> with the function result or a
    ///     debounce error.
    /// </returns>
    Task<Result<T, DebounceError>> DebounceAsync<T>(
        Func<Task<Result<T, DebounceError>>> function,
        string key = "",
        TimeSpan? debounceTime = null,
        [CallerMemberName] string caller = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0);

    /// <summary>
    ///     Debounces an action that does not return a value.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="key">An optional unique identifier for the action call used for debouncing purposes.</param>
    /// <param name="debounceTime">The minimum time interval between successive executions.</param>
    /// <param name="caller">Automatically filled by the compiler to provide the method name of the caller.</param>
    /// <param name="filePath">Automatically filled by the compiler to provide the source file path of the caller.</param>
    /// <param name="lineNumber">
    ///     Automatically filled by the compiler to provide the line number in the source code of the caller.
    /// </param>
    /// <returns>
    ///     A <see cref="Task" /> containing a <see cref="Result{Unit, DebounceError}" /> indicating success or a debounce
    ///     error.
    /// </returns>
    Task<Result<Unit, DebounceError>> DebounceAsync(
        Action action,
        string key = "",
        TimeSpan? debounceTime = null,
        [CallerMemberName] string caller = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0);

    /// <summary>
    ///     Debounces a function that returns a <see cref="Result{Unit, DebounceError}" />.
    /// </summary>
    /// <param name="function">The asynchronous function to execute.</param>
    /// <param name="key">An optional unique identifier for the function call used for debouncing purposes.</param>
    /// <param name="debounceTime">The minimum time interval between successive executions.</param>
    /// <param name="caller">Automatically filled by the compiler to provide the method name of the caller.</param>
    /// <param name="filePath">Automatically filled by the compiler to provide the source file path of the caller.</param>
    /// <param name="lineNumber">
    ///     Automatically filled by the compiler to provide the line number in the source code of the caller.
    /// </param>
    /// <returns>
    ///     A <see cref="Task" /> containing a <see cref="Result{Unit, DebounceError}" /> indicating success or a debounce
    ///     error.
    /// </returns>
    Task<Result<Unit, DebounceError>> DebounceAsync(
        Func<Task<Result<Unit, DebounceError>>> function,
        string key = "",
        TimeSpan? debounceTime = null,
        [CallerMemberName] string caller = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0);

    /// <summary>
    ///     Clears all debounce entries from the cache.
    /// </summary>
    void ClearCache();
}
