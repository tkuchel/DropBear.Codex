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
    /// </summary>
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
                return Result<bool, TaskError>.Success(true);
            }

            return Result<bool, TaskError>.Failure(new TaskError("Task timed out."));
        }
        catch (Exception ex)
        {
            return Result<bool, TaskError>.Failure(new TaskError("Error during task execution.", ex));
        }
    }

    /// <summary>
    ///     Applies a timeout to a <see cref="Task{TResult}" />.
    ///     Uses <see cref="ValueTask{TResult}" /> for efficiency.
    /// </summary>
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
                return Result<T, TaskError>.Success(await task);
            }

            return Result<T, TaskError>.Failure(new TaskError("Task timed out."));
        }
        catch (Exception ex)
        {
            return Result<T, TaskError>.Failure(new TaskError("Error during task execution.", ex));
        }
    }
}
