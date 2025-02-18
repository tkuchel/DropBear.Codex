#region

using System.Collections.Concurrent;
using System.Text.Json;
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
///     Optimized for Blazor Server with proper thread safety and memory management.
/// </summary>
public sealed partial class DropBearFileUploader : DropBearComponentBase
{
    private const string ModuleName = JsModuleNames.FileReaderHelpers;
    private const long BytesPerMb = 1024 * 1024;
    private const int StateUpdateDebounceMs = 100;
    private readonly SemaphoreSlim _fileSemaphore = new(1, 1);

    private readonly ConcurrentDictionary<string, UploadFile> _selectedFiles = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _uploadCancellationTokens = new();
    private readonly ConcurrentQueue<UploadFile> _uploadedFiles = new();
    private readonly SemaphoreSlim _uploadSemaphore = new(1, 1);
    private int _dragCounter;
    private volatile bool _isDragOver;

    private volatile bool _isInitialized;
    private volatile bool _isUploading;
    private IJSObjectReference? _jsModule;
    private IJSObjectReference? _jsUtilsModule;
    private CancellationTokenSource? _stateUpdateDebouncer;
    private volatile int _uploadProgress;
    private ElementReference DropZoneElement;

    [Parameter] public int MaxFileSize { get; set; } = (int)(10 * BytesPerMb);
    [Parameter] public IReadOnlyCollection<string> AllowedFileTypes { get; set; } = Array.Empty<string>();
    [Parameter] public EventCallback<List<UploadFile>> OnFilesUploaded { get; set; }

    [Parameter]
    public Func<UploadFile, IProgress<int>, CancellationToken, Task<UploadResult>>? UploadFileAsync { get; set; }

    private bool CanUpload => _selectedFiles.Any() && !_isUploading && !IsDisposed && UploadFileAsync != null;
    private IReadOnlyDictionary<string, UploadFile> SelectedFiles => _selectedFiles;
    private IEnumerable<UploadFile> UploadedFiles => _uploadedFiles.ToArray();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _jsModule ??= await GetJsModuleAsync(ModuleName);

