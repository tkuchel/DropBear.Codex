#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Compatibility;

/// <summary>
///     A default error type that extends <see cref="ResultError" />,
///     intended for backward-compatibility scenarios.
/// </summary>
public record DefaultError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DefaultError" /> record
    ///     with the specified message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public DefaultError(string message)
        : base(message)
    {
    }
}
