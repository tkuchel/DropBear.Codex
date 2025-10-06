#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Errors;

/// <summary>
///     An error with a specific error code for categorization.
/// </summary>
public sealed record CodedError : ResultError
{
    /// <summary>
    ///     Initializes a new CodedError.
    /// </summary>
    public CodedError(string message, string code) : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Message = message;
        Code = code;
    }

    /// <summary>
    ///     Creates a new CodedError.
    /// </summary>
    public static CodedError Create(string code, string message) => new(message, code);
}
