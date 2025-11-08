#region

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Errors;
using DropBear.Codex.Blazor.Extensions;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Core.Results.Base;
using Microsoft.JSInterop;
using Serilog;
using Serilog.Events;

#endregion

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Thread-safe service for managing JavaScript module initialization with enhanced
///     resilience, memory optimization, and proper error handling.
/// </summary>
public sealed class JsInitializationService : IJsInitializationService
{
    #region Constructors

    /// <summary>
    ///     Initializes a new instance of the <see cref="JsInitializationService" /> class.
    /// </summary>
    /// <param name="jsRuntime">The JavaScript runtime to use for browser interaction.</param>
    /// <param name="logger">The logger for diagnostic information.</param>
    /// <exception cref="ArgumentNullException">Thrown if either parameter is null.</exception>
    public JsInitializationService(IJSRuntime jsRuntime, ILogger logger)
    {
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize with capacity hints to reduce resizing
        _moduleStates = new ConcurrentDictionary<string, ModuleInitializationState>(
            Environment.ProcessorCount * 2,
            20,
            StringComparer.Ordinal); // Assume ~20 modules max in typical apps
    }

    #endregion

    #region Nested Types

    /// <summary>
    ///     Manages initialization state for a specific module.
    /// </summary>
    private sealed class ModuleInitializationState : IAsyncDisposable
    {
        private readonly ILogger _logger;
        private volatile int _initializationAttempts;
        private volatile int _isDisposed;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ModuleInitializationState" /> class.
        /// </summary>
        /// <param name="moduleName">The name of the module.</param>
        /// <param name="logger">The logger for diagnostic information.</param>
        public ModuleInitializationState(string moduleName, ILogger logger)
        {
            ModuleName = moduleName;
            _logger = logger;
            InitializeLock = new SemaphoreSlim(1, 1);
        }

        /// <summary>
        ///     Gets the name of the module.
        /// </summary>
        public string ModuleName { get; }

        /// <summary>
        ///     Gets the lock used to synchronize initialization.
        /// </summary>
        public SemaphoreSlim InitializeLock { get; }

        /// <summary>
        ///     Gets the number of initialization attempts.
        /// </summary>
        public int InitializationAttempts => _initializationAttempts;

        /// <summary>
        ///     Disposes of the resources used by this instance.
        /// </summary>
        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            {
                return ValueTask.CompletedTask;
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
                    ModuleName
                );
            }

