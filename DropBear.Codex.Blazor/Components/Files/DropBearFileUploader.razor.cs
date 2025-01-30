#region

using System.Collections.Concurrent;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Files;

/// <summary>
///     A Blazor component for handling file uploads with drag-and-drop support and progress tracking.
/// </summary>
public sealed partial class DropBearFileUploader : DropBearComponentBase
{
    private const string MODULE_NAME = "file-reader-helpers";
    private const long BYTES_PER_MB = 1024 * 1024;

    private readonly List<UploadFile> _selectedFiles = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _uploadCancellationTokens = new();
    private readonly List<UploadFile> _uploadedFiles = new();

    private bool _isDragOver;
    private bool _isUploading;
    private int _uploadProgress;

    /// <summary>
    ///     Gets a value indicating whether file upload is currently in progress.
    /// </summary>
    private bool IsUploading
    {
        get => _isUploading;
        set
        {
            if (_isUploading != value)
            {
                _isUploading = value;
                InvokeStateHasChanged(() => { });
            }
        }
    }

    /// <summary>
    ///     Gets the current upload progress as a percentage.
    /// </summary>
    private int UploadProgress
    {
        get => _uploadProgress;
        set
        {
            if (_uploadProgress != value)
            {
                _uploadProgress = value;
                InvokeStateHasChanged(() => { });
            }
        }
    }

    /// <summary>
    ///     Gets a read-only view of currently selected files.
    /// </summary>
    private IReadOnlyList<UploadFile> SelectedFiles => _selectedFiles;

    /// <summary>
    ///     Gets a read-only view of successfully uploaded files.
    /// </summary>
    private IReadOnlyList<UploadFile> UploadedFiles => _uploadedFiles;

