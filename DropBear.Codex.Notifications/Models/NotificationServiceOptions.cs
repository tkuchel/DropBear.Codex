namespace DropBear.Codex.Notifications.Models;

/// <summary>
///     Configuration options for the NotificationService.
/// </summary>
public class NotificationServiceOptions
{
    /// <summary>
    ///     Gets or sets the semaphore timeout in minutes.
    /// </summary>
    public int SemaphoreTimeoutMinutes { get; set; } = 30;

    /// <summary>
    ///     Gets or sets the number of retry attempts for transient failures.
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    ///     Gets or sets the number of exceptions allowed before opening the circuit breaker.
    /// </summary>
    public int CircuitBreakerThreshold { get; set; } = 5;

    /// <summary>
    ///     Gets or sets the duration of circuit breaker in open state (in seconds).
    /// </summary>
    public int CircuitBreakerDurationSeconds { get; set; } = 60;
}
