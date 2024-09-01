#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Files;

/// <summary>
///     A Blazor component for uploading files with drag-and-drop support and progress indication.
/// </summary>
public sealed partial class DropBearFileUploader : DropBearComponentBase, IDisposable
{
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

    public void Dispose()
    {
        _dismissCancellationTokenSource?.Dispose();
    }

    private async Task HandleDrop()
    {
        _isDragOver = false;
        await HandleDroppedFiles();
    }

    private async Task HandleDroppedFiles()
    {
        _dismissCancellationTokenSource = new CancellationTokenSource();
        var files = await JSRuntime.InvokeAsync<List<DroppedFile>>("DropBearFileUploader.getDroppedFiles",
            _dismissCancellationTokenSource.Token);

        foreach (var uploadFile in from file in files
                 where IsFileValid(file)
                 select new UploadFile
                 {
                     Name = file.Name, Size = file.Size, ContentType = file.Type, UploadStatus = UploadStatus.Ready
                 })
        {
            _selectedFiles.Add(uploadFile);
        }

        await JSRuntime.InvokeVoidAsync("DropBearFileUploader.clearDroppedFiles",
            _dismissCancellationTokenSource.Token);
        StateHasChanged();
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    private async Task HandleFileSelection(InputFileChangeEventArgs e)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
        foreach (var file in e.GetMultipleFiles())
        {
            if (!IsFileValid(file))
            {
                continue;
            }

            var uploadFile = new UploadFile
            {
                Name = file.Name,
                Size = file.Size,
                ContentType = file.ContentType,
                UploadStatus = UploadStatus.Ready,
                FileData = file
            };

            _selectedFiles.Add(uploadFile);
        }

        StateHasChanged();
    }

    private bool IsFileValid(IBrowserFile file)
    {
        return IsFileValid(new DroppedFile { Name = file.Name, Size = file.Size, Type = file.ContentType });
    }

    private bool IsFileValid(DroppedFile file)
    {
        if (file.Size > MaxFileSize)
        {
            // You might want to show an error message to the user here
            return false;
        }

        return AllowedFileTypes.Count == 0 || AllowedFileTypes.Contains(file.Type, StringComparer.OrdinalIgnoreCase);
    }

    private void RemoveFile(UploadFile file)
    {
        _selectedFiles.Remove(file);
        StateHasChanged();
    }

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
                        StateHasChanged();
                    });

                    var result = await UploadFileAsync(file, progress);
                    file.UploadStatus = result.Status;
                    if (result.Status == UploadStatus.Success)
                    {
                        _uploadedFiles.Add(file);
                    }
                }
                else
                {
                    // Fallback to simulated upload if no upload function is provided
                    if (_dismissCancellationTokenSource is not null)
                    {
                        await Task.Delay(1000, _dismissCancellationTokenSource.Token);
                    }

#pragma warning disable CA5394
                    file.UploadStatus = Random.Shared.Next(10) < 8 ? UploadStatus.Success : UploadStatus.Failure;
#pragma warning restore CA5394

                    if (file.UploadStatus == UploadStatus.Success)
                    {
                        _uploadedFiles.Add(file);
                    }
                }
            }
            catch
            {
                file.UploadStatus = UploadStatus.Failure;
            }

            _uploadProgress = (int)((i + 1) / (float)_selectedFiles.Count * 100);
            StateHasChanged();
        }

        await OnFilesUploaded.InvokeAsync(_uploadedFiles);

        _isUploading = false;
        _uploadProgress = 100;

        // Remove successfully uploaded files from the selected files list
        _selectedFiles.RemoveAll(f => f.UploadStatus == UploadStatus.Success);

        StateHasChanged();
    }

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
