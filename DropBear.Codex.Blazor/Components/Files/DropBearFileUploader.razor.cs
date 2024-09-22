#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Notifications.Services;
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
    private bool _isDragOver;
    private bool _isUploading;
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
            Logger.Information("Calling DropBearFileUploader.getDroppedFiles");
            var files = await JsRuntime.InvokeAsync<List<DroppedFile>>("DropBearFileUploader.getDroppedFiles",
                _dismissCancellationTokenSource.Token);
            Logger.Information("JavaScript call completed, processing files");

            foreach (var file in files)
            {
                if (IsFileValid(file))
                {
                    Logger.Information("File added: {FileName} with size {FileSize}", file.Name, FormatFileSize(file.Size));

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
            Logger.Information("Clearing dropped files and updating UI");
            await JsRuntime.InvokeVoidAsync("DropBearFileUploader.clearDroppedFiles", _dismissCancellationTokenSource.Token);
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
                Logger.Information("File selected: {FileName} with size {FileSize}", file.Name,
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
        Logger.Information("File removed: {FileName}", file.Name);
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
                        Logger.Information("File uploaded successfully: {FileName}", file.Name);
                    }
                    else
                    {
                        Logger.Warning("File upload failed: {FileName}", file.Name);
                    }
                }
                else
                {
                    // Fallback simulated upload
                    if (_dismissCancellationTokenSource is not null)
                    {
                        await Task.Delay(1000, _dismissCancellationTokenSource.Token);
                    }

                    file.UploadStatus = Random.Shared.Next(10) < 8 ? UploadStatus.Success : UploadStatus.Failure;

                    if (file.UploadStatus == UploadStatus.Success)
                    {
                        _uploadedFiles.Add(file);
                        Logger.Information("Simulated file upload successful: {FileName}", file.Name);
                    }
                    else
                    {
                        Logger.Warning("Simulated file upload failed: {FileName}", file.Name);
                    }
                }
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

        Logger.Information("File upload process completed.");
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
