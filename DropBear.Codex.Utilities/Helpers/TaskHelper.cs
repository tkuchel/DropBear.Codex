#region

using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Utilities.Errors;

#endregion

namespace DropBear.Codex.Utilities.Helpers;

/// <summary>
///     Provides utility methods for working with tasks, optimized for .NET 8.
/// </summary>
public static class TaskHelper
{
    /// <summary>
    ///     Applies a timeout to a <see cref="Task" />.
    ///     Uses <see cref="ValueTask{TResult}" /> for efficiency.
    ///     Note: When timeout occurs, this method attempts to signal cancellation via the provided cancellationToken.
    ///     The task should respect the cancellation token for proper cleanup.
    /// </summary>
    /// <param name="task">The task to apply timeout to.</param>
    /// <param name="timeout">The timeout duration.</param>
    /// <param name="cancellationToken">Optional cancellation token that the task should respect for proper cancellation.</param>
    /// <returns>A Result indicating success or timeout/error.</returns>
    public static async ValueTask<Result<bool, TaskError>> WithTimeout(this Task task, TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (task is null)
        {
            return Result<bool, TaskError>.Failure(new TaskError("Task cannot be null."));
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);
            var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCts.Token)).ConfigureAwait(false);

            if (completedTask == task)
            {
                // Cancel the delay task since we completed successfully
                await timeoutCts.CancelAsync().ConfigureAwait(false);
                return Result<bool, TaskError>.Success(true);
            }

            // Timeout occurred - signal cancellation
            await timeoutCts.CancelAsync().ConfigureAwait(false);
            return Result<bool, TaskError>.Failure(new TaskError("Task timed out."));
        }
        catch (OperationCanceledException)
        {
            return Result<bool, TaskError>.Failure(new TaskError("Task was cancelled."));
        }
        catch (Exception ex)
        {
            return Result<bool, TaskError>.Failure(new TaskError("Error during task execution.", ex));
        }
    }

    /// <summary>
    ///     Applies a timeout to a <see cref="Task{TResult}" />.
    ///     Uses <see cref="ValueTask{TResult}" /> for efficiency.
    ///     Note: When timeout occurs, this method attempts to signal cancellation via the provided cancellationToken.
    ///     The task should respect the cancellation token for proper cleanup.
    /// </summary>
    /// <param name="task">The task to apply timeout to.</param>
    /// <param name="timeout">The timeout duration.</param>
    /// <param name="cancellationToken">Optional cancellation token that the task should respect for proper cancellation.</param>
    /// <returns>A Result containing the task result or timeout/error information.</returns>
    public static async ValueTask<Result<T, TaskError>> WithTimeout<T>(this Task<T> task, TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (task is null)
        {
            return Result<T, TaskError>.Failure(new TaskError("Task cannot be null."));
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);
            var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCts.Token)).ConfigureAwait(false);

            if (completedTask == task)
            {
                // Cancel the delay task since we completed successfully
                await timeoutCts.CancelAsync().ConfigureAwait(false);
                return Result<T, TaskError>.Success(await task.ConfigureAwait(false));
            }

            // Timeout occurred - signal cancellation
            await timeoutCts.CancelAsync().ConfigureAwait(false);
            return Result<T, TaskError>.Failure(new TaskError("Task timed out."));
        }
        catch (OperationCanceledException)
        {
            return Result<T, TaskError>.Failure(new TaskError("Task was cancelled."));
        }
        catch (Exception ex)
        {
            return Result<T, TaskError>.Failure(new TaskError("Error during task execution.", ex));
        }
    }
}
