#region

using DropBear.Codex.Blazor.Components.Bases;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Files;

/// <summary>
///     A Blazor component for downloading files with progress indication.
/// </summary>
public sealed partial class DropBearFileDownloader : DropBearComponentBase
{
    private const int DISPOSAL_DELAY_MS = 100;

    private readonly CancellationTokenSource _downloadCancellationSource = new();
    private int _downloadProgress;
    private bool _isDownloading;

    /// <summary>
    ///     Initiates the file download process and updates progress.
    /// </summary>
    private async Task StartDownload()
    {
        if (!ValidateDownloadState())
        {
            return;
        }

        _isDownloading = true;
        _downloadProgress = 0;

        try
        {
            Logger.Information("Starting download: {FileName}", FileName);
            await ProcessDownload();
            await OnDownloadComplete.InvokeAsync(true);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or OperationCanceledException)
        {
            Logger.Warning(ex, "Download interrupted: {FileName}", FileName);
            await OnDownloadComplete.InvokeAsync(false);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Download failed: {FileName}", FileName);
            await OnDownloadComplete.InvokeAsync(false);
        }
        finally
        {
            await CleanupDownload();
        }
    }

    private bool ValidateDownloadState()
    {
        if (_isDownloading || DownloadFileAsync is null)
        {
            Logger.Warning("Invalid download state: IsDownloading={IsDownloading}, HasDelegate={HasDelegate}",
                _isDownloading, DownloadFileAsync != null);
            return false;
        }

        if (IsDisposed)
        {
            Logger.Warning("Cannot start download - component is disposed");
            return false;
        }

        return true;
    }

    private async Task ProcessDownload()
    {
        // Create progress reporter
        var progress = new Progress<int>(UpdateProgress);

        // Download the file
        await using var resultStream = await DownloadFileAsync!(progress, _downloadCancellationSource.Token);
        var streamRef = new DotNetStreamReference(resultStream);

        Logger.Debug("Initiating client-side download: {FileName}", FileName);

        await SafeJsInteropAsync<bool>(
            "DropBearFileDownloader.downloadFileFromStream",
            FileName,
            streamRef,
            ContentType
        );
    }

    private void UpdateProgress(int percent)
    {
        _downloadProgress = percent;
        Logger.Debug("Download progress: {Progress}%", percent);
        _ = InvokeAsync(StateHasChanged); // Fire and forget with discard
    }

    private async Task CleanupDownload()
    {
        _isDownloading = false;
        await InvokeStateHasChangedAsync(() =>
        {
            Logger.Debug("Download finalized: {FileName}", FileName);
            return Task.CompletedTask; // Add missing return
        });
    }

    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        try
        {
            await _downloadCancellationSource.CancelAsync();
            await Task.Delay(DISPOSAL_DELAY_MS, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during download cleanup");
        }
        finally
        {
            _downloadCancellationSource.Dispose();
        }
    }

    #region Parameters

    /// <summary>
    ///     The displayed file name (e.g., "document.pdf").
    /// </summary>
    [Parameter]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    ///     The displayed file size (e.g., "2.5 MB").
    /// </summary>
    [Parameter]
    public string FileSize { get; set; } = string.Empty;

    /// <summary>
    ///     The CSS class for the file icon (e.g., "fas fa-file-pdf").
    /// </summary>
    [Parameter]
    public string FileIconClass { get; set; } = "fas fa-file-pdf";

    /// <summary>
    ///     A delegate returning a stream for the file to be downloaded.
    /// </summary>
    [Parameter]
    public Func<IProgress<int>, CancellationToken, Task<Stream>>? DownloadFileAsync { get; set; }

    /// <summary>
    ///     An event callback invoked upon download completion.
    /// </summary>
    [Parameter]
    public EventCallback<bool> OnDownloadComplete { get; set; }

    /// <summary>
    ///     The MIME content type for the file.
    /// </summary>
    [Parameter]
    public string ContentType { get; set; } = "application/octet-stream";

    #endregion
}
