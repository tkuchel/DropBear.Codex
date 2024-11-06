#region

using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Core;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Compatibility;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents the result of an upload operation.
/// </summary>
public sealed class UploadResult : Result<string>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="UploadResult" /> class.
    /// </summary>
    /// <param name="status">The status of the upload operation.</param>
    /// <param name="message">The message associated with the upload result.</param>
    public UploadResult(UploadStatus status, string message)
        : base(message, message, null, MapResultState(status))
    {
        Status = status;
    }

    /// <summary>
    ///     Gets the status of the upload operation.
    /// </summary>
    public UploadStatus Status { get; }

    /// <summary>
    ///     Gets the message associated with the upload result.
    /// </summary>
    public string Message => Value;

    /// <summary>
    ///     Maps the <see cref="UploadStatus" /> to a corresponding <see cref="ResultState" />.
    /// </summary>
    /// <param name="status">The upload status.</param>
    /// <returns>The corresponding result state.</returns>
    private static ResultState MapResultState(UploadStatus status) =>
        status switch
        {
            UploadStatus.Success => ResultState.Success,
            UploadStatus.Failure => ResultState.Failure,
            UploadStatus.Warning => ResultState.PartialSuccess,
            _ => ResultState.Pending
        };
}
