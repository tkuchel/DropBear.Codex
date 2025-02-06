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
///     Leverages <see cref="DropBearComponentBase" /> for JavaScript interop and disposal.
/// </summary>
public sealed partial class DropBearFileUploader : DropBearComponentBase
{
    // Typically rename to match your actual JS module name.
    private const string MODULE_NAME = JsModuleNames.FileReaderHelpers;
    private const long BYTES_PER_MB = 1024 * 1024;

    // Private fields.
    private readonly List<UploadFile> _selectedFiles = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _uploadCancellationTokens = new();
    private readonly List<UploadFile> _uploadedFiles = new();
    private bool _isDragOver;
    private bool _isUploading;
    private IJSObjectReference? _jsModule;
    private IJSObjectReference? _jsUtilsModule;
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

            if (IsDisposed)
            {
                return;
            }

            try
            {
                InvokeStateHasChanged(() => { });
            }
            catch (ObjectDisposedException)
            {
                // Component disposed; ignore UI update.
            }
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
            // Schedule a UI update.
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
    ///     Indicates whether uploading is allowed (some files are selected, no active upload, not disposed, and an upload
    ///     delegate is set).
    /// </summary>
    private bool CanUpload => SelectedFiles.Any() &&
                              !IsUploading &&
                              !IsDisposed &&
                              UploadFileAsync != null;

    #endregion

    #region Lifecycle

    /// <inheritdoc />
    /// <remarks>
    ///     Loads the JS modules on first render.
    /// </remarks>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender).ConfigureAwait(false);
        if (!firstRender || IsDisposed)
        {
            return;
        }