            return ValueTask.CompletedTask;
        }

        /// <summary>
        ///     Increments the initialization attempts counter.
        /// </summary>
        public void IncrementInitializationAttempts()
        {
            Interlocked.Increment(ref _initializationAttempts);
        }
    }

    #endregion

    #region Fields and Constants

    /// <summary>
    ///     Default timeout for module initialization in seconds.
    /// </summary>
    private const int DefaultTimeoutSeconds = 5;

    /// <summary>
    ///     Maximum number of initialization attempts for a module.
    /// </summary>
    private const int DefaultMaxAttempts = 50;

    /// <summary>
    ///     Delay between retry attempts in milliseconds.
    /// </summary>
    private const int RetryDelayMs = 100;

    /// <summary>
    ///     JavaScript runtime used to interact with the browser.
    /// </summary>
    private readonly IJSRuntime _jsRuntime;

    /// <summary>
    ///     Logger for diagnostic information.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    ///     Tracks initialization state for each module with improved memory efficiency.
    /// </summary>
    private readonly ConcurrentDictionary<string, ModuleInitializationState> _moduleStates;

    /// <summary>
    ///     Tracks pre-initialized modules to skip checks.
    /// </summary>
    private readonly HashSet<string> _preInitializedModules = new(StringComparer.Ordinal);

    /// <summary>
    ///     Flag to track disposal state.
    /// </summary>
    private int _isDisposed;

    /// <summary>
    ///     Cancellation token source for shutdown.
    /// </summary>
    private readonly CancellationTokenSource _disposalCts = new();

    #endregion

    #region Public Methods

    /// <summary>
    ///     Marks a module as pre-initialized to skip initialization checks.
    ///     Useful for modules known to be already loaded (e.g., from a _Host.cshtml script).
    /// </summary>
    /// <param name="moduleName">The name of the module that is already initialized.</param>
    public void PreInitializeModule(string moduleName)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        lock (_preInitializedModules)
        {
            _preInitializedModules.Add(moduleName);
            _logger.Information("Module {Module} marked as pre-initialized", moduleName);
        }
    }

    /// <summary>
    ///     Ensures a JavaScript module is initialized with timeout and cancellation support.
    /// </summary>
    /// <param name="moduleName">The name of the module to initialize.</param>
    /// <param name="timeout">Optional timeout duration (defaults to 5 seconds).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that completes when the module is initialized.</returns>
    /// <exception cref="ArgumentException">If moduleName is null or empty.</exception>
    /// <exception cref="ObjectDisposedException">If the service is disposed.</exception>
    /// <exception cref="TimeoutException">If initialization exceeds the timeout period.</exception>
    /// <exception cref="OperationCanceledException">If initialization is cancelled.</exception>
    /// <exception cref="JSException">If module initialization fails.</exception>
    public async ValueTask EnsureJsModuleInitializedAsync(
        string moduleName,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        // Check if already pre-initialized
        if (_preInitializedModules.Contains(moduleName))
        {
            _logger.Debug("Module {Module} is pre-initialized, skipping initialization check", moduleName);
            return;
        }

        timeout ??= TimeSpan.FromSeconds(DefaultTimeoutSeconds);
        if (timeout.Value <= TimeSpan.Zero)
        {
            timeout = TimeSpan.FromSeconds(DefaultTimeoutSeconds);
        }

        // Create a linked token that includes our disposal token
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            _disposalCts.Token,
            cancellationToken);

        // Create a timeout task instead of using CancelAfter
        var timeoutTask = Task.Delay(timeout.Value, linkedCts.Token);

        var state = _moduleStates.GetOrAdd(
            moduleName,
            name => new ModuleInitializationState(name, _logger)
        );

        try
        {
            await state.InitializeLock.WaitAsync(linkedCts.Token);
            try
            {
                // If already initialized, return immediately
                if (await IsModuleInitialized(moduleName, linkedCts.Token))
                {
                    return;
                }

                // Create initialization task
                var initializationTask = WaitForModuleInitializationAsync(
                    moduleName,
                    state,
                    linkedCts.Token
                );

                // Wait for initialization or timeout
                var completedTask = await Task.WhenAny(initializationTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    throw new TimeoutException(
                        $"JavaScript module {moduleName} initialization timed out after {timeout.Value.TotalSeconds:F1}s"
                    );
                }

                // Observe any exceptions
                await initializationTask;
            }
            finally
            {
                state.InitializeLock.Release();
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // This is a timeout converted to cancellation
            throw new TimeoutException(
                $"JavaScript module {moduleName} initialization timed out after {timeout.Value.TotalSeconds:F1}s"
            );
        }
    }

    /// <summary>
    ///     Checks if a JavaScript module is already initialized.
    /// </summary>
    /// <param name="moduleName">The name of the module to check.</param>
    /// <returns>True if the module is initialized, false otherwise.</returns>
    public bool IsModuleInitialized(string moduleName)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        // Check if pre-initialized
        if (_preInitializedModules.Contains(moduleName))
        {
            return true;
        }

        // For synchronous check, we can't do async JS call, so return false
        // The actual check happens in the async version
        return false;
    }

    /// <summary>
    ///     Clears the initialization state for all modules.
    /// </summary>
    public void ClearInitializationState()
    {
        ThrowIfDisposed();

        _preInitializedModules.Clear();
        _logger.Information("Cleared all module initialization states");
    }

    /// <summary>
    ///     Ensures a JavaScript module is initialized with circuit reconnection resilience.
    ///     This method will automatically retry initialization if the circuit is disconnected.
    /// </summary>
    /// <param name="moduleName">The name of the module to initialize.</param>
    /// <param name="component">The component requesting initialization (used for circuit state).</param>
    /// <param name="timeout">Optional timeout duration (defaults to 5 seconds).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that completes when the module is initialized.</returns>
    /// <exception cref="ArgumentException">If moduleName is null or empty.</exception>
    /// <exception cref="ObjectDisposedException">If the service is disposed.</exception>
    /// <exception cref="TimeoutException">If initialization exceeds the timeout period or reconnection fails.</exception>
    /// <exception cref="OperationCanceledException">If initialization is cancelled.</exception>
    /// <exception cref="JSException">If module initialization fails after retries.</exception>
    public async Task EnsureJsModuleInitializedWithResilienceAsync(
        string moduleName,
        DropBearComponentBase component,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentNullException.ThrowIfNull(component);

        // Apply circuit resilience pattern using extension method
        await component.WithCircuitResilienceAsync(
            async ct =>
            {
                await EnsureJsModuleInitializedAsync(moduleName, timeout, ct);
                return true; // Return value is ignored
            },
            TimeSpan.FromSeconds(30), // Reconnection timeout
            3, // Maximum retries
            cancellationToken);
    }

    /// <summary>
    ///     Gets initialization metrics for a specific module or all modules.
    /// </summary>
    /// <param name="moduleName">Optional module name to get metrics for. If null, gets metrics for all modules.</param>
    /// <returns>A dictionary of metrics.</returns>
    public Result<IDictionary<string, object>, JsInitializationError> GetModuleMetrics(string? moduleName = null)
    {
        ThrowIfDisposed();

        try
        {
            var metrics = new Dictionary<string, object>(StringComparer.Ordinal);

            if (!string.IsNullOrWhiteSpace(moduleName))
            {
                // Get metrics for a specific module
                if (_moduleStates.TryGetValue(moduleName, out var state))
                {
                    metrics["ModuleName"] = moduleName;
                    metrics["InitializationAttempts"] = state.InitializationAttempts;
                    metrics["IsPreInitialized"] = _preInitializedModules.Contains(moduleName);
                    return Result<IDictionary<string, object>, JsInitializationError>.Success(metrics);
                }

                return Result<IDictionary<string, object>, JsInitializationError>.Failure(
                    JsInitializationError.ModuleNotFound(moduleName));
            }

            // Get metrics for all modules
            metrics["TotalModules"] = _moduleStates.Count;
            metrics["PreInitializedModules"] = _preInitializedModules.Count;

            var moduleMetrics = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var (name, state) in _moduleStates)
            {
                moduleMetrics[name] = new
                {
                    Attempts = state.InitializationAttempts,
                    IsPreInitialized = _preInitializedModules.Contains(name)
                };
            }

            metrics["Modules"] = moduleMetrics;

            return Result<IDictionary<string, object>, JsInitializationError>.Success(metrics);
        }
        catch (Exception ex)
        {
            return Result<IDictionary<string, object>, JsInitializationError>.Failure(
                JsInitializationError.OperationFailed("Failed to get module metrics", ex.Message),
                ex);
        }
    }

    /// <summary>
    ///     Disposes of the resources used by this service.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        try
        {
            // Signal cancellation to all pending operations
            await _disposalCts.CancelAsync();

            // Dispose all module states
            foreach (var state in _moduleStates.Values)
            {
                try
                {
                    await state.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Error disposing module state for {ModuleName}", state.ModuleName);
                }
            }

            _moduleStates.Clear();
            _preInitializedModules.Clear();
            _disposalCts.Dispose();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during JsInitializationService disposal");
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    ///     Checks if a JavaScript module is initialized.
    /// </summary>
    /// <param name="moduleName">The name of the module to check.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the module is initialized; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task<bool> IsModuleInitialized(
        string moduleName,
        CancellationToken cancellationToken)
    {
        try
        {
            // Optimized to use a single JS invocation with a template string
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

    /// <summary>
    ///     Waits for a JavaScript module to initialize, attempting to invoke its initialize method if needed.
    /// </summary>
    /// <param name="moduleName">The name of the module to initialize.</param>
    /// <param name="state">The module's initialization state.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the module is initialized.</returns>
    /// <exception cref="JSException">Thrown if module initialization fails after maximum attempts.</exception>
    private async Task WaitForModuleInitializationAsync(
        string moduleName,
        ModuleInitializationState state,
        CancellationToken cancellationToken)
    {
        var attempts = 0;
        var lastError = string.Empty;

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

                    if (_logger.IsEnabled(LogEventLevel.Debug))
                    {
                        _logger.Debug(
                            "Initialization attempt {Attempt} for module {Module}",
                            state.InitializationAttempts,
                            moduleName
                        );
                    }
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
                lastError = ex.Message;
                _logger.Warning(
                    ex,
                    "JS error during initialization attempt {Attempt} for {Module}",
                    attempts,
                    moduleName
                );
            }

            // Use exponential backoff with jitter for retries
            var delay = Math.Min(RetryDelayMs * Math.Pow(1.5, Math.Min(attempts, 10) - 1), 2000);
            var jitter = Random.Shared.Next(-(int)(delay * 0.1), (int)(delay * 0.1));
            await Task.Delay((int)delay + jitter, cancellationToken);
        }

        throw new JSException(
            $"JavaScript module {moduleName} failed to initialize after {attempts} attempts. Last error: {lastError}"
        );
    }

    /// <summary>
    ///     Throws an ObjectDisposedException if this instance has been disposed.
    /// </summary>
    /// <param name="caller">The name of the calling method.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the service is disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    #endregion
}
