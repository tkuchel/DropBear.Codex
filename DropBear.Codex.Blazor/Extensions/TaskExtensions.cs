namespace DropBear.Codex.Blazor.Extensions;

/// <summary>
///     Provides extension methods for <see cref="Task" /> and <see cref="ValueTask" />
///     that allow waiting for a specified timeout.
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    ///     Waits for the <see cref="ValueTask{TResult}" /> to complete within the specified <paramref name="timeout" />.
    ///     If the task does not complete in time, a <see cref="TimeoutException" /> is thrown.
    /// </summary>
    /// <typeparam name="T">The type of the result produced by the <see cref="ValueTask{TResult}" />.</typeparam>
    /// <param name="task">The <see cref="ValueTask{TResult}" /> to wait on.</param>
    /// <param name="timeout">The maximum time to wait for the task to complete.</param>
    /// <param name="cancellationToken">
    ///     An optional <see cref="CancellationToken" /> that can cancel the wait early.
    /// </param>
    /// <returns>The result of the completed <see cref="ValueTask{TResult}" />.</returns>
    /// <exception cref="TimeoutException">
    ///     Thrown if the task does not complete within the specified <paramref name="timeout" />.
    /// </exception>
    public static async ValueTask<T> WaitAsync<T>(
        this ValueTask<T> task,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        // Create a linked token so we can cancel if either the user-provided token or the timeout triggers
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            // Wait for either the original task or the timeout task to complete
            var completedTask = await Task.WhenAny(
                task.AsTask(),
                Task.Delay(Timeout.InfiniteTimeSpan, timeoutCts.Token) // or Task.Delay(timeout, timeoutCts.Token)
            ).ConfigureAwait(false);

            // If the completed task is not the original, it means we timed out
            if (completedTask != task.AsTask())
            {
                throw new TimeoutException();
            }

            // Await the result of the original ValueTask
            return await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // If we get here, it means the timeout elapsed rather than caller's explicit cancellation
            throw new TimeoutException();
        }
    }

    /// <summary>
    ///     Waits for the <see cref="ValueTask" /> to complete within the specified <paramref name="timeout" />.
    ///     If the task does not complete in time, a <see cref="TimeoutException" /> is thrown.
    /// </summary>
    /// <param name="task">The <see cref="ValueTask" /> to wait on.</param>
    /// <param name="timeout">The maximum time to wait for the task to complete.</param>
    /// <param name="cancellationToken">
    ///     An optional <see cref="CancellationToken" /> that can cancel the wait early.
    /// </param>
    /// <exception cref="TimeoutException">
    ///     Thrown if the task does not complete within the specified <paramref name="timeout" />.
    /// </exception>
    public static async ValueTask WaitAsync(
        this ValueTask task,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            // Wait for either the original task or the timeout
            var completedTask = await Task.WhenAny(
                task.AsTask(),
                Task.Delay(Timeout.InfiniteTimeSpan, timeoutCts.Token)
            ).ConfigureAwait(false);

            // If the completed task is not the original, throw
            if (completedTask != task.AsTask())
            {
                throw new TimeoutException();
            }

            // Await the original task
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException();
        }
    }

    /// <summary>
    ///     Waits for the <see cref="Task{TResult}" /> to complete within the specified <paramref name="timeout" />.
    ///     If the task does not complete in time, a <see cref="TimeoutException" /> is thrown.
    ///     This method is provided for compatibility with <see cref="Task{TResult}" />-based code.
    /// </summary>
    /// <typeparam name="T">The type of the result produced by the <see cref="Task{TResult}" />.</typeparam>
    /// <param name="task">The <see cref="Task{TResult}" /> to wait on.</param>
    /// <param name="timeout">The maximum time to wait for the task to complete.</param>
    /// <param name="cancellationToken">
    ///     An optional <see cref="CancellationToken" /> that can cancel the wait early.
    /// </param>
    /// <returns>The result of the completed <see cref="Task{TResult}" />.</returns>
    /// <exception cref="TimeoutException">
    ///     Thrown if the task does not complete within the specified <paramref name="timeout" />.
    /// </exception>
    public static async Task<T> WaitAsync<T>(
        this Task<T> task,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            // WhenAny returns the first task to finish (either the original or the delay)
            var completedTask = await Task.WhenAny(
                task,
                Task.Delay(Timeout.InfiniteTimeSpan, timeoutCts.Token)
            ).ConfigureAwait(false);

            if (completedTask != task)
            {
                throw new TimeoutException();
            }

            // Return the result if completed in time
            return await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException();
        }
    }

    /// <summary>
    ///     Waits for the <see cref="Task" /> to complete within the specified <paramref name="timeout" />.
    ///     If the task does not complete in time, a <see cref="TimeoutException" /> is thrown.
    ///     This method is provided for compatibility with <see cref="Task" />-based code.
    /// </summary>
    /// <param name="task">The <see cref="Task" /> to wait on.</param>
    /// <param name="timeout">The maximum time to wait for the task to complete.</param>
    /// <param name="cancellationToken">
    ///     An optional <see cref="CancellationToken" /> that can cancel the wait early.
    /// </param>
    /// <exception cref="TimeoutException">
    ///     Thrown if the task does not complete within the specified <paramref name="timeout" />.
    /// </exception>
    public static async Task WaitAsync(
        this Task task,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            var completedTask = await Task.WhenAny(
                task,
                Task.Delay(Timeout.InfiniteTimeSpan, timeoutCts.Token)
            ).ConfigureAwait(false);

            if (completedTask != task)
            {
                throw new TimeoutException();
            }

            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException();
        }
    }
}
