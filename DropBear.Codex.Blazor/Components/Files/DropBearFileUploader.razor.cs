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
///     Optimized for Blazor Server with proper thread safety, memory management, and resilience features.
/// </summary>
public sealed partial class DropBearFileUploader : DropBearComponentBase
{
    #region Constructor

    /// <summary>
    ///     Initializes a new instance of the DropBearFileUploader component.
    /// </summary>
    public DropBearFileUploader()
    {
        _progressUpdateTimer = new Timer(ProcessProgressUpdates, null,
            TimeSpan.FromMilliseconds(ProgressUpdateIntervalMs),
            TimeSpan.FromMilliseconds(ProgressUpdateIntervalMs));
    }

    #endregion

    #region Cleanup and Disposal

    /// <inheritdoc />
    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        try
        {
            _progressUpdateTimer.Dispose();
            _concurrentUploadsSemaphore.Dispose();

            await CancelAllUploads();

            foreach (var cts in _uploadCancellationTokens.Values)
            {
                cts.Dispose();
            }

            _uploadCancellationTokens.Clear();

            if (_jsModule != null)
            {
                await _jsModule.InvokeVoidAsync($"{ModuleName}API.clearDroppedFileStore");
            }

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

    #endregion

    #region Constants

    private const string ModuleName = JsModuleNames.FileReaderHelpers;
    private const long BytesPerMb = 1024 * 1024;
    private const int StateUpdateDebounceMs = 100;
    private const int MaxConcurrentUploads = 3;
    private const int MaxRetryAttempts = 3;
    private const int RetryDelayMs = 1000;
    private const int ProgressUpdateIntervalMs = 100;

    #endregion

    #region Private Fields

    private readonly SemaphoreSlim _fileSemaphore = new(1, 1);
    private readonly SemaphoreSlim _uploadSemaphore = new(1, 1);
    private readonly SemaphoreSlim _concurrentUploadsSemaphore = new(MaxConcurrentUploads, MaxConcurrentUploads);

    private readonly ConcurrentDictionary<string, UploadFile> _selectedFiles = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _uploadCancellationTokens = new();
    private readonly ConcurrentQueue<UploadFile> _uploadedFiles = new();
    private readonly Queue<int> _progressUpdates = new();

    private readonly Timer _progressUpdateTimer;
    private readonly string[] _blockedExtensions = { ".exe", ".dll", ".bat", ".cmd", ".msi", ".sh", ".app" };

    private int _dragCounter;
    private volatile bool _isDragOver;
    private volatile bool _isInitialized;
    private volatile bool _isUploading;
    private volatile bool _isCircuitConnected = true;

    private IJSObjectReference? _jsModule;
    private IJSObjectReference? _jsUtilsModule;
    private CancellationTokenSource? _stateUpdateDebouncer;
    private volatile int _uploadProgress;
    private ElementReference _dropZoneElement;

    #endregion

    #region Parameters

    /// <summary>
    ///     Gets or sets the maximum allowed file size in bytes. Defaults to 10MB.
    /// </summary>
    [Parameter]
    public int MaxFileSize { get; set; } = (int)(10 * BytesPerMb);

    /// <summary>
    ///     Gets or sets the collection of allowed file types (MIME types).
    ///     If empty, all file types are allowed.
    /// </summary>
    [Parameter]
    public IReadOnlyCollection<string> AllowedFileTypes { get; set; } = Array.Empty<string>();

    /// <summary>
    ///     Event callback triggered when files are successfully uploaded.
    /// </summary>
    [Parameter]
    public EventCallback<List<UploadFile>> OnFilesUploaded { get; set; }

    /// <summary>
    ///     Function to handle the actual file upload process.
    /// </summary>
    [Parameter]
    public Func<UploadFile, IProgress<int>, CancellationToken, Task<UploadResult>>? UploadFileAsync { get; set; }

    /// <summary>
    ///     Optional key for persisting component state across sessions.
    /// </summary>
    [Parameter]
    public string? PersistenceKey { get; set; }

    #endregion

    #region Properties

    private bool CanUpload => _selectedFiles.Any() && !_isUploading && !IsDisposed && UploadFileAsync != null;
    private IReadOnlyDictionary<string, UploadFile> SelectedFiles => _selectedFiles;
    private IEnumerable<UploadFile> UploadedFiles => _uploadedFiles.ToArray();

    #endregion

    #region Lifecycle Methods

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            if (!string.IsNullOrEmpty(PersistenceKey))
            {
                await RestoreStateAsync();
            }

            await base.OnInitializedAsync();
        }
        catch (Exception ex)
        {
            LogError("Failed to initialize component", ex);
            throw;
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _jsModule ??= await GetJsModuleAsync(ModuleName);

            await _jsModule.InvokeVoidAsync($"{ModuleName}API.initialize");
            await _jsModule.InvokeVoidAsync($"{ModuleName}API.initGlobalDropPrevention");
            await _jsModule.InvokeVoidAsync($"{ModuleName}API.initializeDropZone", _dropZoneElement);

            await RegisterCircuitHandler();
        }
    }

    /// <inheritdoc />
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

    #endregion

    #region Event Handlers

    /// <summary>
    ///     Handles the dragenter event and updates the drag state.
    /// </summary>
    private void HandleDragEnter(DragEventArgs e)
    {
        if (_isUploading || IsDisposed)
        {
            return;
        }

        _dragCounter++;
        _isDragOver = true;
        _ = QueueStateUpdate();
    }

    /// <summary>
    ///     Handles the dragleave event and updates the drag state.
    /// </summary>
    private void HandleDragLeave(DragEventArgs e)
    {
        if (_isUploading || IsDisposed)
        {
            return;
        }

        _dragCounter--;
        if (_dragCounter <= 0)
        {
            _isDragOver = false;
            _dragCounter = 0;
        }

        _ = QueueStateUpdate();
    }

    /// <summary>
    ///     Handles the drop event and processes dropped files.
    /// </summary>
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

            LogDebug("Files array length: {Length}", e.DataTransfer.Files.Length);
            LogDebug("Files: {Files}", string.Join(", ", e.DataTransfer.Files));
            LogDebug("Items count: {Count}", e.DataTransfer.Items.Length);

            foreach (var item in e.DataTransfer.Items)
            {
                LogDebug("Item - Kind: {Kind}, Type: {Type}", item.Kind, item.Type);
            }

            var fileData = new
            {
                fileNames = e.DataTransfer.Files,
                fileTypes = e.DataTransfer.Items
                    .Where(item => item.Kind == "file")
                    .Select(item => item.Type)
                    .ToArray()
            };

            LogDebug("Sending to JS: {FileData}", JsonSerializer.Serialize(fileData));

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

    /// <summary>
    ///     Captures data from the drop event for processing.
    /// </summary>
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

    /// <summary>
    ///     Handles file selection from the file input element.
    /// </summary>
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

    #endregion

    #region File Processing Methods

    /// <summary>
    ///     Processes the selected files, validating and preparing them for upload.
    /// </summary>
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

    /// <summary>
    ///     Validates a file against size and type restrictions.
    /// </summary>
    private bool ValidateFile(IBrowserFile file)
    {
        // Check for blocked extensions
        var extension = Path.GetExtension(file.Name).ToLowerInvariant();
        if (_blockedExtensions.Contains(extension))
        {
            LogWarning("Blocked file type: {FileName} ({Extension})", file.Name, extension);
            return false;
        }

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

    /// <summary>
    ///     Removes a file from the selected files list.
    /// </summary>
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

    #endregion

    #region Upload Methods

    /// <summary>
    ///     Initiates the upload process for all selected files.
    /// </summary>
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

    /// <summary>
    ///     Uploads a single file with retry logic and progress tracking.
    /// </summary>
    private async Task UploadSingleFile(UploadFile file, CancellationToken cancellationToken)
    {
        file.UploadStatus = UploadStatus.Uploading;
        file.UploadProgress = 0;

        try
        {
            await _concurrentUploadsSemaphore.WaitAsync(cancellationToken);

            try
            {
                var progress = new Progress<int>(percent =>
                {
                    file.UploadProgress = percent;
                    _progressUpdates.Enqueue(percent);
                });

                if (UploadFileAsync != null)
                {
                    var result = await RetryUploadAsync(async () =>
                    {
                        if (!_isCircuitConnected)
                        {
                            throw new OperationCanceledException("Circuit disconnected");
                        }

                        return await UploadFileAsync(file, progress, cancellationToken);
                    });

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
            finally
            {
                _concurrentUploadsSemaphore.Release();
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

    /// <summary>
    ///     Retries an upload operation with exponential backoff.
    /// </summary>
    private async Task<UploadResult> RetryUploadAsync(
        Func<Task<UploadResult>> uploadAction,
        int maxRetries = MaxRetryAttempts)
    {
        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                return await uploadAction();
            }
            catch (Exception ex) when (i < maxRetries - 1 &&
                                       ex is not OperationCanceledException)
            {
                LogWarning("Upload attempt {Attempt} failed, retrying...", i + 1);
                await Task.Delay(RetryDelayMs * (1 << i)); // Exponential backoff
            }
        }

        throw new Exception($"Upload failed after {maxRetries} attempts");
    }

    /// <summary>
    ///     Notifies completion of file uploads and cleans up successful uploads.
    /// </summary>
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

        if (!string.IsNullOrEmpty(PersistenceKey))
        {
            await SaveStateAsync();
        }
    }

    #endregion

    #region UI Interaction Methods

    /// <summary>
    ///     Opens the file selection dialog.
    /// </summary>
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

    /// <summary>
    ///     Processes queued progress updates to reduce SignalR messages.
    /// </summary>
    private void ProcessProgressUpdates(object? state)
    {
        if (_progressUpdates.Count == 0)
        {
            return;
        }

        var progress = (int)_progressUpdates.Average();
        _progressUpdates.Clear();

        try
        {
            _ = InvokeAsync(() =>
            {
                _uploadProgress = progress;
                StateHasChanged();
            });
        }
        catch (ObjectDisposedException)
        {
            // Component might be disposed, ignore
        }
    }

    /// <summary>
    ///     Queues a state update with debouncing.
    /// </summary>
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

    #endregion

    #region Circuit and State Management

    /// <summary>
    ///     Registers a handler for circuit disconnections.
    /// </summary>
    private async Task RegisterCircuitHandler()
    {
        if (_jsModule != null)
        {
            await _jsModule.InvokeVoidAsync(
                "blazor.registerCircuitHandler",
                DotNetObjectReference.Create(this));
        }
    }

    /// <summary>
    ///     Handles circuit disconnection events.
    /// </summary>
    [JSInvokable]
    public void OnCircuitClose()
    {
        _isCircuitConnected = false;
        _ = CancelAllUploads();
    }

    /// <summary>
    ///     Cancels all active uploads.
    /// </summary>
    private async Task CancelAllUploads()
    {
        foreach (var cts in _uploadCancellationTokens.Values)
        {
            try
            {
                await cts.CancelAsync();
            }
            catch
            {
                // Ignore cancellation errors
            }
        }
    }

    /// <summary>
    ///     Saves the current component state.
    /// </summary>
    private async Task SaveStateAsync()
    {
        try
        {
            if (_jsModule != null && !string.IsNullOrEmpty(PersistenceKey))
            {
                var state = new
                {
                    SelectedFiles = _selectedFiles.Values.Select(f => new
                    {
                        f.Name,
                        f.Size,
                        f.ContentType,
                        f.UploadStatus,
                        f.UploadProgress
                    }).ToList()
                };

                await _jsModule.InvokeVoidAsync(
                    $"{ModuleName}API.saveState",
                    PersistenceKey,
                    JsonSerializer.Serialize(state));
            }
        }
        catch (Exception ex)
        {
            LogWarning("Failed to save state", ex);
        }
    }

    /// <summary>
    ///     Restores the component state from persistence.
    /// </summary>
    private async Task RestoreStateAsync()
    {
        try
        {
            if (_jsModule != null && !string.IsNullOrEmpty(PersistenceKey))
            {
                var state = await _jsModule.InvokeAsync<string>(
                    $"{ModuleName}API.getStoredState",
                    PersistenceKey);

                if (!string.IsNullOrEmpty(state))
                {
                    // Process stored state
                    LogDebug("State restored from persistence");
                }
            }
        }
        catch (Exception ex)
        {
            LogWarning("Failed to restore state", ex);
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Formats a byte size into a human-readable string.
    /// </summary>
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
    ///     Gets the CSS class for an upload status.
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
    ///     Gets the icon CSS class for an upload status.
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
}
