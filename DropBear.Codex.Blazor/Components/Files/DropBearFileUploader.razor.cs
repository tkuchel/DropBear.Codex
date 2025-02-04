﻿#region

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
///     A Blazor component for uploading files with progress and status indication.
/// </summary>
public sealed partial class DropBearFileUploader : DropBearComponentBase, IDisposable
{
    private new static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearFileUploader>();

    private readonly List<UploadFile> _selectedFiles = new();
    private readonly List<UploadFile> _uploadedFiles = new();

    private ElementReference _fileInputRef;

    /// <summary>
    ///     The maximum allowed file size in bytes (defaults to 10 MB).
    /// </summary>
    [Parameter]
    public int MaxFileSize { get; set; } = 10 * 1024 * 1024; // 10MB

    /// <summary>
    ///     The list of allowed file types (MIME types).
    ///     If empty, all types are allowed.
    /// </summary>
    [Parameter]
    public IReadOnlyCollection<string> AllowedFileTypes { get; set; } = Array.Empty<string>();

    /// <summary>
    ///     Event callback triggered after files are uploaded.
    /// </summary>
    [Parameter]
    public EventCallback<List<UploadFile>> OnFilesUploaded { get; set; }

    /// <summary>
    ///     The delegate responsible for uploading each file (server logic).
    /// </summary>
    [Parameter]
    public Func<UploadFile, IProgress<int>, Task<UploadResult>>? UploadFileAsync { get; set; }

    /// <summary>
    ///     Indicates whether any file upload operation is currently in progress.
    /// </summary>
    private bool IsUploading { get; set; }

    /// <summary>
    ///     Represents the overall upload progress (0..100).
    /// </summary>
    private int UploadProgress { get; set; }

    /// <summary>
    ///     Gets the list of currently selected files (not yet uploaded).
    /// </summary>
    private IReadOnlyList<UploadFile> SelectedFiles => _selectedFiles;

    /// <summary>
    ///     Gets the list of successfully uploaded files.
    /// </summary>
    private IReadOnlyList<UploadFile> UploadedFiles => _uploadedFiles;

    /// <summary>
    ///     Disposes any necessary resources when the component is destroyed.
    /// </summary>
    public void Dispose()
    {
        // Add any cleanup logic here if needed
    }

    /// <summary>
    ///     Handles file selection from the file input. Validates and adds them to the list of selected files.
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
    ///     Validates a file's size and content type against <see cref="MaxFileSize" /> and <see cref="AllowedFileTypes" />.
    /// </summary>
    private bool IsFileValid(IBrowserFile file)
    {
        var withinSizeLimit = file.Size <= MaxFileSize;
        var allowedType = AllowedFileTypes.Count == 0 ||
                          AllowedFileTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase);

        return withinSizeLimit && allowedType;
    }

    /// <summary>
    ///     Removes a file from the currently selected list.
    /// </summary>
    private void RemoveFile(UploadFile file)
    {
        _selectedFiles.Remove(file);
        Logger.Debug("Removed file: {FileName}", file.Name);
        StateHasChanged();
    }

    /// <summary>
    ///     Initiates upload for all selected files by calling <see cref="UploadFileAsync" />.
    /// </summary>
    private async Task UploadFilesAsync()
    {
        if (UploadFileAsync is null)
        {
            Logger.Warning("UploadFileAsync delegate is null; cannot upload files.");
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
                // Set up per-file progress
                var progress = new Progress<int>(percent =>
                {
                    file.UploadProgress = percent;
                    UploadProgress = (int)_selectedFiles.Average(f => f.UploadProgress);
                    StateHasChanged();
                });

                // Perform the actual file upload
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

            // Recalculate overall progress
            UploadProgress = (int)((i + 1) / (float)_selectedFiles.Count * 100);
            StateHasChanged();
        }

        // Inform the parent that files have been uploaded
        await OnFilesUploaded.InvokeAsync(_uploadedFiles.ToList());

        IsUploading = false;
        UploadProgress = 100;

        // Remove successfully uploaded files from the selected list
        _selectedFiles.RemoveAll(f => f.UploadStatus == UploadStatus.Success);

        Logger.Debug("File upload process completed.");
        StateHasChanged();
    }

    /// <summary>
    ///     Converts a file size (in bytes) to a human-readable string (B, KB, MB, etc.).
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
    ///     Returns a CSS icon class representing the file's upload status.
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
    ///     Opens the file dialog via JS interop by programmatically clicking the hidden file input.
    /// </summary>
    private async Task OpenFileDialog()
    {
        await JsRuntime.InvokeVoidAsync("clickElementById", "fileInput");
    }
}
