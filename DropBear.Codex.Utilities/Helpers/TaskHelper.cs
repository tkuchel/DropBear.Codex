namespace DropBear.Codex.Utilities.Helpers;

/// <summary>
///     Provides utility methods for working with tasks, including applying timeouts.
/// </summary>
public static class TaskHelper
{
    /// <summary>
    ///     Applies a timeout to a <see cref="TaskCompletionSource{TResult}" />.
    /// </summary>
    /// <typeparam name="TResult">The result type of the task.</typeparam>
    /// <param name="taskSource">The task completion source to apply the timeout to.</param>
    /// <param name="timeout">The duration after which the task should be canceled if not completed.</param>
    /// <returns>The task's result if completed within the timeout; otherwise throws a <see cref="TaskCanceledException" />.</returns>
    /// <exception cref="TaskCanceledException">Thrown if the task does not complete within the specified timeout.</exception>
    public static async Task<TResult> TimeoutAfter<TResult>(this TaskCompletionSource<TResult> taskSource,
        TimeSpan timeout)
    {
        using var timeoutCancellationTokenSource = new CancellationTokenSource();
        var completedTask = await Task
            .WhenAny(taskSource.Task, Task.Delay(timeout, timeoutCancellationTokenSource.Token))
            .ConfigureAwait(false);

        if (completedTask == taskSource.Task)
        {
            await timeoutCancellationTokenSource.CancelAsync()
                .ConfigureAwait(false); // Cancel the delay task to free up resources.
            return await taskSource.Task.ConfigureAwait(false); // Return the result of the completed task.
        }

        taskSource.TrySetCanceled(timeoutCancellationTokenSource.Token);

        throw new TaskCanceledException("The operation has timed out.");
    }

    /// <summary>
    ///     Applies a timeout to a <see cref="Task" />.
    /// </summary>
    /// <param name="task">The task to apply the timeout to.</param>
    /// <param name="timeout">The duration after which the task should be canceled if not completed.</param>
    /// <returns>True if the task completed within the timeout; otherwise false.</returns>
    public static async Task<bool> WithTimeout(this Task task, TimeSpan timeout)
    {
        var timeoutTask = Task.Delay(timeout);
        var completedTask = await Task.WhenAny(task, timeoutTask).ConfigureAwait(false);

        if (completedTask == timeoutTask)
        {
            return false; // Timeout occurred
        }

        await task.ConfigureAwait(false); // Propagate any exceptions
        return true; // Task completed within timeout
    }

    /// <summary>
    ///     Applies a timeout to a <see cref="Task{TResult}" />.
    /// </summary>
    /// <typeparam name="T">The result type of the task.</typeparam>
    /// <param name="task">The task to apply the timeout to.</param>
    /// <param name="timeout">The duration after which the task should be canceled if not completed.</param>
    /// <returns>A tuple indicating if the task completed and the result if it did.</returns>
    public static async Task<(bool IsCompleted, T? Result)> WithTimeout<T>(this Task<T> task, TimeSpan timeout)
    {
        var timeoutTask = Task.Delay(timeout);
        var completedTask = await Task.WhenAny(task, timeoutTask).ConfigureAwait(false);

        if (completedTask == timeoutTask)
        {
            return (false, default); // Timeout occurred
        }

        var result = await task.ConfigureAwait(false); // Propagate any exceptions and get result
        return (true, result); // Task completed within timeout
    }
}
