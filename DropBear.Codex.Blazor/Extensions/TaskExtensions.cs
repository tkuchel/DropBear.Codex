namespace DropBear.Codex.Blazor.Extensions;

public static class TaskExtensions
{
    public static async ValueTask<T> WaitAsync<T>(this ValueTask<T> task, TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            return await Task.WhenAny(task.AsTask(), Task.Delay(timeout, timeoutCts.Token))
                .ContinueWith(t =>
                {
                    if (t.Result == task.AsTask())
                    {
                        return task.Result;
                    }

                    throw new TimeoutException();
                }, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException();
        }
    }

    public static async ValueTask WaitAsync(this ValueTask task, TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await Task.WhenAny(task.AsTask(), Task.Delay(timeout, timeoutCts.Token))
                .ContinueWith(t =>
                {
                    if (t.Result != task.AsTask())
                    {
                        throw new TimeoutException();
                    }
                }, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException();
        }
    }

    // Keep the Task versions for compatibility
    public static async Task<T> WaitAsync<T>(this Task<T> task, TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            return await Task.WhenAny(task, Task.Delay(timeout, timeoutCts.Token))
                .ContinueWith(t =>
                {
                    if (t.Result == task)
                    {
                        return task.Result;
                    }

                    throw new TimeoutException();
                }, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException();
        }
    }

    public static async Task WaitAsync(this Task task, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await Task.WhenAny(task, Task.Delay(timeout, timeoutCts.Token))
                .ContinueWith(t =>
                {
                    if (t.Result != task)
                    {
                        throw new TimeoutException();
                    }
                }, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException();
        }
    }
}
