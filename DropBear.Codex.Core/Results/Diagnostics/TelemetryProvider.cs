#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;

#endregion

namespace DropBear.Codex.Core.Results.Diagnostics;

/// <summary>
///     Provides global access to the telemetry system with configuration support.
///     Optimized for .NET 9 with thread-safe lazy initialization.
/// </summary>
public static class TelemetryProvider
{
    private static IResultTelemetry? _current;
    private static TelemetryOptions? _options;
    private static readonly Lock ConfigLock = new();

    /// <summary>
    ///     Gets the current telemetry instance.
    ///     Returns a disabled no-op instance if not configured.
    /// </summary>
    public static IResultTelemetry Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _current ?? DisabledTelemetry.Instance;
    }

    /// <summary>
    ///     Gets whether telemetry is currently enabled.
    /// </summary>
    public static bool IsEnabled
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _options?.Mode != TelemetryMode.Disabled && _current != null;
    }

    /// <summary>
    ///     Configures the global telemetry provider with the specified options.
    ///     This should be called once during application startup.
    /// </summary>
    /// <param name="options">The telemetry options to use.</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when validation fails.</exception>
    public static void Configure(TelemetryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options, nameof(options));

        var validationErrors = options.Validate().ToList();
        if (validationErrors.Count > 0)
        {
            throw new InvalidOperationException(
                $"TelemetryOptions validation failed: {string.Join(", ", validationErrors)}");
        }

        lock (ConfigLock)
        {
            // Dispose existing telemetry if present
            if (_current is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _options = options;
            _current = CreateTelemetry(options);
        }
    }

    /// <summary>
    ///     Configures telemetry using a builder pattern.
    /// </summary>
    /// <param name="configure">Action to configure the options.</param>
    /// <exception cref="ArgumentNullException">Thrown when configure is null.</exception>
    public static void Configure(Action<TelemetryOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure, nameof(configure));

        var options = new TelemetryOptions();
        configure(options);
        Configure(options);
    }

    /// <summary>
    ///     Resets the telemetry provider to its default disabled state.
    /// </summary>
    public static void Reset()
    {
        lock (ConfigLock)
        {
            if (_current is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _current = null;
            _options = null;
        }
    }

    /// <summary>
    ///     Sets a custom telemetry implementation.
    ///     Use this for testing or custom telemetry backends.
    /// </summary>
    /// <param name="telemetry">The custom telemetry instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when telemetry is null.</exception>
    public static void SetCustom(IResultTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry, nameof(telemetry));

        lock (ConfigLock)
        {
            if (_current is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _current = telemetry;
            _options = new TelemetryOptions { Mode = TelemetryMode.Synchronous };
        }
    }

    /// <summary>
    ///     Creates a telemetry instance based on the provided options.
    /// </summary>
    /// <param name="options">The telemetry options.</param>
    /// <returns>An instance of <see cref="IResultTelemetry" /> based on the specified mode.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an invalid telemetry mode is specified.</exception>
    private static IResultTelemetry CreateTelemetry(TelemetryOptions options)
    {
        return options.Mode switch
        {
            TelemetryMode.Disabled => DisabledTelemetry.Instance,
            TelemetryMode.FireAndForget => new DefaultResultTelemetry(options),
            TelemetryMode.BackgroundChannel => new DefaultResultTelemetry(options),
            TelemetryMode.Synchronous => new DefaultResultTelemetry(options),
            _ => throw new ArgumentOutOfRangeException(nameof(options), options.Mode, "Invalid telemetry mode")
        };
    }
}

/// <summary>
///     A no-op telemetry implementation with zero overhead.
///     Used when telemetry is disabled.
/// </summary>
/// <remarks>
///     This implementation uses the file-scoped sealed class pattern for optimal performance.
///     All methods are inlined and perform no operations, resulting in zero runtime overhead.
/// </remarks>
sealed file class DisabledTelemetry : IResultTelemetry
{
    /// <summary>
    ///     Gets the singleton instance of the disabled telemetry implementation.
    /// </summary>
    public static readonly DisabledTelemetry Instance = new();

    private DisabledTelemetry() { }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrackResultCreated(
        ResultState state,
        Type resultType,
        string? caller = null)
    {
        // No operation - zero overhead
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrackResultTransformed(
        ResultState originalState,
        ResultState newState,
        Type resultType,
        string? caller = null)
    {
        // No operation - zero overhead
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrackException(
        Exception exception,
        ResultState state,
        Type resultType,
        string? caller = null)
    {
        // No operation - zero overhead
    }
}