        try
        {
            // Load and cache the required JS modules.
            _jsModule = await GetJsModuleAsync(MODULE_NAME).ConfigureAwait(false);
            _jsUtilsModule = await GetJsModuleAsync(JsModuleNames.Utils).ConfigureAwait(false);
            Logger.Debug("File uploader JS module initialized: {ComponentId}", ComponentId);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to initialize file uploader module: {ComponentId}", ComponentId);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Cancel any ongoing uploads and dispose cancellation tokens before calling base disposal logic.
    /// </remarks>
    public override async ValueTask DisposeAsync()
    {
        try
        {
            // Cancel and dispose all per-file cancellation tokens.
            foreach (var cts in _uploadCancellationTokens.Values)
            {
                await cts.CancelAsync().ConfigureAwait(false);
                cts.Dispose();
            }

            _uploadCancellationTokens.Clear();
        }
        finally
        {
            await base.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        try
        {
            // If needed, call a JS dispose function.
            // Example: await _jsModule?.InvokeVoidAsync("DropBearFileUploader.dispose", ComponentId);
            if (_jsUtilsModule is not null)
            {
                await _jsUtilsModule.DisposeAsync().ConfigureAwait(false);
                _jsUtilsModule = null;
            }
        }
        catch (JSDisconnectedException)
        {
            LogWarning("Cleanup skipped: JS runtime disconnected.");
        }
        catch (TaskCanceledException)
        {
            LogWarning("Cleanup skipped: Operation cancelled.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during JS cleanup for DropBearFileUploader: {ComponentId}", ComponentId);
        }
        finally
        {
            _jsModule = null;
        }
    }

    #endregion

    #region Drag & Drop

    /// <summary>
    ///     Handles the drag-enter event.
    /// </summary>
    private void HandleDragEnter()
    {
        if (!IsUploading && !IsDisposed)
        {
            _isDragOver = true;
            StateHasChanged();
        }
    }

    /// <summary>
    ///     Handles the drag-leave event.
    /// </summary>
    private void HandleDragLeave()
    {
        if (!IsUploading && !IsDisposed)
        {
            _isDragOver = false;
            StateHasChanged();
        }
    }

    /// <summary>
    ///     Handles the drop event, obtains file references via JS interop, and processes them.
    /// </summary>
    /// <param name="e">Drag event arguments.</param>
    private async Task HandleDrop(DragEventArgs e)
    {
        _isDragOver = false;
        if (IsUploading || IsDisposed)
        {
            return;
        }

        try
        {
            // Ensure the JS module is loaded.
            if (_jsModule is null)
            {
                _jsModule = await GetJsModuleAsync(MODULE_NAME).ConfigureAwait(false);
            }

            // Retrieve dropped files from JavaScript.
            var jsFiles = await _jsModule.InvokeAsync<IJSObjectReference[]>(
                    $"{MODULE_NAME}API.getDroppedFiles",
                    ComponentToken, // Use component cancellation token.
                    e.DataTransfer)
                .ConfigureAwait(false);

            var browserFiles = new List<IBrowserFile>();
            foreach (var jsFile in jsFiles)
            {
                try
                {
                    // Create a file proxy from the JS file reference.
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
            await InvokeAsync(StateHasChanged).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling dropped files for: {ComponentId}", ComponentId);
        }
    }

    #endregion

    #region File Selection

    /// <summary>
    ///     Handles file selection from an input element.
    /// </summary>
    /// <param name="e">Event arguments containing the selected files.</param>
    private async Task HandleFileSelectionAsync(InputFileChangeEventArgs e)
    {
        if (IsUploading || IsDisposed)
        {
            return;
        }

        try
        {
            await ProcessSelectedFiles(e.GetMultipleFiles()).ConfigureAwait(false);
            await InvokeAsync(StateHasChanged).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error processing selected files");
        }
    }

    /// <summary>
    ///     Processes and validates the selected files.
    /// </summary>
    /// <param name="files">The files to process.</param>
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
                Logger.Debug("File selected: {FileName} ({FileSize})", file.Name, FormatFileSize(file.Size));
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

    /// <summary>
    ///     Validates the file against size and allowed type constraints.
    /// </summary>
    /// <param name="file">The file to validate.</param>
    /// <returns>True if valid; otherwise, false.</returns>
    private bool ValidateFile(IBrowserFile file)
    {
        if (file.Size > MaxFileSize)
        {
            Logger.Warning("File exceeds size limit: {FileName} ({FileSize})",
                file.Name, FormatFileSize(file.Size));
            return false;
        }

        if (AllowedFileTypes.Any() &&
            !AllowedFileTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            Logger.Warning("File type not allowed: {FileName} ({FileType})",
                file.Name, file.ContentType);
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Removes a selected file.
    /// </summary>
    /// <param name="file">The file to remove.</param>
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
        await InvokeAsync(StateHasChanged).ConfigureAwait(false);
    }

    #endregion

    #region Upload Logic

    /// <summary>
    ///     Initiates the upload of all selected files.
    /// </summary>
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
            // Make a copy of the selected files list.
            foreach (var file in _selectedFiles.ToList())
            {
                // Create a separate cancellation token source for each file.
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
            await InvokeAsync(StateHasChanged).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Uploads a single file using the user-provided delegate.
    /// </summary>
    /// <param name="file">The file to upload.</param>
    /// <param name="cancellationToken">Cancellation token for the upload.</param>
    private async Task UploadSingleFile(UploadFile file, CancellationToken cancellationToken)
    {
        file.UploadStatus = UploadStatus.Uploading;
        file.UploadProgress = 0;

        try
        {
            // Create a progress delegate to update both the file's individual progress and the overall progress.
            var progress = new Progress<int>(percent =>
            {
                try
                {
                    file.UploadProgress = percent;
                    // Update the overall progress as the average of all file progress values.
                    UploadProgress = (int)_selectedFiles.Average(f => f.UploadProgress);
                    // Schedule a UI update (fire and forget).
                    _ = InvokeAsync(StateHasChanged);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error updating progress UI.");
                }
            });

            if (UploadFileAsync is not null)
            {
                // Invoke the user-provided delegate to perform the upload.
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

    /// <summary>
    ///     Notifies listeners that files have been uploaded and removes successfully uploaded files from the selected list.
    /// </summary>
    private async Task NotifyUploadCompletion()
    {
        if (OnFilesUploaded.HasDelegate)
        {
            await OnFilesUploaded.InvokeAsync(_uploadedFiles.ToList()).ConfigureAwait(false);
        }

        // Remove successfully uploaded files from the selected files list.
        _selectedFiles.RemoveAll(f => f.UploadStatus == UploadStatus.Success);
    }

    /// <summary>
    ///     Opens the file dialog via JavaScript.
    /// </summary>
    private async Task OpenFileDialog()
    {
        try
        {
            _jsUtilsModule ??= await GetJsModuleAsync(JsModuleNames.Utils).ConfigureAwait(false);
            // Invoke a JS helper that triggers a click on the hidden file input element.
            await _jsUtilsModule.InvokeVoidAsync(
                $"{JsModuleNames.Utils}API.clickElementById",
                $"{ComponentId}-file-input").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error opening file dialog");
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    ///     Formats a file size in bytes into a human-readable string.
    /// </summary>
    /// <param name="bytes">The file size in bytes.</param>
    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        var order = 0;
        double len = bytes;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    ///     Returns a CSS class name based on the upload status.
    /// </summary>
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

    /// <summary>
    ///     Returns an icon CSS class based on the upload status.
    /// </summary>
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
    ///     List of allowed file types (MIME types). An empty list allows all types.
    /// </summary>
    [Parameter]
    public IReadOnlyCollection<string> AllowedFileTypes { get; set; } = Array.Empty<string>();

    /// <summary>
    ///     Event callback triggered after files are successfully uploaded.
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
