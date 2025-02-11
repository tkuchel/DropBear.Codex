#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Files;

/// <summary>
///     A Blazor component for downloading files with progress indication.
///     Optimized for Blazor Server with proper thread safety and resource management.
/// </summary>
public sealed partial class DropBearFileDownloader : DropBearComponentBase
{
    private const string JsModuleName = JsModuleNames.FileDownloader;
    private const int DisposalDelayMs = 100;
    private const int JsOperationTimeoutMs = 30000;

    private readonly SemaphoreSlim _downloadSemaphore = new(1, 1);
    private readonly CancellationTokenSource _userCancellationSource = new();
    private volatile int _downloadProgress;
    private volatile bool _isDownloading;
    private volatile bool _isInitialized;
    private IJSObjectReference? _module;

    [Parameter] public string FileName { get; set; } = string.Empty;
    [Parameter] public string FileSize { get; set; } = string.Empty;
    [Parameter] public string FileIconClass { get; set; } = "fas fa-file-pdf";
    [Parameter] public string ContentType { get; set; } = "application/octet-stream";
    [Parameter] public Func<IProgress<int>, CancellationToken, Task<Stream>>? DownloadFileAsync { get; set; }
    [Parameter] public EventCallback<bool> OnDownloadComplete { get; set; }

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
            await _module.InvokeVoidAsync($"{JsModuleName}API.initialize", ComponentToken);

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

    private async Task StartDownload()
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
        catch (Exception ex) when (ex is OperationCanceledException or JSDisconnectedException)
        {
            LogWarning("Download interrupted: {Reason}", ex.GetType().Name);
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

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeoutCts.CancelAfter(JsOperationTimeoutMs);

        // Create stream reference and invoke JS download
        using var streamRef = new DotNetStreamReference(resultStream);
        var success = await _module.InvokeAsync<bool>(
            $"{JsModuleName}API.downloadFileFromStream",
            timeoutCts.Token,
            FileName,
            streamRef,
            ContentType
        );

        if (!success)
        {
            throw new InvalidOperationException("JS download failed");
        }
    }

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

    private async Task CleanupDownload()
    {
        await QueueStateHasChangedAsync(() =>
        {
            _isDownloading = false;
            _downloadProgress = 0;
        });
        LogDebug("Download finalized: {FileName}", FileName);
    }

    private async Task<bool> ValidateDownloadStateAsync()
    {
        if (!_isInitialized)
        {
            LogWarning("Cannot start download - component not initialized");
            return false;
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

    private void CancelDownload()
    {
        if (!_isDownloading)
        {
            return;
        }

        LogDebug("Canceling download: {FileName}", FileName);
        _userCancellationSource.Cancel();
    }

    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        try
        {
            _userCancellationSource.Cancel();

            if (_isDownloading)
            {
                await Task.Delay(DisposalDelayMs, CancellationToken.None);
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
}
