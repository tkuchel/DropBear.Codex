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
///     Refactored to leverage DropBearComponentBase for JS interop and disposal.
/// </summary>
public sealed partial class DropBearFileUploader : DropBearComponentBase
{
    // Typically rename to match your actual JS module name:
    private const string MODULE_NAME = "file-reader-helpers";
    private const long BYTES_PER_MB = 1024 * 1024;

    // Private fields
    private readonly List<UploadFile> _selectedFiles = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _uploadCancellationTokens = new();
    private readonly List<UploadFile> _uploadedFiles = new();
    private bool _isDragOver;
    private bool _isUploading;

    private IJSObjectReference? _jsModule;

    private int _uploadProgress;

    #region Properties

    /// <summary>
    ///     Indicates whether file upload is currently in progress.
    /// </summary>
    private bool IsUploading
    {
        get => _isUploading;
        set
        {
            if (_isUploading == value)
            {
                return;
            }

            _isUploading = value;
            InvokeStateHasChanged(() => { });
        }
    }

    /// <summary>
    ///     Current upload progress as a percentage.
    /// </summary>
    private int UploadProgress
    {
        get => _uploadProgress;
        set
        {
            if (_uploadProgress == value)
            {
                return;
            }

            _uploadProgress = value;
            InvokeStateHasChanged(() => { });
        }
    }

    /// <summary>
    ///     Read-only view of currently selected files.
    /// </summary>
    private IReadOnlyList<UploadFile> SelectedFiles => _selectedFiles;

    /// <summary>
    ///     Read-only view of successfully uploaded files.
    /// </summary>
    private IReadOnlyList<UploadFile> UploadedFiles => _uploadedFiles;

    /// <summary>
    ///     Indicates whether uploading is allowed (some files are selected and no active upload).
    /// </summary>
    private bool CanUpload => SelectedFiles.Any()
                              && !IsUploading
                              && !IsDisposed
                              && UploadFileAsync != null;

    #endregion

    #region Lifecycle

    /// <inheritdoc />
    /// <remarks>
    ///     Loads the JS module once on first render and logs initialization.
    /// </remarks>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (!firstRender || IsDisposed)
        {
            return;
        }

