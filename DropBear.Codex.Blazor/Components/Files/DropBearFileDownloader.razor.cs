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

    [Parameter] public string FileName { get; set; } = string.Empty;

    [Parameter] public string FileSize { get; set; } = string.Empty;

    [Parameter] public string FileIconClass { get; set; } = "fas fa-file-pdf";

    [Parameter] public Func<IProgress<int>, CancellationToken, Task<Stream>>? DownloadFileAsync { get; set; }

    [Parameter] public EventCallback<bool> OnDownloadComplete { get; set; }

    [Parameter] public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>
    ///     Asynchronously disposes of the component's resources.
    /// </summary>
    public new async ValueTask DisposeAsync()
    {
        if (_dismissCancellationTokenSource != null)
        {
            await _dismissCancellationTokenSource.CancelAsync();

            try
            {
                // Wait briefly to allow any pending operations to complete
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during asynchronous disposal of DropBearFileDownloader");
            }
            finally
            {
                _dismissCancellationTokenSource.Dispose();
                _dismissCancellationTokenSource = null;
            }
        }

        await base.DisposeAsync();
        GC.SuppressFinalize(this);
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

            // Set up progress reporting
            var progress = new Progress<int>(percent =>
            {
                _downloadProgress = percent;
                Logger.Debug("Download progress updated: {Progress}%", percent);
                InvokeAsync(StateHasChanged);
            });

            // Call the download function, passing progress and cancellation token
            await using var resultStream = await DownloadFileAsync(progress, _dismissCancellationTokenSource.Token);

            // Use JS interop to trigger the download using a stream reference
            var streamRef = new DotNetStreamReference(resultStream);

            Logger.Debug("Download completed for file: {FileName}, preparing to save file on client.", FileName);

            await SafeJsVoidInteropAsync(
                "DropBearFileDownloader.downloadFileFromStream",
                FileName,
                streamRef,
                ContentType);

            // Notify completion with success
            await OnDownloadComplete.InvokeAsync(true);
        }
        catch (JSDisconnectedException)
        {
            Logger.Warning("JSInterop call failed due to disconnected circuit during download of file: {FileName}",
                FileName);
            await OnDownloadComplete.InvokeAsync(false); // Notify failure
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
            await InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>
    ///     Overrides the base class method to clean up any JavaScript resources if necessary.
    /// </summary>
    protected override Task CleanupJavaScriptResourcesAsync()
    {
        // No additional JS resources to clean up for this component
        return Task.CompletedTask;
    }
}
