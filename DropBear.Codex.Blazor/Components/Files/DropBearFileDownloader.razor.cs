#region

using System.Buffers;
using System.Text.Json;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Errors;
using DropBear.Codex.Core.Results.Base;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Files;

/// <summary>
///     A Blazor component for downloading files with progress indication.
///     Optimized for Blazor Server with proper thread safety, resource management, and Result-based error handling.
/// </summary>
public sealed partial class DropBearFileDownloader : DropBearComponentBase
{
    #region Initialization and Component Lifecycle

    /// <summary>
    ///     Initializes the component and loads the JavaScript module.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected override async Task InitializeComponentAsync()
    {
        if (_isInitialized || IsDisposed)
        {
            return;
        }

        try
        {
            await _downloadSemaphore.WaitAsync(ComponentToken);

            _module = await GetJsModuleAsync(JsModuleName);

            // Check if module is initialized
            var isInitialized = await _module.InvokeAsync<bool>($"{JsModuleName}API.isInitialized");
            if (!isInitialized)
            {
                await _module.InvokeVoidAsync($"{JsModuleName}API.initialize", ComponentToken);

                // Verify initialization
                isInitialized = await _module.InvokeAsync<bool>($"{JsModuleName}API.isInitialized");
                if (!isInitialized)
                {
                    throw new InvalidOperationException("Failed to initialize JS module");
                }
            }

            _isInitialized = true;
            LogDebug("File downloader initialized");
        }
        catch (Exception ex)
        {
            LogError("Failed to initialize file downloader", ex);
            throw;
        }
        finally
        {
            _downloadSemaphore.Release();
        }
    }

    #endregion

    #region Cleanup and Disposal