        try
        {
            // Ensure the JS module is loaded once and cached
            await EnsureJsModuleInitializedAsync(MODULE_NAME).ConfigureAwait(false);
            _jsModule = await GetJsModuleAsync(MODULE_NAME).ConfigureAwait(false);

            Logger.Debug("File uploader JS module initialized: {ComponentId}", ComponentId);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to initialize file uploader module: {ComponentId}", ComponentId);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Cancel any ongoing uploads, then call base disposal logic.
    ///     This is your custom disposal for <see cref="_uploadCancellationTokens" />.
    /// </remarks>
    protected override async ValueTask DisposeCoreAsync()
    {
        try
        {
            foreach (var cts in _uploadCancellationTokens.Values)
            {
                await cts.CancelAsync().ConfigureAwait(false);
                cts.Dispose();
            }

            _uploadCancellationTokens.Clear();
        }
        finally
        {
            // Always call the base
            await base.DisposeCoreAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Called by the base class to allow final JS cleanup if needed.
    ///     For example, you could call a JS dispose function if your
    ///     "file-reader-helpers" module includes it.
    /// </remarks>
    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        try
        {
            // If your JS module has a dispose function:
            // await _jsModule?.InvokeVoidAsync("DropBearFileUploader.dispose", ComponentId);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during JS cleanup for DropBearFileUploader: {ComponentId}", ComponentId);
        }
        finally
        {
            // This just resets our local reference; the base class also cleans up
            _jsModule = null;
        }
    }

    #endregion

    #region Drag & Drop

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
        _isDragOver = false;
        if (IsUploading || IsDisposed)
        {
            return;
        }

        try
        {
            // If you have a function in "file-reader-helpers" named "getDroppedFiles":
            if (_jsModule is null)
            {
                _jsModule = await GetJsModuleAsync(MODULE_NAME).ConfigureAwait(false);
            }

            var jsFiles = await _jsModule.InvokeAsync<IJSObjectReference[]>(
                "getDroppedFiles",
                ComponentToken, // base class token for cancellation
                e.DataTransfer
            ).ConfigureAwait(false);

            var browserFiles = new List<IBrowserFile>();
            foreach (var jsFile in jsFiles)
            {
                try
                {
                    // If you have a separate helper to create a file proxy:
                    var proxy = await BrowserFileProxy.CreateAsync(jsFile).ConfigureAwait(false);
                    browserFiles.Add(proxy);
                }
                catch (Exception exProxy)
                {
                    Logger.Error(exProxy, "Failed to create file proxy for dropped file");
                    await jsFile.DisposeAsync().ConfigureAwait(false);
                }
            }

            await ProcessSelectedFiles(browserFiles).ConfigureAwait(false);
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling dropped files for: {ComponentId}", ComponentId);
        }
    }

    #endregion

    #region File Selection

    private async Task HandleFileSelectionAsync(InputFileChangeEventArgs e)
    {
        if (IsUploading || IsDisposed)
        {
            return;
        }

        try
        {
            await ProcessSelectedFiles(e.GetMultipleFiles()).ConfigureAwait(false);
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
                    file
                );

                _selectedFiles.Add(uploadFile);
                Logger.Debug("File selected: {FileName} ({FileSize})",
                    file.Name,
                    FormatFileSize(file.Size));
            }
            else
            {
                if (file is IAsyncDisposable disposable)
                {
                    await disposable.DisposeAsync().ConfigureAwait(false);
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
            await disposable.DisposeAsync().ConfigureAwait(false);
        }

        Logger.Debug("File removed: {FileName}", file.Name);
        StateHasChanged();
    }

    #endregion

    #region Upload Logic

    private async Task UploadFilesAsync()
    {
        if (!CanUpload)
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
                // For each file, create a separate CancellationTokenSource if you need
                // per-file cancellation. Alternatively, you could link to ComponentToken.
                var cts = new CancellationTokenSource();
                _uploadCancellationTokens[file.Name] = cts;

                uploadTasks.Add(UploadSingleFile(file, cts.Token));
            }

            await Task.WhenAll(uploadTasks).ConfigureAwait(false);
            await NotifyUploadCompletion().ConfigureAwait(false);
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
            // Progress delegate updates both the file’s individual progress
            // and the overall component’s average progress
            var progress = new Progress<int>(percent =>
            {
                file.UploadProgress = percent;
                UploadProgress = (int)_selectedFiles.Average(f => f.UploadProgress);
                StateHasChanged();
            });

            if (UploadFileAsync is not null)
            {
                // The user-provided delegate performs the actual upload
                var result = await UploadFileAsync(file, progress, cancellationToken).ConfigureAwait(false);
                file.UploadStatus = result.Status;

                if (result.Status == UploadStatus.Success)
                {
                    _uploadedFiles.Add(file);
                    Logger.Debug("Upload succeeded: {FileName}", file.Name);
                }
                else
                {
                    Logger.Warning("Upload failed: {FileName} - {Status}", file.Name, result.Status);
                }
            }
            else
            {
                file.UploadStatus = UploadStatus.Failure;
                Logger.Warning("No upload delegate set for file: {FileName}", file.Name);
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
            await OnFilesUploaded.InvokeAsync(_uploadedFiles.ToList()).ConfigureAwait(false);
        }

        // Remove successfully uploaded files from the "selected" list
        _selectedFiles.RemoveAll(f => f.UploadStatus == UploadStatus.Success);
    }

    private async Task OpenFileDialog()
    {
        try
        {
            // Invokes a JS helper that triggers a click on the <input> element
            await SafeJsVoidInteropAsync(
                "clickElementById",
                $"{ComponentId}-file-input"
            ).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error opening file dialog");
        }
    }

    #endregion

    #region Helpers

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

    #endregion

    #region Parameters

    /// <summary>
    ///     Maximum allowed file size in bytes (default: 10 MB).
    /// </summary>
    [Parameter]
    public int MaxFileSize { get; set; } = (int)(10 * BYTES_PER_MB);

    /// <summary>
    ///     List of allowed file types (MIME types). Empty = allow all.
    /// </summary>
    [Parameter]
    public IReadOnlyCollection<string> AllowedFileTypes { get; set; } = Array.Empty<string>();

    /// <summary>
    ///     Event callback triggered after files upload successfully.
    /// </summary>
    [Parameter]
    public EventCallback<List<UploadFile>> OnFilesUploaded { get; set; }

    /// <summary>
    ///     Delegate that performs the actual file upload and returns an <see cref="UploadResult" />.
    /// </summary>
    [Parameter]
    public Func<UploadFile, IProgress<int>, CancellationToken, Task<UploadResult>>? UploadFileAsync { get; set; }

    #endregion
}
