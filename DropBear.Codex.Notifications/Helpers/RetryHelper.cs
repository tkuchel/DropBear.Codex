using Serilog;

namespace DropBear.Codex.Notifications.Helpers;

public static class RetryHelper
{
    public static async Task<TResult> RetryAsync<TResult>(
        Func<Task<TResult>> action,
        int maxRetries,
        TimeSpan delay,
        ILogger logger,
        string retryMessage,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                logger.Error(ex, retryMessage, attempt);
                if (attempt == maxRetries) throw;

                await Task.Delay(delay, cancellationToken);
            }
        }
        throw new InvalidOperationException("Max retry attempts exceeded");
    }
}

