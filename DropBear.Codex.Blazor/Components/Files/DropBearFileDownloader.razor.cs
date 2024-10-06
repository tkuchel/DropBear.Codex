#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Files;

/// <summary>
///     A Blazor component for downloading files with progress indication.
/// </summary>
public partial class DropBearFileDownloader : DropBearComponentBase, IDisposable
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearFileDownloader>();
    private CancellationTokenSource? _dismissCancellationTokenSource;
    private int _downloadProgress;
    private bool _isDownloading;

    [Parameter] public string FileName { get; set; } = string.Empty;
    [Parameter] public string FileSize { get; set; } = string.Empty;
    [Parameter] public string FileIconClass { get; set; } = "fas fa-file-pdf";
    [Parameter] public Func<IProgress<int>, CancellationToken, Task<Stream>>? DownloadFileAsync { get; set; }
    [Parameter] public EventCallback<bool> OnDownloadComplete { get; set; }
    [Parameter] public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>
    ///     Clean up resources on disposal.
    /// </summary>
    public void Dispose()
    {
        _dismissCancellationTokenSource?.Cancel();
        _dismissCancellationTokenSource?.Dispose();
        _dismissCancellationTokenSource = null;
    }

    /// <summary>
    ///     Starts the file download process and monitors progress.
    /// </summary>
    private async Task StartDownload()
    {
        if (_isDownloading || DownloadFileAsync is null)
        {
            Logger.Warning(
                "Download attempt skipped because a download is already in progress or DownloadFileAsync is null.");
            return;
        }

        _dismissCancellationTokenSource = new CancellationTokenSource();
        _isDownloading = true;
        _downloadProgress = 0;

        try
        {
            Logger.Debug("Starting download for file: {FileName}", FileName);

            // Set up progress reporting
            var progress = new Progress<int>(percent =>
            {
                _downloadProgress = percent;
                Logger.Debug("Download progress updated: {Progress}%", percent);
                InvokeAsync(StateHasChanged);
            });

            // Call the download function, passing progress and cancellation token
            using var resultStream = await DownloadFileAsync(progress, _dismissCancellationTokenSource.Token);

            // Use JS interop to trigger the download using a stream reference
            var streamRef = new DotNetStreamReference(resultStream);

            Logger.Debug("Download completed for file: {FileName}, preparing to save file on client.", FileName);

            await JsRuntime.InvokeVoidAsync(
                "downloadFileFromStream",
                _dismissCancellationTokenSource.Token,
                FileName,
                streamRef,
                ContentType);

            // Notify completion with success
            await OnDownloadComplete.InvokeAsync(true);
        }
        catch (OperationCanceledException)
        {
            Logger.Warning("Download canceled for file: {FileName}", FileName);
            await OnDownloadComplete.InvokeAsync(false); // Notify cancellation as failure
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred during file download for file: {FileName}", FileName);
            await OnDownloadComplete.InvokeAsync(false); // Notify failure
        }
        finally
        {
            _isDownloading = false;
            _dismissCancellationTokenSource?.Dispose();
            _dismissCancellationTokenSource = null;
            Logger.Debug("Download process finalized for file: {FileName}", FileName);
            InvokeAsync(StateHasChanged);
        }
    }
}
