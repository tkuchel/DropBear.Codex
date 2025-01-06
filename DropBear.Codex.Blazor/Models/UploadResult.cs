#region

using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Compatibility;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents the result of an upload operation,
///     extending a <see cref="Result{T}" /> with <see cref="UploadStatus" />.
/// </summary>
public sealed class UploadResult : Result<string>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="UploadResult" /> class.
    /// </summary>
    /// <param name="status">The status of the upload operation (e.g., Success, Failure).</param>
    /// <param name="message">A message describing the upload result.</param>
    public UploadResult(UploadStatus status, string message)
        : base(message, message, null, MapResultState(status))
    {
        Status = status;
    }

    /// <summary>
    ///     Gets the status of the upload operation (e.g., Ready, InProgress, Success, Failure, etc.).
    /// </summary>
    public UploadStatus Status { get; }

    /// <summary>
    ///     Gets the message associated with the upload result.
    ///     Internally, it's the same as <see cref="Result{T}.Value" /> from the base class.
    /// </summary>
    public string Message => Value;

    /// <summary>
    ///     Maps an <see cref="UploadStatus" /> to a <see cref="ResultState" />.
    /// </summary>
    /// <param name="status">The <see cref="UploadStatus" /> to map.</param>
    /// <returns>The corresponding <see cref="ResultState" />.</returns>
    private static ResultState MapResultState(UploadStatus status)
    {
        return status switch
        {
            UploadStatus.Success => ResultState.Success,
            UploadStatus.Failure => ResultState.Failure,
            UploadStatus.Warning => ResultState.PartialSuccess,
            _ => ResultState.Pending
        };
    }
}
