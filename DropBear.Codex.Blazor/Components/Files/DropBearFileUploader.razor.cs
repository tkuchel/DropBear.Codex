#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

#endregion

namespace DropBear.Codex.Blazor.Components.Files;

/// <summary>
///     A Blazor component for uploading files with progress and status indication.
/// </summary>
public sealed partial class DropBearFileUploader : DropBearComponentBase
{
    private const string FILE_INPUT_ID = "fileInput";
    private const long BYTES_PER_MB = 1024 * 1024;

    private readonly List<UploadFile> _selectedFiles = new();
    private readonly List<UploadFile> _uploadedFiles = new();
    private ElementReference _fileInputRef;

    private bool IsUploading { get; set; }
    private int UploadProgress { get; set; }
    private IReadOnlyList<UploadFile> SelectedFiles => _selectedFiles;
    private IReadOnlyList<UploadFile> UploadedFiles => _uploadedFiles;

    private async Task HandleFileSelectionAsync(InputFileChangeEventArgs e)
    {
        try
        {
            await ProcessSelectedFiles(e.GetMultipleFiles());
            await InvokeStateHasChangedAsync(() => Task.CompletedTask);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error processing selected files");
        }
    }

    private Task ProcessSelectedFiles(IReadOnlyList<IBrowserFile> files)
    {
        foreach (var file in files)
        {
            if (IsFileValid(file))
            {
                var uploadFile = new UploadFile(file.Name, file.Size, file.ContentType, file);
                _selectedFiles.Add(uploadFile);
                Logger.Debug("File selected: {FileName} ({FileSize})", file.Name, FormatFileSize(file.Size));
            }
            else
            {
                Logger.Warning("Invalid file rejected: {FileName}", file.Name);
            }
        }

        return Task.CompletedTask;
    }

    private bool IsFileValid(IBrowserFile file)
    {
        return file.Size <= MaxFileSize &&
               (AllowedFileTypes.Count == 0 ||
                AllowedFileTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase));
    }

    private void RemoveFile(UploadFile file)
    {
        if (IsUploading || IsDisposed)
        {
            return;
        }

        _selectedFiles.Remove(file);
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
            await ProcessFileUploads();
            await NotifyUploadCompletion();
        }
        finally
        {
            IsUploading = false;
            UploadProgress = 100;
            await InvokeStateHasChangedAsync(() => Task.CompletedTask);
        }
    }

    private async Task ProcessFileUploads()
    {
        for (var i = 0; i < _selectedFiles.Count; i++)
        {
            var file = _selectedFiles[i];
            await UploadSingleFile(file);
            UploadProgress = (int)((i + 1) / (float)_selectedFiles.Count * 100);
            StateHasChanged();
        }
    }

    private async Task UploadSingleFile(UploadFile file)
    {
        file.UploadStatus = UploadStatus.Uploading;

        try
        {
            var progress = new Progress<int>(percent =>
            {
                file.UploadProgress = percent;
                UploadProgress = (int)_selectedFiles.Average(f => f.UploadProgress);
                StateHasChanged();
            });

            var result = await UploadFileAsync!(file, progress);
            file.UploadStatus = result.Status;

            if (result.Status == UploadStatus.Success)
            {
                _uploadedFiles.Add(file);
                Logger.Debug("Upload succeeded: {FileName}", file.Name);
            }
            else
            {
                Logger.Warning("Upload failed: {FileName}", file.Name);
            }
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

    private async Task OpenFileDialog()
    {
        try
        {
            await SafeJsVoidInteropAsync("clickElementById", FILE_INPUT_ID);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error opening file dialog");
        }
    }

    #region Parameters

    /// <summary>
    ///     The maximum allowed file size in bytes (defaults to 10 MB).
    /// </summary>
    [Parameter]
    public int MaxFileSize { get; set; } = (int)(10 * BYTES_PER_MB);

    /// <summary>
    ///     The list of allowed file types (MIME types).
    /// </summary>
    [Parameter]
    public IReadOnlyCollection<string> AllowedFileTypes { get; set; } = Array.Empty<string>();

    /// <summary>
    ///     Event callback triggered after files are uploaded.
    /// </summary>
    [Parameter]
    public EventCallback<List<UploadFile>> OnFilesUploaded { get; set; }

    /// <summary>
    ///     The delegate responsible for uploading each file.
    /// </summary>
    [Parameter]
    public Func<UploadFile, IProgress<int>, Task<UploadResult>>? UploadFileAsync { get; set; }

    #endregion
}
