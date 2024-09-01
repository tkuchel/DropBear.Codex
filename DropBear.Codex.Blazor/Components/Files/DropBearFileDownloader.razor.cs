#region

using DropBear.Codex.Blazor.Components.Bases;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Files;

/// <summary>
///     A Blazor component for downloading files with progress indication.
/// </summary>
public sealed partial class DropBearFileDownloader : DropBearComponentBase, IDisposable
{
    private CancellationTokenSource? _dismissCancellationTokenSource;
    private int _downloadProgress;
    private bool _isDownloading;

    [Parameter] public string FileName { get; set; } = string.Empty;
    [Parameter] public string FileSize { get; set; } = string.Empty;
    [Parameter] public string FileIconClass { get; set; } = "fas fa-file-pdf";
    [Parameter] public Func<IProgress<int>, Task<MemoryStream>>? DownloadFileAsync { get; set; }
    [Parameter] public EventCallback<bool> OnDownloadComplete { get; set; }
    [Parameter] public string ContentType { get; set; } = "application/octet-stream";

    public void Dispose()
    {
        _dismissCancellationTokenSource?.Dispose();
    }

    /// <summary>
    ///     Starts the file download process.
    /// </summary>
    private async Task StartDownload()
    {
        _dismissCancellationTokenSource = new CancellationTokenSource();

        if (_isDownloading || DownloadFileAsync is null)
        {
            return;
        }

        _isDownloading = true;
        _downloadProgress = 0;

        try
        {
            var progress = new Progress<int>(percent =>
            {
                _downloadProgress = percent;
                StateHasChanged();
            });

            var resultStream = await DownloadFileAsync(progress);

            resultStream.Position = 0;
            var byteArray = resultStream.ToArray();

            // Use the ContentType parameter
            await JsRuntime.InvokeVoidAsync("downloadFileFromStream", _dismissCancellationTokenSource.Token, FileName,
                byteArray, ContentType);

            await OnDownloadComplete.InvokeAsync(true);
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Download failed: {ex.Message}");
            await OnDownloadComplete.InvokeAsync(false);
        }
        finally
        {
            _isDownloading = false;
            StateHasChanged();
        }
    }
}
