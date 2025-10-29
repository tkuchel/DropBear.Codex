using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Errors;
using DropBear.Codex.Core;
using DropBear.Codex.Core.Results.Base;

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents the result of an upload operation using the Result pattern.
///     Provides backward compatibility while leveraging Core's Result type.
/// </summary>
public sealed class UploadResult
{
    private readonly Result<Unit, FileUploadError> _result;

    /// <summary>
    ///     Initializes a new instance of the <see cref="UploadResult" /> class.
    /// </summary>
    /// <param name="result">The underlying result.</param>
    /// <param name="status">The upload status for UI tracking.</param>
    private UploadResult(Result<Unit, FileUploadError> result, UploadStatus status)
    {
        _result = result;
        Status = status;
    }

    /// <summary>
    ///     Gets the status of the upload operation (e.g., Ready, InProgress, Success, Failure, etc.).
    /// </summary>
    public UploadStatus Status { get; }

    /// <summary>
    ///     Gets the message associated with the upload result.
    /// </summary>
    public string Message => _result.IsSuccess ? "Upload completed successfully" : _result.Error?.Message ?? string.Empty;

    /// <summary>
    ///     Gets a value indicating whether the upload was successful.
    /// </summary>
    public bool IsSuccess => _result.IsSuccess;

    /// <summary>
    ///     Gets a value indicating whether the upload failed.
    /// </summary>
    public bool IsFailure => _result.IsSuccess == false;

    /// <summary>
    ///     Gets the underlying Result for advanced scenarios.
    /// </summary>
    public Result<Unit, FileUploadError> Result => _result;

    /// <summary>
    ///     Creates a successful upload result.
    /// </summary>
    /// <param name="message">The success message (optional, not used in Result-based approach).</param>
    /// <returns>A new <see cref="UploadResult" /> instance.</returns>
    public static UploadResult Success(string message = "Upload completed successfully")
    {
        return new UploadResult(Result<Unit, FileUploadError>.Success(Unit.Value), UploadStatus.Success);
    }

    /// <summary>
    ///     Creates a failed upload result.
    /// </summary>
    /// <param name="message">The failure message.</param>
    /// <returns>A new <see cref="UploadResult" /> instance.</returns>
    public static UploadResult Failure(string message)
    {
        var error = new FileUploadError(message);
        return new UploadResult(Result<Unit, FileUploadError>.Failure(error), UploadStatus.Failure);
    }

    /// <summary>
    ///     Creates a failed upload result with a specific error.
    /// </summary>
    /// <param name="error">The upload error.</param>
    /// <returns>A new <see cref="UploadResult" /> instance.</returns>
    public static UploadResult Failure(FileUploadError error)
    {
        return new UploadResult(Result<Unit, FileUploadError>.Failure(error), UploadStatus.Failure);
    }

    /// <summary>
    ///     Creates an uploading status result.
    /// </summary>
    /// <param name="message">The progress message.</param>
    /// <returns>A new <see cref="UploadResult" /> instance.</returns>
    public static UploadResult Uploading(string message = "Upload in progress")
    {
        // Uploading is a transient state, we use Success result but with Uploading status
        return new UploadResult(Result<Unit, FileUploadError>.Success(Unit.Value), UploadStatus.Uploading);
    }

    /// <summary>
    ///     Creates a cancelled upload result.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <returns>A new <see cref="UploadResult" /> instance.</returns>
    public static UploadResult Cancelled(string fileName)
    {
        var error = FileUploadError.Cancelled(fileName);
        return new UploadResult(Result<Unit, FileUploadError>.Cancelled(error), UploadStatus.Cancelled);
    }

    /// <summary>
    ///     Creates an UploadResult from a Core Result.
    /// </summary>
    /// <param name="result">The core result.</param>
    /// <returns>A new <see cref="UploadResult" /> instance.</returns>
    public static UploadResult FromResult(Result<Unit, FileUploadError> result)
    {
        var status = result.State switch
        {
            Core.Enums.ResultState.Success => UploadStatus.Success,
            Core.Enums.ResultState.Cancelled => UploadStatus.Cancelled,
            Core.Enums.ResultState.Warning => UploadStatus.Warning,
            _ => UploadStatus.Failure
        };

        return new UploadResult(result, status);
    }
}
