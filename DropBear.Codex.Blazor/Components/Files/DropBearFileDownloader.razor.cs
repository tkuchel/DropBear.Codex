#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Files;

/// <summary>
///     A Blazor component for downloading files with progress indication,
///     refactored to support user cancellation and use DropBearComponentBase
///     for JS interop and disposal.
/// </summary>
public sealed partial class DropBearFileDownloader : DropBearComponentBase
{
    // A small delay (optional) to allow any leftover JS calls to finish
    private const int DISPOSAL_DELAY_MS = 100;

    // A local CTS used for user-driven cancellation (e.g., a "Cancel" button).
    // We combine it with the base class’s token so the operation also ends if
    // the circuit is torn down or the component is disposed.
    private readonly CancellationTokenSource _userCancellationSource = new();
    private int _downloadProgress;
    private bool _isDownloading;

    private IJSObjectReference? _module;
    private const string JsModuleName = JsModuleNames.FileDownloader;

    #region Lifecycle

    /// <summary>
    ///     Called after the first render to load and optionally initialize the JS module.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (!firstRender || IsDisposed)
        {
            return;
        }

        try
        {
            // Load/cache the "file-downloader" module
            _module = await GetJsModuleAsync(JsModuleName).ConfigureAwait(false);
            // Optionally call an initialization function within that module
            await _module.InvokeVoidAsync($"{JsModuleName}API.initialize").ConfigureAwait(false);

            LogDebug("DropBearFileDownloader JS module initialized.");
        }
        catch (Exception ex)
        {
            LogError("Error initializing DropBearFileDownloader module.", ex);
            throw;
        }
    }

    /// <summary>
    ///     Called by the base class during disposal to clean up JS resources and local tokens.
    /// </summary>
    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        try
        {
            // Cancel any user-initiated downloads to prevent hanging tasks
            await _userCancellationSource.CancelAsync();

            // Optional short delay to allow in-flight tasks or JS calls to settle
            await Task.Delay(DISPOSAL_DELAY_MS).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogError("Error during JS cleanup for DropBearFileDownloader.", ex);
        }
        finally
        {
            _userCancellationSource.Dispose();
            _module = null; // The base class also disposes the JS module reference from its cache
        }
    }

    #endregion

    #region Download Logic

    /// <summary>
    ///     Initiates the file download process.
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

            // Wrap the component’s token + user token in a linked token:
            // so the download stops if EITHER the component is disposed OR the user cancels.
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                ComponentToken, _userCancellationSource.Token
            );
            var downloadToken = linkedCts.Token;

            // Perform the download
            await ProcessDownload(downloadToken).ConfigureAwait(false);
            await OnDownloadComplete.InvokeAsync(true).ConfigureAwait(false);
        }
        catch (JSDisconnectedException jsEx)
        {
            LogWarning("Download interrupted (JS disconnected): {FileName}", jsEx, FileName);
            await OnDownloadComplete.InvokeAsync(false).ConfigureAwait(false);
        }
        catch (OperationCanceledException ocEx)
        {
            LogWarning("Download canceled: {FileName}", ocEx, FileName);
            await OnDownloadComplete.InvokeAsync(false).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogError("Download failed: {FileName}", ex, FileName);
            await OnDownloadComplete.InvokeAsync(false).ConfigureAwait(false);
        }
        finally
        {
            await CleanupDownload().ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Cancel the active download by canceling our local token.
    /// </summary>
    private void CancelDownload()
    {
        if (!_isDownloading)
        {
            return;
        }

        LogDebug("User cancel requested for file: {FileName}", FileName);
        _userCancellationSource.Cancel();
    }

    /// <summary>
    ///     Actual download logic: fetches the stream, calls JS to prompt user save.
    /// </summary>
    private async Task ProcessDownload(CancellationToken token)
    {
        // 1) Retrieve the file stream from the user-provided delegate with progress
        if (DownloadFileAsync is null)
        {
            throw new InvalidOperationException("No download delegate provided.");
        }

        var progress = new Progress<int>(UpdateProgress);
        await using var resultStream = await DownloadFileAsync(progress, token).ConfigureAwait(false);

        // 2) Convert the result stream into a DotNetStreamReference for JS to handle
        var streamRef = new DotNetStreamReference(resultStream);

        // 3) Call into the JS module to save the file
        LogDebug("Initiating client-side download: {FileName}", FileName);

        var result = _module?.InvokeAsync<bool>(
            "DropBearFileDownloader.downloadFileFromStream", token, FileName,
            streamRef,
            ContentType
        ).ConfigureAwait(false);

        if (result is null)
        {
            throw new InvalidOperationException("JS module not available.");
        }
    }

    /// <summary>
    ///     Handle progress updates. Not thread-safe by default, but typically safe for a single download.
    /// </summary>
    private void UpdateProgress(int percent)
    {
        _downloadProgress = percent;
        LogDebug("Download progress: {Progress}%", percent);
        _ = InvokeAsync(StateHasChanged); // Fire & forget
    }

    /// <summary>
    ///     Finalize the download, reset flags, re-render.
    /// </summary>
    private async Task CleanupDownload()
    {
        _isDownloading = false;

        // Optionally force a final render
        await InvokeStateHasChangedAsync(() =>
        {
            LogDebug("Download finalized: {FileName}", FileName);
            return Task.CompletedTask;
        });
    }

    /// <summary>
    ///     Confirm that we can start a download.
    /// </summary>
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

    #endregion

    #region Parameters

    /// <summary>
    ///     Displayed filename (e.g., "document.pdf").
    /// </summary>
    [Parameter]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    ///     Displayed file size (e.g., "2.5 MB").
    /// </summary>
    [Parameter]
    public string FileSize { get; set; } = string.Empty;

    /// <summary>
    ///     CSS class for the file icon (e.g., "fas fa-file-pdf").
    /// </summary>
    [Parameter]
    public string FileIconClass { get; set; } = "fas fa-file-pdf";

    /// <summary>
    ///     Delegate returning a stream for the file to be downloaded, plus progress reporting and cancellation.
    /// </summary>
    [Parameter]
    public Func<IProgress<int>, CancellationToken, Task<Stream>>? DownloadFileAsync { get; set; }

    /// <summary>
    ///     Event callback invoked upon download completion (true=success, false=failed/cancelled).
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
