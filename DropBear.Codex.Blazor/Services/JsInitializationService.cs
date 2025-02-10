#region

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using DropBear.Codex.Blazor.Interfaces;
using Microsoft.JSInterop;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Thread-safe service for managing JavaScript module initialization.
/// </summary>
public sealed class JsInitializationService : IJsInitializationService
{
    private const int DefaultTimeoutSeconds = 5;
    private const int DefaultMaxAttempts = 50;
    private const int RetryDelayMs = 100;
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger _logger;

    private readonly ConcurrentDictionary<string, ModuleInitializationState> _moduleStates = new();
    private int _isDisposed;

    public JsInitializationService(IJSRuntime jsRuntime, ILogger logger)
    {
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        foreach (var state in _moduleStates.Values)
        {
            try
            {
                await state.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error disposing module state");
            }
        }

        _moduleStates.Clear();
    }

    public async Task EnsureJsModuleInitializedAsync(
        string moduleName,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        timeout ??= TimeSpan.FromSeconds(DefaultTimeoutSeconds);
        if (timeout.Value <= TimeSpan.Zero)
        {
            timeout = TimeSpan.FromSeconds(DefaultTimeoutSeconds);
        }

        var state = _moduleStates.GetOrAdd(
            moduleName,
            name => new ModuleInitializationState(name, _logger)
        );

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout.Value);

        try
        {
            await state.InitializeLock.WaitAsync(timeoutCts.Token);
            try
            {
                if (await IsModuleInitialized(moduleName, timeoutCts.Token))
                {
                    return;
                }

                await WaitForModuleInitializationAsync(
                    moduleName,
                    state,
                    timeoutCts.Token
                );
            }
            finally
            {
                state.InitializeLock.Release();
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"JavaScript module {moduleName} initialization timed out after {timeout.Value.TotalSeconds:F1}s"
            );
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task<bool> IsModuleInitialized(
        string moduleName,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _jsRuntime.InvokeAsync<bool>(
                "eval",
                cancellationToken,
                @$"
                    typeof window['{moduleName}'] !== 'undefined' &&
                    window['{moduleName}'] !== null &&
                    window['{moduleName}'].__initialized === true
                "
            );
        }
        catch (JSException)
        {
            return false;
        }
    }

    private async Task WaitForModuleInitializationAsync(
        string moduleName,
        ModuleInitializationState state,
        CancellationToken cancellationToken)
    {
        var attempts = 0;
        while (attempts++ < DefaultMaxAttempts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var needsInitialization = await _jsRuntime.InvokeAsync<bool>(
                    "eval",
                    cancellationToken,
                    @$"
                        typeof window['{moduleName}'] !== 'undefined' &&
                        window['{moduleName}'] !== null &&
                        window['{moduleName}'].__initialized !== true &&
                        typeof window['{moduleName}'].initialize === 'function'
                    "
                );

                if (needsInitialization)
                {
                    await _jsRuntime.InvokeVoidAsync(
                        $"{moduleName}.initialize",
                        cancellationToken
                    );

                    state.IncrementInitializationAttempts();
                    _logger.Debug(
                        "Initialization attempt {Attempt} for module {Module}",
                        state.InitializationAttempts,
                        moduleName
                    );
                }
                else if (await IsModuleInitialized(moduleName, cancellationToken))
                {
                    _logger.Debug(
                        "Module {Module} initialized after {Attempts} attempts",
                        moduleName,
                        state.InitializationAttempts
                    );
                    return;
                }
            }
            catch (JSException ex)
            {
                _logger.Warning(
                    ex,
                    "JS error during initialization attempt {Attempt} for {Module}",
                    attempts,
                    moduleName
                );
            }

            await Task.Delay(RetryDelayMs, cancellationToken);
        }

        throw new JSException(
            $"JavaScript module {moduleName} failed to initialize after {attempts} attempts"
        );
    }

    private void ThrowIfDisposed([CallerMemberName] string? caller = null)
    {
        if (Volatile.Read(ref _isDisposed) != 0)
        {
            throw new ObjectDisposedException(
                GetType().Name,
                $"Cannot {caller} on disposed {GetType().Name}"
            );
        }
    }

    /// <summary>
    ///     Manages initialization state for a specific module.
    /// </summary>
    private sealed class ModuleInitializationState : IAsyncDisposable
    {
        private readonly ILogger _logger;
        private readonly string _moduleName;
        private volatile int _initializationAttempts;
        private volatile int _isDisposed;

        public ModuleInitializationState(string moduleName, ILogger logger)
        {
            _moduleName = moduleName;
            _logger = logger;
            InitializeLock = new SemaphoreSlim(1, 1);
        }

        public SemaphoreSlim InitializeLock { get; }
        public int InitializationAttempts => _initializationAttempts;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            {
                return;
            }

            try
            {
                InitializeLock.Dispose();
            }
            catch (Exception ex)
            {
                _logger.Warning(
                    ex,
                    "Error disposing initialization lock for {Module}",
                    _moduleName
                );
            }
        }

        public void IncrementInitializationAttempts()
        {
            Interlocked.Increment(ref _initializationAttempts);
        }
    }
}