            // Ensure FileReaderHelpers is initialized
            await _jsModule.InvokeVoidAsync($"{ModuleName}API.initialize");
            // Enable global drop prevention
            await _jsModule.InvokeVoidAsync($"{ModuleName}API.initGlobalDropPrevention");
            // Initialize the drop zone
            await _jsModule!.InvokeVoidAsync($"{ModuleName}API.initializeDropZone", DropZoneElement);
        }
    }

    protected override async Task InitializeComponentAsync()
    {
        if (_isInitialized || IsDisposed)
        {
            return;
        }

        try
        {
            await _uploadSemaphore.WaitAsync(ComponentToken);

            _jsModule ??= await GetJsModuleAsync(ModuleName);
            _jsUtilsModule ??= await GetJsModuleAsync(JsModuleNames.Utils);

            _isInitialized = true;
            LogDebug("File uploader initialized");
        }
        catch (Exception ex)
        {
            LogError("Failed to initialize file uploader", ex);
            throw;
        }
        finally
        {
            _uploadSemaphore.Release();
        }
    }

    private async Task QueueStateUpdate()
    {
        if (_stateUpdateDebouncer is not null)
        {
            await _stateUpdateDebouncer.CancelAsync();
        }

        try
        {
            _stateUpdateDebouncer = new CancellationTokenSource();
            await Task.Delay(StateUpdateDebounceMs, _stateUpdateDebouncer.Token);
            await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException)
        {
            // Debouncing in action, ignore
        }
        catch (NullReferenceException nullReferenceException)
        {
            LogWarning("Failed to queue state update", nullReferenceException);
        }
        catch (ObjectDisposedException)
        {
            // Component disposed, ignore
        }
        catch (Exception ex)
        {
            LogError("Failed to queue state update", ex);
        }
        finally
        {
            _stateUpdateDebouncer?.Dispose();
            _stateUpdateDebouncer = null;
        }
    }

    /// <summary>
    ///     Handles the dragenter event and updates the drag state.
    /// </summary>
    /// <param name="e">The drag event args.</param>
    private void HandleDragEnter(DragEventArgs e)
    {
        if (_isUploading || IsDisposed)
        {
            return;
        }

        _dragCounter++; // Increment drag counter.
        _isDragOver = true;
        _ = QueueStateUpdate();
    }

    /// <summary>
    ///     Handles the dragleave event and updates the drag state.
    /// </summary>
    /// <param name="e">The drag event args.</param>
    private void HandleDragLeave(DragEventArgs e)
    {
        if (_isUploading || IsDisposed)
        {
            return;
        }

        _dragCounter--; // Decrement drag counter.
        if (_dragCounter <= 0)
        {
            _isDragOver = false;
            _dragCounter = 0; // Reset counter if below zero.
        }

        _ = QueueStateUpdate();
    }

    private async Task HandleDrop(DragEventArgs e)
    {
        if (_isUploading || IsDisposed)
        {
            return;
        }

        _isDragOver = false;

        try
        {
            await _fileSemaphore.WaitAsync(ComponentToken);

            if (!_isInitialized)
            {
                await InitializeComponentAsync();
            }

            // Log what we have
            LogDebug("Files array length: {Length}", e.DataTransfer.Files.Length);
            LogDebug("Files: {Files}", string.Join(", ", e.DataTransfer.Files));
            LogDebug("Items count: {Count}", e.DataTransfer.Items.Length);
            foreach (var item in e.DataTransfer.Items)
            {
                LogDebug("Item - Kind: {Kind}, Type: {Type}", item.Kind, item.Type);
            }

            // Create the transfer data structure
            var fileData = new
            {
                fileNames = e.DataTransfer.Files,
                fileTypes = e.DataTransfer.Items
                    .Where(item => item.Kind == "file")
                    .Select(item => item.Type)
                    .ToArray()
            };

            LogDebug("Sending to JS: {FileData}",
                JsonSerializer.Serialize(fileData));

            var fileKeys = await _jsModule!.InvokeAsync<string[]>(
                $"{ModuleName}API.getDroppedFileKeys",
                ComponentToken,
                fileData
            );

            LogDebug("Received keys: {Keys}", string.Join(", ", fileKeys));

            var browserFiles = new List<IBrowserFile>();
            foreach (var key in fileKeys)
            {
                try
                {
                    var proxy = await BrowserFileProxy.CreateAsync(key, _jsModule);
                    browserFiles.Add(proxy);
                }
                catch (Exception ex)
                {
                    LogError("Failed to create file proxy", ex);
                }
            }

            await ProcessSelectedFiles(browserFiles);
        }
        catch (Exception ex)
        {
            LogError("Failed to handle dropped files", ex);
        }
        finally
        {
            _fileSemaphore.Release();
            await QueueStateUpdate();
        }
    }

    private async Task OnDropCapture(DragEventArgs e)
    {
        try
        {
            if (_jsModule != null)
            {
                LogDebug("Capturing drop data with files count: {Count}", e.DataTransfer.Files.Length);
                foreach (var file in e.DataTransfer.Files)
                {
                    LogDebug("File in DataTransfer: {File}", file);
                }
                await _jsModule.InvokeVoidAsync($"{ModuleName}API.captureDropData", e.DataTransfer);
            }
        }
        catch (Exception ex)
        {
            LogError("Failed to capture drop data", ex);
        }
    }



    private async Task HandleFileSelectionAsync(InputFileChangeEventArgs e)
    {
        if (_isUploading || IsDisposed)
        {
            return;
        }

        try
        {
            await _fileSemaphore.WaitAsync(ComponentToken);
            await ProcessSelectedFiles(e.GetMultipleFiles());
        }
        catch (Exception ex)
        {
            LogError("Failed to process selected files", ex);
        }
        finally
        {
            _fileSemaphore.Release();
            await QueueStateUpdate();
        }
    }

    private async Task ProcessSelectedFiles(IReadOnlyList<IBrowserFile> files)
    {
        foreach (var file in files)
        {
            if (ValidateFile(file))
            {
                var uploadFile = new UploadFile(file.Name, file.Size, file.ContentType, file);
                _selectedFiles.TryAdd(file.Name, uploadFile);
                LogDebug("File selected: {FileName} ({Size})", file.Name, FormatFileSize(file.Size));
            }
            else
            {
                if (file is IAsyncDisposable disposable)
                {
                    await disposable.DisposeAsync();
                }

                LogWarning("Invalid file rejected: {FileName}", file.Name);
            }
        }
    }

    private bool ValidateFile(IBrowserFile file)
    {
        if (file.Size > MaxFileSize)
        {
            LogWarning("File too large: {FileName} ({Size})", file.Name, FormatFileSize(file.Size));
            return false;
        }

        if (AllowedFileTypes.Any() &&
            !AllowedFileTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            LogWarning("File type not allowed: {FileName} ({Type})", file.Name, file.ContentType);
            return false;
        }

        return true;
    }

    private async Task RemoveFile(UploadFile file)
    {
        if (_isUploading || IsDisposed)
        {
            return;
        }

        try
        {
            await _fileSemaphore.WaitAsync(ComponentToken);

            if (_selectedFiles.TryRemove(file.Name, out _) && file.File is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }

            LogDebug("File removed: {FileName}", file.Name);
        }
        finally
        {
            _fileSemaphore.Release();
            await QueueStateUpdate();
        }
    }

    private async Task UploadFilesAsync()
    {
        if (!CanUpload)
        {
            return;
        }

        try
        {
            await _uploadSemaphore.WaitAsync(ComponentToken);
            _isUploading = true;
            _uploadProgress = 0;

            var uploadTasks = new List<Task>();
            foreach (var file in SelectedFiles.Values)
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
            LogError("Upload failed", ex);
        }
        finally
        {
            foreach (var cts in _uploadCancellationTokens.Values)
            {
                cts.Dispose();
            }

            _uploadCancellationTokens.Clear();
            _isUploading = false;
            _uploadProgress = 0;
            _uploadSemaphore.Release();
            await QueueStateUpdate();
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
                _uploadProgress = (int)_selectedFiles.Values.Average(f => f.UploadProgress);
                _ = QueueStateUpdate();
            });

            if (UploadFileAsync != null)
            {
                var result = await UploadFileAsync(file, progress, cancellationToken);
                file.UploadStatus = result.Status;

                if (result.Status == UploadStatus.Success)
                {
                    _uploadedFiles.Enqueue(file);
                    LogDebug("Upload succeeded: {FileName}", file.Name);
                }
                else
                {
                    LogWarning("Upload failed: {FileName} - {Status}", file.Name, result.Status);
                }
            }
        }
        catch (OperationCanceledException)
        {
            file.UploadStatus = UploadStatus.Cancelled;
            LogDebug("Upload cancelled: {FileName}", file.Name);
        }
        catch (Exception ex)
        {
            file.UploadStatus = UploadStatus.Failure;
            LogError("Upload error: {FileName}", ex, file.Name);
        }
    }

    private async Task NotifyUploadCompletion()
    {
        if (OnFilesUploaded.HasDelegate)
        {
            await OnFilesUploaded.InvokeAsync(_uploadedFiles.ToList());
        }

        foreach (var file in _selectedFiles.Values.Where(f => f.UploadStatus == UploadStatus.Success))
        {
            _selectedFiles.TryRemove(file.Name, out _);
        }
    }

    private async Task OpenFileDialog()
    {
        try
        {
            if (!_isInitialized)
            {
                await InitializeComponentAsync();
            }

            await _jsUtilsModule!.InvokeVoidAsync(
                $"{JsModuleNames.Utils}API.clickElementById",
                $"{ComponentId}-file-input"
            );
        }
        catch (Exception ex)
        {
            LogError("Failed to open file dialog", ex);
        }
    }

    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        try
        {
            foreach (var cts in _uploadCancellationTokens.Values)
            {
                await cts.CancelAsync();
                cts.Dispose();
            }

            _uploadCancellationTokens.Clear();

            if (_jsUtilsModule != null)
            {
                await _uploadSemaphore.WaitAsync(TimeSpan.FromSeconds(5));
                try
                {
                    await _jsUtilsModule.DisposeAsync();
                }
                finally
                {
                    _uploadSemaphore.Release();
                }
            }
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException)
        {
            LogWarning("Cleanup interrupted: {Reason}", ex.GetType().Name);
        }
        catch (Exception ex)
        {
            LogError("Failed to cleanup file uploader", ex);
        }
        finally
        {
            try
            {
                _uploadSemaphore.Dispose();
                _fileSemaphore.Dispose();
                _stateUpdateDebouncer?.Dispose();
            }
            catch (ObjectDisposedException) { }

            _jsModule = null;
            _jsUtilsModule = null;
            _isInitialized = false;
        }
    }

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
}