    /// <summary>
    ///     Cleans up JavaScript resources when the component is disposed.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        try
        {
            await _userCancellationSource.CancelAsync();

            if (_isDownloading)
            {
                try
                {
                    // Give any in-progress downloads a moment to clean up
                    await Task.Delay(DisposalDelayMs, CancellationToken.None);
                }
                catch
                {
                    // Ignore cancellation during disposal
                }
            }

            if (_module != null)
            {
                await _downloadSemaphore.WaitAsync(TimeSpan.FromSeconds(5));
                try
                {
                    await _module.InvokeVoidAsync(
                        $"{JsModuleName}API.dispose",
                        new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token
                    );
                    LogDebug("File downloader resources cleaned up");
                }
                catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException)
                {
                    LogWarning("JS cleanup interrupted: {Reason}", ex.GetType().Name);
                }
                catch (Exception ex)
                {
                    LogError("Failed to cleanup JS module", ex);
                }
                finally
                {
                    _downloadSemaphore.Release();
                }
            }
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException)
        {
            LogWarning("Cleanup interrupted: {Reason}", ex.GetType().Name);
        }
        catch (Exception ex)
        {
            LogError("Failed to cleanup file downloader", ex);
        }
        finally
        {
            try
            {
                _userCancellationSource.Dispose();
                _downloadSemaphore.Dispose();
            }
            catch (ObjectDisposedException) { }

            _module = null;
            _isInitialized = false;
            _isDownloading = false;
        }
    }

    #endregion

    #region Constants

    /// <summary>
    ///     Delay in milliseconds before completing disposal to ensure pending operations complete.
    /// </summary>
    private const int DisposalDelayMs = 100;

    /// <summary>
    ///     Timeout in milliseconds for JavaScript operations.
    /// </summary>
    private const int JsOperationTimeoutMs = 30000;

    /// <summary>
    ///     Default buffer size for file streaming in bytes (80KB).
    /// </summary>
    private const int DefaultBufferSize = 81920;

    /// <summary>
    ///     Maximum number of retry attempts for download operations.
    /// </summary>
    private const int MaxRetryAttempts = 3;

    /// <summary>
    ///     Base delay in milliseconds for retry operations.
    /// </summary>
    private const int RetryDelayMs = 1000;

    /// <summary>
    ///     Name of the JavaScript module used for file downloads.
    /// </summary>
    private const string JsModuleName = JsModuleNames.FileDownloader;

    #endregion

    #region Private Fields

    /// <summary>
    ///     Thread synchronization primitive to control access to download operations.
    /// </summary>
    private readonly SemaphoreSlim _downloadSemaphore = new(1, 1);

    /// <summary>
    ///     Cancellation token source for user-initiated cancellations.
    /// </summary>
    private readonly CancellationTokenSource _userCancellationSource = new();

    /// <summary>
    ///     Current download progress as a percentage (0-100).
    /// </summary>
    private volatile int _downloadProgress;

    /// <summary>
    ///     Flag indicating whether a download is currently in progress.
    /// </summary>
    private volatile bool _isDownloading;

    /// <summary>
    ///     Flag indicating whether the component has been initialized.
    /// </summary>
    private volatile bool _isInitialized;

    /// <summary>
    ///     Reference to the JavaScript module for file downloads.
    /// </summary>
    private IJSObjectReference? _module;

    /// <summary>
    ///     Object pool for reusing byte array buffers.
    /// </summary>
    private static readonly ArrayPool<byte> BufferPool = ArrayPool<byte>.Shared;

    /// <summary>
    ///     Date and time when the current download started.
    /// </summary>
    private DateTime _downloadStartTime;

    #endregion

    #region Parameters

    /// <summary>
    ///     Gets or sets the name of the file to download.
    /// </summary>
    [Parameter]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the size of the file displayed to the user (e.g., "1.2 MB").
    /// </summary>
    [Parameter]
    public string FileSize { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the CSS class for the file icon.
    /// </summary>
    [Parameter]
    public string FileIconClass { get; set; } = "fas fa-file-pdf";

    /// <summary>
    ///     Gets or sets the MIME type of the file being downloaded.
    /// </summary>
    [Parameter]
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>
    ///     Gets or sets the function that retrieves the file data stream.
    ///     This delegate should return a Stream containing the file data.
    /// </summary>
    [Parameter]
    public Func<IProgress<int>, CancellationToken, Task<Stream>>? DownloadFileAsync { get; set; }

    /// <summary>
    ///     Gets or sets the event callback that is invoked when the download completes.
    ///     The boolean parameter indicates whether the download was successful.
    /// </summary>
    [Parameter]
    public EventCallback<bool> OnDownloadComplete { get; set; }

    /// <summary>
    ///     Gets or sets the buffer size for streaming file data in bytes.
    ///     Larger values can improve performance but use more memory.
    /// </summary>
    [Parameter]
    public int BufferSize { get; set; } = DefaultBufferSize;

    /// <summary>
    ///     Gets or sets whether to automatically retry failed downloads.
    /// </summary>
    [Parameter]
    public bool AutoRetry { get; set; } = true;

    #endregion

    #region Public Methods

    /// <summary>
    ///     Starts the file download process, handling errors and retries automatically.
    /// </summary>
    /// <returns>A task that represents the asynchronous download operation.</returns>
    public async Task StartDownload()
    {
        if (!await ValidateDownloadStateAsync())
        {
            return;
        }

        try
        {
            await _downloadSemaphore.WaitAsync(ComponentToken);

            await QueueStateHasChangedAsync(() =>
            {
                _isDownloading = true;
                _downloadProgress = 0;
                _downloadStartTime = DateTime.UtcNow;
            });

            LogDebug("Starting download: {FileName}", FileName);

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                ComponentToken,
                _userCancellationSource.Token
            );
            linkedCts.Token.ThrowIfCancellationRequested();

            await ProcessDownload(linkedCts.Token);
            await NotifyDownloadComplete(true);
        }
        catch (OperationCanceledException ex)
        {
            LogWarning("Download cancelled: {Reason}", ex.Message);
            await NotifyDownloadComplete(false);
        }
        catch (JSDisconnectedException ex)
        {
            LogWarning("Download interrupted due to connection loss: {Reason}", ex.Message);
            await NotifyDownloadComplete(false);
        }
        catch (Exception ex)
        {
            LogError("Download failed: {FileName}", ex, FileName);
            await NotifyDownloadComplete(false);
        }
        finally
        {
            await CleanupDownload();
            _downloadSemaphore.Release();
        }
    }

    /// <summary>
    ///     Tries to download the file and returns a Result object indicating success or failure.
    ///     This method allows for more detailed error handling using the Result pattern.
    /// </summary>
    /// <returns>
    ///     A Result indicating success or failure, with detailed error information on failure.
    /// </returns>
    public async Task<Result<bool, FileDownloadError>> TryDownloadAsync()
    {
        try
        {
            if (!await ValidateDownloadStateAsync())
            {
                return Result<bool, FileDownloadError>.Failure(
                    FileDownloadError.InvalidState("Component is not in a valid state for downloading"));
            }

            await _downloadSemaphore.WaitAsync(ComponentToken);

            try
            {
                // Set UI state
                await QueueStateHasChangedAsync(() =>
                {
                    _isDownloading = true;
                    _downloadProgress = 0;
                    _downloadStartTime = DateTime.UtcNow;
                });

                LogDebug("Starting download with result pattern: {FileName}", FileName);

                // Create linked token
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    ComponentToken, _userCancellationSource.Token);

                // Process download with robust error handling
                try
                {
                    await ProcessDownload(linkedCts.Token);
                    await NotifyDownloadComplete(true);

                    // Calculate download elapsed time
                    var elapsed = DateTime.UtcNow - _downloadStartTime;
                    LogDebug("Download completed successfully in {ElapsedMs}ms: {FileName}",
                        elapsed.TotalMilliseconds, FileName);

                    return Result<bool, FileDownloadError>.Success(true);
                }
                catch (OperationCanceledException)
                {
                    return Result<bool, FileDownloadError>.Cancelled(
                        FileDownloadError.Cancelled());
                }
                catch (JSDisconnectedException)
                {
                    return Result<bool, FileDownloadError>.Failure(
                        FileDownloadError.NetworkFailure("Browser connection was lost during download"));
                }
                catch (JSException ex)
                {
                    return Result<bool, FileDownloadError>.Failure(
                        FileDownloadError.JavaScriptError(ex.Message));
                }
                catch (Exception ex)
                {
                    return Result<bool, FileDownloadError>.Failure(
                        FileDownloadError.UploadFailed(FileName, ex.Message));
                }
            }
            finally
            {
                await CleanupDownload();
                _downloadSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            LogError("Critical error in download operation", ex);
            return Result<bool, FileDownloadError>.Failure(
                FileDownloadError.UploadFailed(FileName, $"Critical error: {ex.Message}"));
        }
    }

    /// <summary>
    ///     Cancels any ongoing download operation.
    /// </summary>
    public void CancelDownload()
    {
        if (!_isDownloading)
        {
            return;
        }

        LogDebug("Canceling download: {FileName}", FileName);
        _userCancellationSource.Cancel();
    }

    #endregion

    #region Private Helper Methods

    /// <summary>
    ///     Processes the file download with robust error handling and retry logic.
    /// </summary>
    /// <param name="token">Cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous download operation.</returns>
    private async Task ProcessDownload(CancellationToken token)
    {
        if (DownloadFileAsync == null || _module == null)
        {
            throw new InvalidOperationException("Download not properly configured");
        }

        // Create a progress object that safely updates UI
        var progress = new Progress<int>(async void (percent) =>
        {
            try
            {
                await QueueStateHasChangedAsync(() => _downloadProgress = percent);
                LogDebug("Download progress: {Progress}%", percent);
            }
            catch (Exception e)
            {
                LogError("Failed to update download progress", e);
            }
        });

        // Get the file stream
        await using var resultStream = await DownloadFileAsync(progress, token);

        // Rent a buffer from the pool and create a buffered stream
        byte[] buffer = BufferPool.Rent(BufferSize);
        try
        {
            // Use optimized buffering for large streams with the pooled buffer
            await using var bufferedStream = new BufferedStream(
                resultStream,
                buffer.Length);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeoutCts.CancelAfter(JsOperationTimeoutMs);

            // Create stream reference and invoke JS download
            using var streamRef = new DotNetStreamReference(bufferedStream);

            if (AutoRetry)
            {
                await RetryDownloadAsync(() => PerformJsDownload(streamRef, timeoutCts.Token), MaxRetryAttempts);
            }
            else
            {
                await PerformJsDownload(streamRef, timeoutCts.Token);
            }
        }
        finally
        {
            // Return the buffer to the pool when done
            BufferPool.Return(buffer);
        }
    }

    /// <summary>
    ///     Performs the actual JavaScript download operation.
    /// </summary>
    /// <param name="streamRef">Stream reference containing the file data.</param>
    /// <param name="token">Cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous download operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the JavaScript download fails.</exception>
    private async Task PerformJsDownload(DotNetStreamReference streamRef, CancellationToken token)
    {
        // Use the helper method instead of directly referencing _module
        var module = await GetJsDownloaderModuleAsync();

        try
        {
            var success = await module.InvokeAsync<bool>(
                $"{JsModuleName}API.downloadFileFromStream",
                token,
                FileName,
                streamRef,
                ContentType
            );

            if (!success)
            {
                LogWarning("JS download returned false");
                throw new InvalidOperationException("JavaScript download failed");
            }
        }
        catch (JSException ex) when (ex.InnerException is JsonException)
        {
            LogError("JS download failed with JSON conversion error", ex);
            throw new InvalidOperationException("JavaScript download failed due to invalid response", ex);
        }
        catch (JSDisconnectedException ex)
        {
            LogError("JS download failed due to disconnect", ex);
            throw new OperationCanceledException("Browser disconnected during download", ex);
        }
        catch (TaskCanceledException ex)
        {
            LogWarning("JS download timed out after {Timeout}ms", JsOperationTimeoutMs);
            throw new TimeoutException($"Download operation timed out after {JsOperationTimeoutMs}ms", ex);
        }
        catch (Exception ex)
        {
            LogError("JS download failed with unexpected error", ex);
            throw;
        }
    }

    /// <summary>
    ///     Retries a download operation with exponential backoff.
    /// </summary>
    /// <param name="downloadAction">The download action to retry.</param>
    /// <param name="maxRetries">Maximum number of retry attempts.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="Exception">Rethrows the last exception if all retries fail.</exception>
    private async Task RetryDownloadAsync(Func<Task> downloadAction, int maxRetries)
    {
        var retryDelay = RetryDelayMs;
        Exception? lastException = null;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                if (attempt > 0)
                {
                    LogWarning("Retry attempt {Attempt} of {MaxRetries}", attempt, maxRetries);
                }

                await downloadAction();
                return; // Success, exit the retry loop
            }
            catch (OperationCanceledException)
            {
                // Don't retry cancellations
                throw;
            }
            catch (Exception ex)
            {
                if (attempt >= maxRetries)
                {
                    LogError("Download failed after {Attempts} attempts", ex, maxRetries + 1);
                    throw; // Rethrow the last exception
                }

                lastException = ex;
                LogWarning("Download attempt {Attempt} failed: {Error}, retrying in {Delay}ms",
                    attempt + 1, ex.Message, retryDelay);

                // Wait with exponential backoff plus jitter
                var jitter = Random.Shared.Next(-100, 100);
                await Task.Delay(retryDelay + jitter, ComponentToken);

                // Exponential backoff for next attempt
                retryDelay *= 2;
            }
        }

        // This should not be reached, but just in case
        throw lastException ?? new InvalidOperationException("Download failed after retries");
    }

    /// <summary>
    ///     Validates the current component state for download.
    /// </summary>
    /// <returns>True if the component is ready for download; otherwise, false.</returns>
    private async Task<bool> ValidateDownloadStateAsync()
    {
        if (!_isInitialized)
        {
            LogWarning("Cannot start download - component not initialized");
            try
            {
                await InitializeComponentAsync();
                return _isInitialized;
            }
            catch
            {
                return false;
            }
        }

        if (_isDownloading)
        {
            LogWarning("Download already in progress");
            return false;
        }

        if (DownloadFileAsync == null)
        {
            LogWarning("No download delegate provided");
            return false;
        }

        if (IsDisposed)
        {
            LogWarning("Cannot start download - component is disposed");
            return false;
        }

        if (_module == null)
        {
            try
            {
                await InitializeComponentAsync();
                return _module != null;
            }
            catch
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Notifies completion of a download operation.
    /// </summary>
    /// <param name="success">Indicates whether the download completed successfully.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task NotifyDownloadComplete(bool success)
    {
        if (OnDownloadComplete.HasDelegate)
        {
            try
            {
                await OnDownloadComplete.InvokeAsync(success);
            }
            catch (Exception ex)
            {
                LogError("Error in download completion callback", ex);
            }
        }
    }

    /// <summary>
    ///     Cleans up resources after a download operation.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task CleanupDownload()
    {
        await QueueStateHasChangedAsync(() =>
        {
            _isDownloading = false;
            _downloadProgress = 0;
        });
        LogDebug("Download finalized: {FileName}", FileName);
    }

    /// <summary>
    ///     Gets the JavaScript module instance, creating it if needed.
    /// </summary>
    /// <returns>A ValueTask containing the JavaScript module reference.</returns>
    private ValueTask<IJSObjectReference> GetJsDownloaderModuleAsync()
    {
        return _module != null
            ? new ValueTask<IJSObjectReference>(_module)
            : new ValueTask<IJSObjectReference>(GetJsModuleAsync(JsModuleName));
    }

    #endregion
}
