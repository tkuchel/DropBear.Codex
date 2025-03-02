#region

using System.Collections.Concurrent;
using System.Text.Json;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Errors;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Results.Base;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Caching.Memory;
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
            await _progressUpdateTimer.DisposeAsync();

            try
            {
                _concurrentUploadsSemaphore.Dispose();
            }
            catch (ObjectDisposedException) { }

            await CancelAllUploads();

            // Dispose all cancellation tokens
            foreach (var tokenEntry in _uploadCancellationTokens)
            {
                try
                {
                    tokenEntry.Value.Dispose();
                }
                catch (ObjectDisposedException) { }
            }

            _uploadCancellationTokens.Clear();

            // Clean up file resources
            await DisposeSelectedFilesAsync();

            if (_jsModule != null)
            {
                try
                {
                    await _jsModule.InvokeVoidAsync($"{ModuleName}API.clearDroppedFileStore");
                }
                catch (JSException)
                {
                    /* Ignore JS errors during cleanup */
                }
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

    /// <summary>
    ///     Size of chunks for file upload, in bytes (1MB).
    /// </summary>
    private const long BytesPerMb = 1024 * 1024;

    /// <summary>
    ///     Debounce time for state updates to minimize UI refresh, in milliseconds.
    /// </summary>
    private const int StateUpdateDebounceMs = 100;

    /// <summary>
    ///     Maximum number of concurrent file uploads.
    /// </summary>
    private const int MaxConcurrentUploads = 3;

    /// <summary>
    ///     Maximum number of upload retry attempts.
    /// </summary>
    private const int MaxRetryAttempts = 3;

    /// <summary>
    ///     Base delay between retry attempts, in milliseconds.
    /// </summary>
    private const int RetryDelayMs = 1000;

    /// <summary>
    ///     Interval for processing progress updates, in milliseconds.
    /// </summary>
    private const int ProgressUpdateIntervalMs = 100;

    /// <summary>
    ///     Minimum time between progress updates to avoid UI thrashing, in milliseconds.
    /// </summary>
    private const int ProgressUpdateThrottleMs = 100;

    /// <summary>
    ///     Name of the JavaScript module for file operations.
    /// </summary>
    private const string ModuleName = JsModuleNames.FileReaderHelpers;

    /// <summary>
    ///     Size limit for the file metadata cache.
    /// </summary>
    private const int FileMetadataCacheSize = 1000;

    #endregion

    #region Private Fields

    /// <summary>
    ///     Thread synchronization primitive for file operations.
    /// </summary>
    private readonly SemaphoreSlim _fileSemaphore = new(1, 1);

    /// <summary>
    ///     Thread synchronization primitive for upload operations.
    /// </summary>
    private readonly SemaphoreSlim _uploadSemaphore = new(1, 1);

    /// <summary>
    ///     Thread synchronization primitive for limiting concurrent uploads.
    /// </summary>
    private readonly SemaphoreSlim _concurrentUploadsSemaphore = new(MaxConcurrentUploads, MaxConcurrentUploads);

    /// <summary>
    ///     Dictionary of file information for files selected for upload.
    /// </summary>
    private readonly ConcurrentDictionary<string, UploadFile> _selectedFiles = new();

    /// <summary>
    ///     Dictionary of cancellation tokens for active uploads, keyed by file name.
    /// </summary>
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _uploadCancellationTokens = new();

    /// <summary>
    ///     Queue of successfully uploaded files, maintained in upload order.
    /// </summary>
    private readonly ConcurrentQueue<UploadFile> _uploadedFiles = new();

    /// <summary>
    ///     Queue of progress updates to be processed.
    /// </summary>
    private readonly Queue<int> _progressUpdates = new();

    /// <summary>
    ///     Timer for batching progress updates to minimize UI refreshes.
    /// </summary>
    private readonly Timer _progressUpdateTimer;

    /// <summary>
    ///     List of file extensions that are blocked for security reasons.
    /// </summary>
    private readonly string[] _blockedExtensions = [".exe", ".dll", ".bat", ".cmd", ".msi", ".sh", ".app"];

    /// <summary>
    ///     Counter for tracking drag events to handle nested elements.
    /// </summary>
    private int _dragCounter;

    /// <summary>
    ///     Flag indicating whether the user is currently dragging over the drop zone.
    /// </summary>
    private volatile bool _isDragOver;

    /// <summary>
    ///     Flag indicating whether the component has been initialized.
    /// </summary>
    private volatile bool _isInitialized;

    /// <summary>
    ///     Flag indicating whether an upload operation is in progress.
    /// </summary>
    private volatile bool _isUploading;

    /// <summary>
    ///     Flag indicating whether the browser circuit is connected.
    /// </summary>
    private volatile bool _isCircuitConnected = true;

    /// <summary>
    ///     Reference to the JavaScript module for file operations.
    /// </summary>
    private IJSObjectReference? _jsModule;

    /// <summary>
    ///     Reference to the JavaScript utility module.
    /// </summary>
    private IJSObjectReference? _jsUtilsModule;

    /// <summary>
    ///     Cancellation token source for debouncing state updates.
    /// </summary>
    private CancellationTokenSource? _stateUpdateDebouncer;

    /// <summary>
    ///     Current overall upload progress percentage (0-100).
    /// </summary>
    private volatile int _uploadProgress;

    /// <summary>
    ///     Reference to the drop zone element for JavaScript interop.
    /// </summary>
    private ElementReference _dropZoneElement;

    /// <summary>
    ///     Timestamp of the last progress update to implement throttling.
    /// </summary>
    private DateTime _lastProgressUpdateTime = DateTime.MinValue;

    /// <summary>
    ///     Cache for file metadata to reduce memory usage with repeated uploads.
    /// </summary>
    private static readonly MemoryCache FileMetadataCache = new(new MemoryCacheOptions
    {
        SizeLimit = FileMetadataCacheSize
    });

    #endregion

    #region Parameters

    /// <summary>
    ///     Gets or sets the maximum allowed file size in bytes. Defaults to 10MB.
    /// </summary>
    [Parameter]
    public int MaxFileSize { get; set; } = (int)(10 * BytesPerMb);

    /// <summary>
    ///     Gets or sets the collection of allowed file types (MIME types).
    ///     If empty, all file types are allowed (except those with blocked extensions).
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

    /// <summary>
    ///     Optional flag to enable automatic retry for failed uploads.
    /// </summary>
    [Parameter]
    public bool AutoRetry { get; set; } = true;

    #endregion

    #region Properties

    /// <summary>
    ///     Indicates whether the component is ready to upload files.
    /// </summary>
    private bool CanUpload => _selectedFiles.Any() && !_isUploading && !IsDisposed && UploadFileAsync != null;

    /// <summary>
    ///     Gets a read-only view of the selected files.
    /// </summary>
    private IReadOnlyDictionary<string, UploadFile> SelectedFiles => _selectedFiles;

    /// <summary>
    ///     Gets all successfully uploaded files.
    /// </summary>
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

            CircuitStateChanged += HandleCircuitStateChanged;

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
            try
            {
                _jsModule ??= await GetJsModuleAsync(ModuleName);

                await _jsModule.InvokeVoidAsync($"{ModuleName}API.initialize");
                await _jsModule.InvokeVoidAsync($"{ModuleName}API.initGlobalDropPrevention");
                await _jsModule.InvokeVoidAsync($"{ModuleName}API.initializeDropZone", _dropZoneElement);
            }
            catch (Exception ex)
            {
                LogError("Failed to initialize drop zone", ex);
                // Non-fatal error, continue initialization
            }
        }
    }

    /// <inheritdoc />
    protected override async ValueTask InitializeComponentAsync()
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

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        CircuitStateChanged -= HandleCircuitStateChanged;
        await CleanupJavaScriptResourcesAsync();
        await base.DisposeAsync();
    }

    #endregion

    #region Event Handlers

    /// <summary>
    ///     Handles changes in the circuit connection state.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="isConnected">Whether the circuit is now connected.</param>
    private void HandleCircuitStateChanged(object? sender, bool isConnected)
    {
        if (!isConnected)
        {
            _ = CancelAllUploads();
        }

        _isCircuitConnected = isConnected;
    }

    /// <summary>
    ///     Handles the dragenter event and updates the drag state.
    /// </summary>
    /// <param name="e">Drag event arguments.</param>
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
    /// <param name="e">Drag event arguments.</param>
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
    /// <param name="e">Drag event arguments containing file information.</param>
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
                    if (_jsModule != null)
                    {
                        var proxy = await BrowserFileProxy.CreateAsync(key, _jsModule);
                        browserFiles.Add(proxy);
                    }
                    else
                    {
                        LogError("JS Module is null", new Exception("JS Module is null"));
                    }
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
    /// <param name="e">Drag event arguments.</param>
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
    /// <param name="e">Input file change event arguments.</param>
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
    ///     Optimized for memory efficiency and parallel validation.
    /// </summary>
    /// <param name="files">Collection of browser files selected for upload.</param>
    private async Task ProcessSelectedFiles(IReadOnlyList<IBrowserFile> files)
    {
        // Pre-validate all files first
        var validFiles = new List<IBrowserFile>(files.Count);
        var invalidFiles = new List<IBrowserFile>(files.Count);

        foreach (var file in files)
        {
            if (ValidateFile(file))
            {
                validFiles.Add(file);
            }
            else
            {
                invalidFiles.Add(file);
            }
        }

        // Process valid files in a batch
        foreach (var file in validFiles)
        {
            var uploadFile = new UploadFile(file.Name, file.Size, file.ContentType, file);
            _selectedFiles.TryAdd(file.Name, uploadFile);
            LogDebug("File selected: {FileName} ({Size})", file.Name, FormatFileSize(file.Size));

            // Cache file metadata for potential reuse
            CacheFileMetadata(file.Name, new { file.Size, file.ContentType, file.LastModified });
        }

        // Clean up invalid files
        foreach (var file in invalidFiles)
        {
            if (file is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }

            LogWarning("Invalid file rejected: {FileName}", file.Name);
        }
    }

    /// <summary>
    ///     Caches file metadata to reduce redundant processing.
    /// </summary>
    /// <param name="fileName">The file name to use as a cache key.</param>
    /// <param name="metadata">The metadata object to cache.</param>
    private static void CacheFileMetadata(string fileName, object metadata)
    {
        FileMetadataCache.Set(fileName, metadata,
            new MemoryCacheEntryOptions
            {
                Size = 1,
                SlidingExpiration = TimeSpan.FromMinutes(30),
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2)
            });
    }

    /// <summary>
    ///     Validates a file against size and type restrictions.
    /// </summary>
    /// <param name="file">The browser file to validate.</param>
    /// <returns>True if the file is valid; otherwise, false.</returns>
    private bool ValidateFile(IBrowserFile file)
    {
        // Check for null or disposed
        if (file == null)
        {
            LogWarning("File validation failed: File is null");
            return false;
        }

        // Check for blocked extensions
        var extension = Path.GetExtension(file.Name).ToLowerInvariant();
        if (_blockedExtensions.Contains(extension))
        {
            LogWarning("Blocked file type: {FileName} ({Extension})", file.Name, extension);
            return false;
        }

        // Check file size
        if (file.Size > MaxFileSize)
        {
            LogWarning("File too large: {FileName} ({Size})", file.Name, FormatFileSize(file.Size));
            return false;
        }

        // Check allowed types if specified
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
    /// <param name="file">The file to remove.</param>
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

                // Remove from cache if present
                FileMetadataCache.Remove(file.Name);
            }

            LogDebug("File removed: {FileName}", file.Name);
        }
        catch (Exception ex)
        {
            LogError("Failed to remove file: {FileName}", ex, file.Name);
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
    ///     Optimized for parallel uploads with throttling.
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

            var files = SelectedFiles.Values.ToList();
            var uploadTaskData = new List<(UploadFile File, CancellationTokenSource Cts)>(files.Count);

            // Prepare all uploads first
            foreach (var file in files)
            {
                var cts = new CancellationTokenSource();
                _uploadCancellationTokens[file.Name] = cts;
                uploadTaskData.Add((file, cts));
            }

            // Start with a controlled degree of parallelism for uploads
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, MaxConcurrentUploads),
                CancellationToken = ComponentToken
            };

            // Use Parallel.ForEachAsync for efficient parallelism in .NET 6+
            await Parallel.ForEachAsync(uploadTaskData, options, async (data, token) =>
            {
                await _concurrentUploadsSemaphore.WaitAsync(token);
                try
                {
                    await UploadSingleFile(data.File, data.Cts.Token);
                }
                finally
                {
                    _concurrentUploadsSemaphore.Release();
                }
            });

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
                try
                {
                    cts.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed, ignore
                }
            }

            _uploadCancellationTokens.Clear();
            _isUploading = false;
            _uploadProgress = 0;
            _uploadSemaphore.Release();
            await QueueStateUpdate();
        }
    }

    /// <summary>
    ///     Tries to upload all selected files and returns a Result indicating success or failure.
    /// </summary>
    /// <returns>A Result containing either the list of successfully uploaded files or an error.</returns>
    public async Task<Result<IReadOnlyList<UploadFile>, FileUploadError>> TryUploadFilesAsync()
    {
        if (!CanUpload)
        {
            return Result<IReadOnlyList<UploadFile>, FileUploadError>.Failure(
                FileUploadError.InvalidState("Component not ready for upload"));
        }

        try
        {
            await _uploadSemaphore.WaitAsync(ComponentToken);
            _isUploading = true;
            _uploadProgress = 0;

            var uploadedFiles = new List<UploadFile>();
            var failedFiles = new List<(UploadFile File, string Reason)>();

            // Create a unified collection of files and their cancellation tokens
            var files = SelectedFiles.Values.ToList();
            var uploadTasks = new List<Task<Result<bool, FileUploadError>>>(files.Count);

            // Prepare tasks for all files
            foreach (var file in files)
            {
                var cts = new CancellationTokenSource();
                _uploadCancellationTokens[file.Name] = cts;

                // Queue the task but don't start it yet - we'll use semaphore later
                uploadTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await _concurrentUploadsSemaphore.WaitAsync(cts.Token);
                        try
                        {
                            return await UploadFileWithResultAsync(file, cts.Token);
                        }
                        finally
                        {
                            _concurrentUploadsSemaphore.Release();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        return Result<bool, FileUploadError>.Cancelled(
                            FileUploadError.Cancelled(file.Name));
                    }
                    catch (Exception ex)
                    {
                        return Result<bool, FileUploadError>.Failure(
                            FileUploadError.UploadFailed(file.Name, ex.Message));
                    }
                }, cts.Token));
            }

            // Process all upload results
            for (var i = 0; i < files.Count; i++)
            {
                try
                {
                    var result = await uploadTasks[i];
                    if (result.IsSuccess)
                    {
                        uploadedFiles.Add(files[i]);
                    }
                    else
                    {
                        failedFiles.Add((files[i], result.Error?.Message ?? "Unknown error"));
                    }
                }
                catch (Exception ex)
                {
                    failedFiles.Add((files[i], $"Exception: {ex.Message}"));
                }
            }

            // Handle the final result
            await NotifyUploadCompletion(uploadedFiles);

            if (failedFiles.Count == 0)
            {
                return Result<IReadOnlyList<UploadFile>, FileUploadError>.Success(uploadedFiles);
            }
            else if (uploadedFiles.Count > 0)
            {
                var error = FileUploadError.BatchUploadPartialFailure(failedFiles.Count, files.Count);
                return Result<IReadOnlyList<UploadFile>, FileUploadError>.PartialSuccess(uploadedFiles, error);
            }
            else
            {
                var error = new FileUploadError("All files failed to upload: " +
                                                string.Join(", ",
                                                    failedFiles.Select(f => $"{f.File.Name}: {f.Reason}")));
                return Result<IReadOnlyList<UploadFile>, FileUploadError>.Failure(error);
            }
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<UploadFile>, FileUploadError>.Failure(
                FileUploadError.UploadFailed("batch", $"Upload operation failed: {ex.Message}"));
        }
        finally
        {
            // Cleanup code
            foreach (var cts in _uploadCancellationTokens.Values)
            {
                try
                {
                    cts.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed, ignore
                }
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
    /// <param name="file">The file to upload.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous upload operation.</returns>
    private async Task UploadSingleFile(UploadFile file, CancellationToken cancellationToken)
    {
        file.UploadStatus = UploadStatus.Uploading;
        file.UploadProgress = 0;

        try
        {
            var progress = new Progress<int>(percent =>
            {
                file.UploadProgress = percent;
                QueueProgressUpdate(percent);
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
    ///     Uploads a single file and returns a Result object.
    /// </summary>
    /// <param name="file">The file to upload.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A Result indicating success or failure.</returns>
    private async Task<Result<bool, FileUploadError>> UploadFileWithResultAsync(
        UploadFile file, CancellationToken cancellationToken)
    {
        file.UploadStatus = UploadStatus.Uploading;
        file.UploadProgress = 0;

        try
        {
            var progress = new Progress<int>(percent =>
            {
                file.UploadProgress = percent;
                QueueProgressUpdate(percent);
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
                    return Result<bool, FileUploadError>.Success(true);
                }

                LogWarning("Upload failed: {FileName} - {Status}", file.Name, result.Status);
                return Result<bool, FileUploadError>.Failure(
                    FileUploadError.UploadFailed(file.Name, $"Upload failed with status: {result.Status}"));
            }

            return Result<bool, FileUploadError>.Failure(
                FileUploadError.InvalidState("No upload handler configured"));
        }
        catch (OperationCanceledException)
        {
            file.UploadStatus = UploadStatus.Cancelled;
            LogDebug("Upload cancelled: {FileName}", file.Name);
            return Result<bool, FileUploadError>.Cancelled(
                FileUploadError.Cancelled(file.Name));
        }
        catch (Exception ex)
        {
            file.UploadStatus = UploadStatus.Failure;
            LogError("Upload error: {FileName}", ex, file.Name);
            return Result<bool, FileUploadError>.Failure(
                FileUploadError.UploadFailed(file.Name, ex.Message));
        }
    }

    /// <summary>
    ///     Retries an upload operation with exponential backoff.
    /// </summary>
    /// <param name="uploadAction">The upload function to retry.</param>
    /// <param name="maxRetries">Maximum number of retry attempts.</param>
    /// <returns>The result of the upload operation.</returns>
    /// <exception cref="Exception">Thrown when all retry attempts fail.</exception>
    private async Task<UploadResult> RetryUploadAsync(
        Func<Task<UploadResult>> uploadAction,
        int maxRetries = MaxRetryAttempts)
    {
        // Start with no delay
        var currentDelay = 0;

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                return await uploadAction();
            }
            catch (Exception ex) when (attempt < maxRetries - 1 &&
                                       ex is not OperationCanceledException)
            {
                LogWarning("Upload attempt {Attempt} failed, retrying in {Delay}ms",
                    attempt + 1, currentDelay);

                if (currentDelay > 0)
                {
                    await Task.Delay(currentDelay);
                }

                // Use exponential backoff with jitter
                currentDelay = (RetryDelayMs * (1 << attempt)) +
                               Random.Shared.Next(0, 100); // Add randomness to prevent thundering herd
            }
        }

        throw new Exception($"Upload failed after {maxRetries} attempts");
    }

    /// <summary>
    ///     Notifies completion of file uploads and cleans up successful uploads.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task NotifyUploadCompletion()
    {
        var uploadedFilesList = _uploadedFiles.ToList();

        if (OnFilesUploaded.HasDelegate)
        {
            await OnFilesUploaded.InvokeAsync(uploadedFilesList);
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

    /// <summary>
    ///     Notifies completion of specified uploaded files.
    /// </summary>
    /// <param name="uploadedFiles">List of successfully uploaded files.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task NotifyUploadCompletion(List<UploadFile> uploadedFiles)
    {
        if (OnFilesUploaded.HasDelegate && uploadedFiles.Count > 0)
        {
            await OnFilesUploaded.InvokeAsync(uploadedFiles);
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
    /// <param name="state">Timer state object (not used).</param>
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
    ///     Queues a progress update with throttling to avoid UI thrashing.
    /// </summary>
    /// <param name="progress">Progress percentage (0-100).</param>
    private void QueueProgressUpdate(int progress)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastProgressUpdateTime).TotalMilliseconds >= ProgressUpdateThrottleMs)
        {
            _progressUpdates.Enqueue(progress);
            _lastProgressUpdateTime = now;
        }
    }

    /// <summary>
    ///     Queues a state update with debouncing.
    /// </summary>
    private async Task QueueStateUpdate()
    {
        if (_stateUpdateDebouncer is not null)
        {
            try
            {
                await _stateUpdateDebouncer.CancelAsync();
            }
            catch
            {
                // Ignore cancellation errors
            }
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
    ///     Cancels all active uploads.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task CancelAllUploads()
    {
        foreach (var cts in _uploadCancellationTokens.Values.ToList())
        {
            try
            {
                await cts.CancelAsync();
            }
            catch (Exception ex)
            {
                LogDebug("Error cancelling upload: {Error}", ex.Message);
            }
        }
    }

    /// <summary>
    ///     Saves the current component state.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
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
    /// <returns>A task representing the asynchronous operation.</returns>
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

    /// <summary>
    ///     Disposes all selected files asynchronously.
    /// </summary>
    private async Task DisposeSelectedFilesAsync()
    {
        foreach (var file in _selectedFiles.Values)
        {
            if (file.File is IAsyncDisposable disposable)
            {
                try
                {
                    await disposable.DisposeAsync();
                }
                catch (Exception ex)
                {
                    LogWarning("Error disposing file: {FileName}, {Error}", file.Name, ex.Message);
                }
            }
        }

        _selectedFiles.Clear();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Formats a byte size into a human-readable string.
    /// </summary>
    /// <param name="bytes">Size in bytes.</param>
    /// <returns>Formatted size string (e.g., "1.2 MB").</returns>
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

    /// <summary>
    ///     Gets the CSS class for an upload status.
    /// </summary>
    /// <param name="status">Upload status.</param>
    /// <returns>CSS class name.</returns>
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
    /// <param name="status">Upload status.</param>
    /// <returns>CSS class for the icon.</returns>
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
