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

public partial class DropBearFileUploader : DropBearComponentBase, IDisposable
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearFileUploader>();

    private readonly List<UploadFile> _selectedFiles = new();
    private readonly List<UploadFile> _uploadedFiles = new();
    private ElementReference _fileInputRef;
    [Parameter] public int MaxFileSize { get; set; } = 10 * 1024 * 1024; // 10MB default

    [Parameter] public IReadOnlyCollection<string> AllowedFileTypes { get; set; } = Array.Empty<string>();

    [Parameter] public EventCallback<List<UploadFile>> OnFilesUploaded { get; set; }

    [Parameter] public Func<UploadFile, IProgress<int>, Task<UploadResult>>? UploadFileAsync { get; set; }

    /// <summary>
    ///     Gets a value indicating whether files are currently being uploaded.
    /// </summary>
    private bool IsUploading { get; set; }

    /// <summary>
    ///     Gets the current upload progress percentage.
    /// </summary>
    private int UploadProgress { get; set; }

    /// <summary>
    ///     Gets the list of selected files.
    /// </summary>
    private IReadOnlyList<UploadFile> SelectedFiles => _selectedFiles;

    /// <summary>
    ///     Gets the list of successfully uploaded files.
    /// </summary>
    private IReadOnlyList<UploadFile> UploadedFiles => _uploadedFiles;

    /// <summary>
    ///     Clean up resources on disposal.
    /// </summary>
    public void Dispose()
    {
        // Implement any necessary disposal logic here
    }

    /// <summary>
    ///     Handles file selection via the file input.
    /// </summary>
    private async Task HandleFileSelectionAsync(InputFileChangeEventArgs e)
    {
        foreach (var file in e.GetMultipleFiles())
        {
            if (IsFileValid(file))
            {
                var uploadFile = new UploadFile(file.Name, file.Size, file.ContentType, file);
                _selectedFiles.Add(uploadFile);
                Logger.Debug("Selected file: {FileName} ({FileSize})", file.Name, FormatFileSize(file.Size));
            }
            else
            {
                Logger.Warning("File {FileName} is invalid and was not added.", file.Name);
            }
        }

        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    ///     Validates a file's size and type against allowed parameters.
    /// </summary>
    private bool IsFileValid(IBrowserFile file)
    {
        return file.Size <= MaxFileSize &&
               (AllowedFileTypes.Count == 0 ||
                AllowedFileTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Removes a file from the selected files list.
    /// </summary>
    private void RemoveFile(UploadFile file)
    {
        _selectedFiles.Remove(file);
        Logger.Debug("Removed file: {FileName}", file.Name);
        StateHasChanged();
    }

    /// <summary>
    ///     Uploads the selected files with progress tracking.
    /// </summary>
    private async Task UploadFilesAsync()
    {
        if (UploadFileAsync is null)
        {
            Logger.Warning("UploadFileAsync delegate is null. Cannot upload files.");
            return;
        }

        IsUploading = true;
        UploadProgress = 0;

        for (var i = 0; i < _selectedFiles.Count; i++)
        {
            var file = _selectedFiles[i];
            file.UploadStatus = UploadStatus.Uploading;

            try
            {
                var progress = new Progress<int>(percent =>
                {
                    file.UploadProgress = percent;
                    UploadProgress = (int)_selectedFiles.Average(f => f.UploadProgress);
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
            catch (Exception ex)
            {
                file.UploadStatus = UploadStatus.Failure;
                Logger.Error(ex, "Error uploading file: {FileName}", file.Name);
            }

            UploadProgress = (int)((i + 1) / (float)_selectedFiles.Count * 100);
            StateHasChanged();
        }

        await OnFilesUploaded.InvokeAsync(_uploadedFiles.ToList());

        IsUploading = false;
        UploadProgress = 100;

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
        double len = bytes;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
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

    /// <summary>
    ///     Opens the file dialog to select files.
    /// </summary>
    private async Task OpenFileDialog()
    {
        await JsRuntime.InvokeVoidAsync("clickElement", _fileInputRef);
    }
}
