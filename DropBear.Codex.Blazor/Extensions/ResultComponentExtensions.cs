#region

using System.Reflection;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Core.Results.Base;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Extensions;

/// <summary>
///     Provides extension methods for Blazor components to handle Result types efficiently.
/// </summary>
public static class ResultComponentExtensions
{
    /// <summary>
    ///     Handles a Result and invokes appropriate callbacks based on success or failure.
    ///     Automatically manages component state updates and exception handling.
    /// </summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <typeparam name="TComponent">The component type.</typeparam>
    /// <param name="component">The component instance.</param>
    /// <param name="resultTask">An asynchronous task that returns a Result.</param>
    /// <param name="onSuccess">Action to execute on success.</param>
    /// <param name="onError">Action to execute on error.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task HandleResultAsync<T, TError, TComponent>(
        this TComponent component,
        ValueTask<Result<T, TError>> resultTask,
        Action<T> onSuccess,
        Action<TError> onError)
        where TError : ResultError
        where TComponent : ComponentBase
    {
        try
        {
            var result = await resultTask.ConfigureAwait(false);

            // Use reflection to access protected InvokeAsync method
            var invokeAsyncMethod = typeof(ComponentBase).GetMethod(
                "InvokeAsync",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(Action) },
                null);

            if (invokeAsyncMethod != null)
            {
                var task = (Task)invokeAsyncMethod.Invoke(component, new object[]
                {
                    new Action(() =>
                    {
                        if (result.IsSuccess)
                        {
                            onSuccess(result.Value!);
                        }
                        else
                        {
                            onError(result.Error!);
                        }
                    })
                })!;

                await task.ConfigureAwait(false);
            }
            else
            {
                // Fallback if reflection fails
                if (result.IsSuccess)
                {
                    onSuccess(result.Value!);
                }
                else
                {
                    onError(result.Error!);
                }
            }
        }
        catch (Exception ex)
        {
            // If the component is a DropBearComponentBase, use its logging through explicit casting
            if (component is DropBearComponentBase dropBearComponent)
            {
                // Call the public LogError method if available, otherwise use fallback
                var logErrorMethod = typeof(DropBearComponentBase).GetMethod(
                    "LogError",
                    BindingFlags.Instance | BindingFlags.Public);

                if (logErrorMethod != null)
                {
                    logErrorMethod.Invoke(dropBearComponent,
                        new object[] { "Error handling result", ex, new object[] { } });
                }
            }
        }
    }

    /// <summary>
    ///     Handles a Result with loading state management.
    /// </summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <typeparam name="TComponent">The component type.</typeparam>
    /// <param name="component">The component instance.</param>
    /// <param name="resultTask">An asynchronous task that returns a Result.</param>
    /// <param name="onSuccess">Action to execute on success.</param>
    /// <param name="onError">Action to execute on error.</param>
    /// <param name="setLoadingState">Action to set the loading state.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task HandleResultWithLoadingAsync<T, TError, TComponent>(
        this TComponent component,
        ValueTask<Result<T, TError>> resultTask,
        Action<T> onSuccess,
        Action<TError> onError,
        Action<bool> setLoadingState)
        where TError : ResultError
        where TComponent : ComponentBase
    {
        try
        {
            // Set loading state to true
            setLoadingState(true);

            // Use reflection to access protected InvokeAsync method for state update
            var invokeAsyncMethod = typeof(ComponentBase).GetMethod(
                "InvokeAsync",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(Action) },
                null);

            if (invokeAsyncMethod != null)
            {
                var updateTask = (Task)invokeAsyncMethod.Invoke(component, new object[]
                {
                    new Action(() =>
                    {
                        /* Just trigger a render */
                    })
                })!;

                await updateTask.ConfigureAwait(false);
            }

            // Process the result
            var result = await resultTask.ConfigureAwait(false);

            // Invoke final state update and callbacks
            if (invokeAsyncMethod != null)
            {
                var finalTask = (Task)invokeAsyncMethod.Invoke(component, new object[]
                {
                    new Action(() =>
                    {
                        setLoadingState(false);

                        if (result.IsSuccess)
                        {
                            onSuccess(result.Value!);
                        }
                        else
                        {
                            onError(result.Error!);
                        }
                    })
                })!;

                await finalTask.ConfigureAwait(false);
            }
            else
            {
                // Fallback if reflection fails
                setLoadingState(false);

                if (result.IsSuccess)
                {
                    onSuccess(result.Value!);
                }
                else
                {
                    onError(result.Error!);
                }
            }
        }
        catch (Exception ex)
        {
            // Reset loading state and log error
            setLoadingState(false);

            // If the component is a DropBearComponentBase, use its logging
            if (component is DropBearComponentBase dropBearComponent)
            {
                // Call the public LogError method if available, otherwise use fallback
                var logErrorMethod = typeof(DropBearComponentBase).GetMethod(
                    "LogError",
                    BindingFlags.Instance | BindingFlags.Public);

                if (logErrorMethod != null)
                {
                    logErrorMethod.Invoke(dropBearComponent,
                        new object[] { "Error handling result", ex, new object[] { } });
                }
            }
        }
    }

    /// <summary>
    ///     Executes an action with the Result pattern, handling exceptions automatically.
    /// </summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="action">The action to execute.</param>
    /// <param name="errorFactory">Factory function to create errors from exceptions.</param>
    /// <returns>A Result representing the outcome of the action.</returns>
    public static Result<T, TError> ExecuteWithResult<T, TError>(
        Func<T> action,
        Func<Exception, TError> errorFactory)
        where TError : ResultError
    {
        try
        {
            var result = action();
            return Result<T, TError>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<T, TError>.Failure(errorFactory(ex), ex);
        }
    }

    /// <summary>
    ///     Asynchronously executes an action with the Result pattern, handling exceptions automatically.
    /// </summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="action">The asynchronous action to execute.</param>
    /// <param name="errorFactory">Factory function to create errors from exceptions.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task representing the asynchronous operation that returns a Result.</returns>
    public static async ValueTask<Result<T, TError>> ExecuteWithResultAsync<T, TError>(
        Func<CancellationToken, ValueTask<T>> action,
        Func<Exception, TError> errorFactory,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        try
        {
            var result = await action(cancellationToken).ConfigureAwait(false);
            return Result<T, TError>.Success(result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<T, TError>.Failure(errorFactory(ex), ex);
        }
    }
}



// Example usage in a component:
// private bool _isLoading;
//
// private async Task LoadDataAsync()
// {
//     await this.HandleResultWithLoadingAsync(
//         _dataService.GetDataAsync(),
//         data => _data = data,
//         error => _errorMessage = error.Message,
//         isLoading => _isLoading = isLoading);
// }
