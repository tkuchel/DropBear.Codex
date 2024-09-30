#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Files;

/// <summary>
///     A Blazor component for uploading files with drag-and-drop support and progress indication.
/// </summary>
public sealed partial class DropBearFileUploader : DropBearComponentBase, IDisposable
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearFileUploader>();

    private readonly List<UploadFile> _selectedFiles = new();
    private readonly List<UploadFile> _uploadedFiles = new();
    private CancellationTokenSource? _dismissCancellationTokenSource;

    private readonly Dictionary<string, (List<byte[]> Chunks, int CurrentProgress, long FileSize)> _fileUploadChunks =
        new();

    private bool _isDragOver;
    private bool _isUploading;
    private int _totalChunks; // Track total number of chunks for progress calculation
    private int _uploadProgress;

    [Parameter] public int MaxFileSize { get; set; } = 10 * 1024 * 1024; // 10MB default
    [Parameter] public IReadOnlyCollection<string> AllowedFileTypes { get; set; } = Array.Empty<string>();
    [Parameter] public EventCallback<List<UploadFile>> OnFilesUploaded { get; set; }
    [Parameter] public Func<UploadFile, IProgress<int>, Task<UploadResult>>? UploadFileAsync { get; set; }

    /// <summary>
    ///     Clean up resources on disposal.
    /// </summary>
    public void Dispose()
    {
        _dismissCancellationTokenSource?.Dispose();
    }

    /// <summary>
    ///     Handles the drop event for drag-and-drop file uploads.
    /// </summary>
    private async Task HandleDrop()
    {
        _isDragOver = false;
        await HandleDroppedFiles();
    }

    /// <summary>
    ///     Handles processing of dropped files from JavaScript interop.
    /// </summary>
    private async Task HandleDroppedFiles()
    {
        _dismissCancellationTokenSource = new CancellationTokenSource();

        try
        {
            Logger.Debug("Calling DropBearFileUploader.getDroppedFiles");
            var files = await JsRuntime.InvokeAsync<List<DroppedFile>>("DropBearFileUploader.getDroppedFiles",
                _dismissCancellationTokenSource.Token);
            Logger.Debug("JavaScript call completed, processing files");

            foreach (var file in files)
            {
                if (IsFileValid(file))
                {
                    Logger.Debug("File added: {FileName} with size {FileSize}", file.Name,
                        FormatFileSize(file.Size));

                    var uploadFile = new UploadFile(
                        file.Name,
                        file.Size,
                        file.Type,
                        droppedFileData: file.Data);

                    _selectedFiles.Add(uploadFile);
                }
                else
                {
                    Logger.Warning("File rejected: {FileName} due to validation failure", file.Name);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling dropped files.");
        }
        finally
        {
            Logger.Debug("Clearing dropped files and updating UI");
            await JsRuntime.InvokeVoidAsync("DropBearFileUploader.clearDroppedFiles",
                _dismissCancellationTokenSource.Token);
            StateHasChanged();
        }
    }


#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    private async Task HandleFileSelection(InputFileChangeEventArgs e)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
        foreach (var file in e.GetMultipleFiles())
        {
            if (IsFileValid(file))
            {
                // Creating new UploadFile instances using the constructor
                var uploadFile = new UploadFile(file.Name, file.Size, file.ContentType, file);

                _selectedFiles.Add(uploadFile);
                Logger.Debug("File selected: {FileName} with size {FileSize}", file.Name,
                    FormatFileSize(file.Size));
            }
            else
            {
                Logger.Warning("File rejected: {FileName} due to validation failure", file.Name);
            }
        }

        StateHasChanged();
    }

    /// <summary>
    ///     Validates a file's size and type against allowed parameters.
    /// </summary>
    private bool IsFileValid(IBrowserFile file)
    {
        // Create a DroppedFile instance using the constructor, then validate it
        var droppedFile = new DroppedFile(file.Name, file.Size, file.ContentType, null);
        return IsFileValid(droppedFile);
    }

    /// <summary>
    ///     Validates a dropped file's size and type against allowed parameters.
    /// </summary>
    private bool IsFileValid(DroppedFile file)
    {
        if (file.Size > MaxFileSize)
        {
            Logger.Warning("File {FileName} exceeds maximum size limit of {MaxFileSize}", file.Name,
                FormatFileSize(MaxFileSize));
            return false;
        }

        if (AllowedFileTypes.Count > 0 && !AllowedFileTypes.Contains(file.Type, StringComparer.OrdinalIgnoreCase))
        {
            Logger.Warning("File {FileName} has unsupported file type {FileType}", file.Name, file.Type);
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Removes a file from the selected files list.
    /// </summary>
    private void RemoveFile(UploadFile file)
    {
        _selectedFiles.Remove(file);
        Logger.Debug("File removed: {FileName}", file.Name);
        StateHasChanged();
    }

    /// <summary>
    ///     Uploads file chunks received via JavaScript interop.
    /// </summary>
    /// <param name="fileName">The name of the file being uploaded.</param>
    /// <param name="chunk">The chunk of file data (as a byte array) being uploaded.</param>
    /// <param name="totalChunks">The total number of chunks for this file.</param>
    /// <param name="fileSize">The total size of the file.</param>
    [JSInvokable]
    public async Task UploadFileChunk(string fileName, byte[] chunk, int totalChunks, long fileSize)
    {
        _totalChunks = totalChunks;

        // Initialize chunk list if this is the first chunk for the file
        if (!_fileUploadChunks.ContainsKey(fileName))
        {
            _fileUploadChunks[fileName] = (new List<byte[]>(), 0, fileSize);
            Logger.Debug($"Started receiving chunks for file: {fileName} (size: {fileSize} bytes)");
        }

        // Add the current chunk to the file's chunk list
        _fileUploadChunks[fileName].Chunks.Add(chunk);
        _fileUploadChunks[fileName] = (_fileUploadChunks[fileName].Chunks,
            _fileUploadChunks[fileName].CurrentProgress + 1, fileSize);

        Logger.Debug(
            $"Received chunk {chunk.Length} bytes for file {fileName} ({_fileUploadChunks[fileName].CurrentProgress}/{totalChunks} chunks)");

        // Check if all chunks have been received
        if (_fileUploadChunks[fileName].CurrentProgress == totalChunks)
        {
            Logger.Debug($"All chunks received for file: {fileName}. Merging and executing callback...");

            try
            {
                // Combine all chunks into a single byte array
                var completeFileData = MergeFileChunks(_fileUploadChunks[fileName].Chunks, fileSize);

                // Create an UploadFile object
                var uploadFile = new UploadFile(
                    fileName,
                    fileSize,
                    "application/octet-stream", // Adjust MIME type as needed
                    null, // Not using IBrowserFile, so set to null
                    completeFileData // Pass the merged byte[] as droppedFileData
                );

                // Track upload progress
                var progress = new Progress<int>(percent =>
                {
                    uploadFile.UploadProgress = percent;
                    _uploadProgress = (int)(_fileUploadChunks[fileName].CurrentProgress / (float)_totalChunks * 100);
                    Logger.Debug("File upload progress: {FileName} {Progress}%", fileName, percent);
                    StateHasChanged();
                });

                // Execute the UploadFileAsync callback after merging
                if (UploadFileAsync is not null)
                {
                    var result = await UploadFileAsync(uploadFile, progress);
                    uploadFile.UploadStatus = result.Status;

                    if (result.Status == UploadStatus.Success)
                    {
                        _uploadedFiles.Add(uploadFile);
                        Logger.Debug("File uploaded successfully: {FileName}", fileName);
                    }
                    else
                    {
                        Logger.Debug("File upload failed: {FileName}", fileName);
                    }
                }

                // Clean up the chunk list for this file
                _fileUploadChunks.Remove(fileName);

                // Optionally update progress
                _uploadProgress = 100;
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error finalizing file upload for {fileName}");
            }
        }
        else
        {
            // Update progress based on the received chunks
            var currentProgress = (int)(_fileUploadChunks[fileName].CurrentProgress / (float)_totalChunks * 100);
            Logger.Debug($"Upload progress for file {fileName}: {currentProgress}%");

            // Update file progress visually if needed
            _uploadProgress = currentProgress;
            StateHasChanged();
        }
    }

    /// <summary>
    ///     Merges the file chunks into a single byte array.
    /// </summary>
    /// <param name="chunks">The list of file chunks to merge.</param>
    /// <param name="fileSize">The total size of the final file.</param>
    /// <returns>A byte array containing the merged file data.</returns>
    private byte[] MergeFileChunks(List<byte[]> chunks, long fileSize)
    {
        var mergedFileData = new byte[fileSize];
        var offset = 0;

        foreach (var chunk in chunks)
        {
            Buffer.BlockCopy(chunk, 0, mergedFileData, offset, chunk.Length);
            offset += chunk.Length;
        }

        Logger.Debug($"Merged {chunks.Count} chunks into a single file of size {fileSize} bytes.");
        return mergedFileData;
    }

    /// <summary>
    ///     Cleans up uploaded files after completion.
    /// </summary>
    private void CleanupUploadedFiles()
    {
        _selectedFiles.RemoveAll(f => f.UploadStatus == UploadStatus.Success);
        Logger.Debug("Cleanup of successfully uploaded files complete.");
        StateHasChanged();
    }


    /// <summary>
    ///     Uploads the selected files with progress tracking.
    /// </summary>
    private async Task UploadFiles()
    {
        _isUploading = true;
        _uploadProgress = 0;

        for (var i = 0; i < _selectedFiles.Count; i++)
        {
            var file = _selectedFiles[i];
            file.UploadStatus = UploadStatus.Uploading;

            try
            {
                if (UploadFileAsync is not null)
                {
                    var progress = new Progress<int>(percent =>
                    {
                        file.UploadProgress = percent;
                        _uploadProgress =
                            (int)(_selectedFiles.Sum(f => f.UploadProgress) / (float)_selectedFiles.Count);
                        Logger.Debug("File upload progress: {FileName} {Progress}%", file.Name, percent);
                        StateHasChanged();
                    });

                    var result = await UploadFileAsync(file, progress);
                    file.UploadStatus = result.Status;
                    if (result.Status == UploadStatus.Success)
                    {
                        _uploadedFiles.Add(file);
                        Logger.Debug("File uploaded successfully: {FileName}", file.Name);
                    }
                    else
                    {
                        Logger.Warning("File upload failed: {FileName}", file.Name);
                    }
                }

                Logger.Warning("File upload failed: {FileName}", file.Name);
            }
            catch (Exception ex)
            {
                file.UploadStatus = UploadStatus.Failure;
                Logger.Error(ex, "Error uploading file: {FileName}", file.Name);
            }

            _uploadProgress = (int)((i + 1) / (float)_selectedFiles.Count * 100);
            StateHasChanged();
        }

        await OnFilesUploaded.InvokeAsync(_uploadedFiles);

        _isUploading = false;
        _uploadProgress = 100;

        // Remove successfully uploaded files from the selected files list
        _selectedFiles.RemoveAll(f => f.UploadStatus == UploadStatus.Success);

        Logger.Debug("File upload process completed.");
        StateHasChanged();
    }

    /// <summary>
    ///     Formats the file size in a human-readable format.
    /// </summary>
    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        var order = 0;
        while (bytes >= 1024 && order < sizes.Length - 1)
        {
            order++;
            bytes /= 1024;
        }

        return $"{bytes:0.##} {sizes[order]}";
    }

    /// <summary>
    ///     Retrieves the appropriate icon class for a file's upload status.
    /// </summary>
    private static string GetFileStatusIconClass(UploadStatus status)
    {
        return status switch
        {
            UploadStatus.Success => "fas fa-check-circle text-success",
            UploadStatus.Failure => "fas fa-times-circle text-danger",
            UploadStatus.Warning => "fas fa-exclamation-circle text-warning",
            _ => "fas fa-question-circle text-muted"
        };
    }
}
