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
public sealed partial class DropBearFileDownloader : DropBearComponentBase, IAsyncDisposable
{
    private new static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearFileDownloader>();

    private CancellationTokenSource? _dismissCancellationTokenSource;
    private int _downloadProgress;
    private bool _isDownloading;

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
    ///     The <see cref="IProgress{T}" /> reports the download progress (0..100).
    /// </summary>
    [Parameter]
    public Func<IProgress<int>, CancellationToken, Task<Stream>>? DownloadFileAsync { get; set; }

    /// <summary>
    ///     An event callback invoked upon download completion (true if success, false otherwise).
    /// </summary>
    [Parameter]
    public EventCallback<bool> OnDownloadComplete { get; set; }

    /// <summary>
    ///     The MIME content type used when saving the file on the client.
    /// </summary>
    [Parameter]
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>
    ///     Asynchronously disposes component resources, including cancellation tokens.
    /// </summary>
    public new async ValueTask DisposeAsync()
    {
        if (_dismissCancellationTokenSource != null)
        {
            await _dismissCancellationTokenSource.CancelAsync();
            try
            {
                // Brief wait to let pending tasks wrap up.
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during asynchronous disposal of DropBearFileDownloader.");
            }
            finally
            {
                _dismissCancellationTokenSource.Dispose();
                _dismissCancellationTokenSource = null;
            }
        }

        await base.DisposeAsync();
        // GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Initiates the file download process and updates progress.
    /// </summary>
    private async Task StartDownload()
    {
        if (_isDownloading || DownloadFileAsync is null)
        {
            Logger.Warning("Download attempt skipped: already downloading or DownloadFileAsync is null.");
            return;
        }

        if (!IsConnected)
        {
            Logger.Warning("Cannot start download; circuit is disconnected.");
            return;
        }

        _dismissCancellationTokenSource = new CancellationTokenSource();
        _isDownloading = true;
        _downloadProgress = 0;

        try
        {
            Logger.Information("Starting download for file: {FileName}", FileName);

            // Create progress reporter
            var progress = new Progress<int>(percent =>
            {
                _downloadProgress = percent;
                Logger.Debug("Download progress: {Progress}%", percent);
                InvokeAsync(StateHasChanged);
            });

            // Download the file via the provided delegate
            await using var resultStream = await DownloadFileAsync(progress, _dismissCancellationTokenSource.Token);

            // Create DotNetStreamReference for JS side
            var streamRef = new DotNetStreamReference(resultStream);

            Logger.Debug("Download completed. Saving file on client: {FileName}", FileName);

            await SafeJsVoidInteropAsync(
                "DropBearFileDownloader.downloadFileFromStream",
                FileName,
                streamRef,
                ContentType
            );

            // Fire the completion event with success = true
            await OnDownloadComplete.InvokeAsync(true);
        }
        catch (JSDisconnectedException)
        {
            Logger.Warning("JSInterop call failed: circuit disconnected during download of {FileName}", FileName);
            await OnDownloadComplete.InvokeAsync(false);
        }
        catch (OperationCanceledException)
        {
            Logger.Warning("Download canceled: {FileName}", FileName);
            await OnDownloadComplete.InvokeAsync(false);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during file download: {FileName}", FileName);
            await OnDownloadComplete.InvokeAsync(false);
        }
        finally
        {
            _isDownloading = false;
            _dismissCancellationTokenSource?.Dispose();
            _dismissCancellationTokenSource = null;
            Logger.Debug("Download process finalized: {FileName}", FileName);
            await InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>
    ///     Override of base method for cleaning up JS resources.
    ///     Returns a completed task here as there are no additional JS resources to release.
    /// </summary>
    protected override Task CleanupJavaScriptResourcesAsync()
    {
        return Task.CompletedTask;
    }
}
