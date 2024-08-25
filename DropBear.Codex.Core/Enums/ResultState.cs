namespace DropBear.Codex.Core.Enums;

/// <summary>
///     Represents the various states that a result can have in an operation.
/// </summary>
public enum ResultState
{
    /// <summary>
    ///     The operation was successful.
    /// </summary>
    Success,

    /// <summary>
    ///     The operation failed.
    /// </summary>
    Failure,

    /// <summary>
    ///     The operation is still ongoing, or its result is yet to be determined.
    /// </summary>
    Pending,

    /// <summary>
    ///     The operation was cancelled before completion.
    /// </summary>
    Cancelled,

    /// <summary>
    ///     The operation succeeded but with potential issues that should be checked.
    /// </summary>
    Warning,

    /// <summary>
    ///     The operation succeeded partially, with some aspects failing.
    /// </summary>
    PartialSuccess,

    /// <summary>
    ///     No operation was performed.
    /// </summary>
    NoOp
}