    /// <summary>
    ///     Gets a value indicating whether files can be uploaded.
    /// </summary>
    private bool CanUpload => SelectedFiles.Any() && !IsUploading && !IsDisposed && UploadFileAsync != null;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                await EnsureJsModuleInitializedAsync(MODULE_NAME);
                Logger.Debug("File uploader initialized: {ComponentId}", ComponentId);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize file uploader: {ComponentId}", ComponentId);
            }
        }
    }

    protected override async ValueTask DisposeCoreAsync()
    {
        try
        {
            foreach (var cts in _uploadCancellationTokens.Values)
            {
                await cts.CancelAsync();
                cts.Dispose();
            }

            _uploadCancellationTokens.Clear();
        }
        finally
        {
            await base.DisposeCoreAsync();
        }
    }

    private void HandleDragEnter()
    {
        if (!IsUploading && !IsDisposed)
        {
            _isDragOver = true;
            StateHasChanged();
        }
    }

    private void HandleDragLeave()
    {
        if (!IsUploading && !IsDisposed)
        {
            _isDragOver = false;
            StateHasChanged();
        }
    }

    private async Task HandleDrop(DragEventArgs e)
    {
        try
        {
            _isDragOver = false;
            if (IsUploading || IsDisposed)
            {
                return;
            }

            var jsModule = await GetJsModuleAsync(MODULE_NAME);
            var jsFiles = await jsModule.InvokeAsync<IJSObjectReference[]>(
                "getDroppedFiles",
                ComponentToken,
                e.DataTransfer);

            var browserFiles = new List<IBrowserFile>();
            foreach (var jsFile in jsFiles)
            {
                try
                {
                    var proxy = await BrowserFileProxy.CreateAsync(jsFile);
                    browserFiles.Add(proxy);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to create file proxy for dropped file");
                    await jsFile.DisposeAsync();
                }
            }

            await ProcessSelectedFiles(browserFiles);
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling dropped files");
        }
    }

    private async Task HandleFileSelectionAsync(InputFileChangeEventArgs e)
    {
        try
        {
            if (IsUploading || IsDisposed)
            {
                return;
            }

            await ProcessSelectedFiles(e.GetMultipleFiles());
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error processing selected files");
        }
    }

    private async Task ProcessSelectedFiles(IReadOnlyList<IBrowserFile> files)
    {
        foreach (var file in files)
        {
            if (ValidateFile(file))
            {
                var uploadFile = new UploadFile(
                    file.Name,
                    file.Size,
                    file.ContentType,
                    file);

                _selectedFiles.Add(uploadFile);
                Logger.Debug("File selected: {FileName} ({FileSize})",
                    file.Name,
                    FormatFileSize(file.Size));
            }
            else
            {
                if (file is IAsyncDisposable disposable)
                {
                    await disposable.DisposeAsync();
                }

                Logger.Warning("Invalid file rejected: {FileName}", file.Name);
            }
        }
    }

    private bool ValidateFile(IBrowserFile file)
    {
        if (file.Size > MaxFileSize)
        {
            Logger.Warning("File exceeds size limit: {FileName} ({FileSize})",
                file.Name,
                FormatFileSize(file.Size));
            return false;
        }

        if (AllowedFileTypes.Any() &&
            !AllowedFileTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            Logger.Warning("File type not allowed: {FileName} ({FileType})",
                file.Name,
                file.ContentType);
            return false;
        }

        return true;
    }

    private async Task RemoveFile(UploadFile file)
    {
        if (IsUploading || IsDisposed)
        {
            return;
        }

        _selectedFiles.Remove(file);
        if (file.File is IAsyncDisposable disposable)
        {
            await disposable.DisposeAsync();
        }

        Logger.Debug("File removed: {FileName}", file.Name);
        StateHasChanged();
    }

    private async Task UploadFilesAsync()
    {
        if (IsUploading || UploadFileAsync is null || IsDisposed)
        {
            return;
        }

        IsUploading = true;
        UploadProgress = 0;

        try
        {
            var uploadTasks = new List<Task>();
            foreach (var file in _selectedFiles.ToList())
            {
                var cts = new CancellationTokenSource();
                _uploadCancellationTokens[file.Name] = cts;

                uploadTasks.Add(UploadSingleFile(file, cts.Token));
            }

            await Task.WhenAll(uploadTasks);
            await NotifyUploadCompletion();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during file upload");
        }
        finally
        {
            foreach (var cts in _uploadCancellationTokens.Values)
            {
                cts.Dispose();
            }

            _uploadCancellationTokens.Clear();

            IsUploading = false;
            UploadProgress = 0;
            StateHasChanged();
        }
    }

    private async Task UploadSingleFile(UploadFile file, CancellationToken cancellationToken)
    {
        file.UploadStatus = UploadStatus.Uploading;
        file.UploadProgress = 0;

        try
        {
            var progress = new Progress<int>(percent =>
            {
                file.UploadProgress = percent;
                UploadProgress = (int)_selectedFiles.Average(f => f.UploadProgress);
                StateHasChanged();
            });

            if (UploadFileAsync != null)
            {
                var result = await UploadFileAsync(file, progress, cancellationToken);
                file.UploadStatus = result.Status;

                if (result.Status == UploadStatus.Success)
                {
                    _uploadedFiles.Add(file);
                    Logger.Debug("Upload succeeded: {FileName}", file.Name);
                }
                else
                {
                    Logger.Warning("Upload failed: {FileName} - {Status}",
                        file.Name,
                        result.Status);
                }
            }
            else
            {
                file.UploadStatus = UploadStatus.Failure;
                Logger.Warning("Upload delegate not set: {FileName}", file.Name);
            }
        }
        catch (OperationCanceledException)
        {
            file.UploadStatus = UploadStatus.Cancelled;
            Logger.Information("Upload cancelled: {FileName}", file.Name);
        }
        catch (Exception ex)
        {
            file.UploadStatus = UploadStatus.Failure;
            Logger.Error(ex, "Upload error: {FileName}", file.Name);
        }
    }

    private async Task NotifyUploadCompletion()
    {
        if (OnFilesUploaded.HasDelegate)
        {
            await OnFilesUploaded.InvokeAsync(_uploadedFiles.ToList());
        }

        _selectedFiles.RemoveAll(f => f.UploadStatus == UploadStatus.Success);
    }

    private async Task OpenFileDialog()
    {
        try
        {
            await SafeJsVoidInteropAsync(
                "clickElementById",
                $"{ComponentId}-file-input");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error opening file dialog");
        }
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        var order = 0;
        double len = bytes;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }

    private static string GetStatusClass(UploadStatus status)
    {
        return status switch
        {
            UploadStatus.Success => "text-success",
            UploadStatus.Failure => "text-danger",
            UploadStatus.Warning => "text-warning",
            UploadStatus.Cancelled => "text-muted",
            _ => "text-muted"
        };
    }

    private static string GetStatusIconClass(UploadStatus status)
    {
        return status switch
        {
            UploadStatus.Success => "fas fa-check-circle text-success",
            UploadStatus.Failure => "fas fa-times-circle text-danger",
            UploadStatus.Warning => "fas fa-exclamation-circle text-warning",
            UploadStatus.Cancelled => "fas fa-ban text-muted",
            _ => "fas fa-question-circle text-muted"
        };
    }

    #region Parameters

    /// <summary>
    ///     Gets or sets the maximum allowed file size in bytes (defaults to 10 MB).
    /// </summary>
    [Parameter]
    public int MaxFileSize { get; set; } = (int)(10 * BYTES_PER_MB);

    /// <summary>
    ///     Gets or sets the list of allowed file types (MIME types).
    ///     Leave empty to allow all types.
    /// </summary>
    [Parameter]
    public IReadOnlyCollection<string> AllowedFileTypes { get; set; } = Array.Empty<string>();

    /// <summary>
    ///     Event callback triggered after files are uploaded successfully.
    /// </summary>
    [Parameter]
    public EventCallback<List<UploadFile>> OnFilesUploaded { get; set; }

    /// <summary>
    ///     Delegate responsible for handling the actual file upload process.
    /// </summary>
    /// <remarks>
    ///     The delegate should handle the actual upload of the file to your backend/storage
    ///     and return an UploadResult indicating success or failure.
    /// </remarks>
    [Parameter]
    public Func<UploadFile, IProgress<int>, CancellationToken, Task<UploadResult>>? UploadFileAsync { get; set; }

    #endregion
}
