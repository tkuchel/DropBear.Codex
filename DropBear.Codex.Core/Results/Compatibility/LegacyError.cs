#region

using System.Diagnostics.CodeAnalysis;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Compatibility;

/// <summary>
///     Legacy error type for backwards compatibility.
///     Use custom error types derived from ResultError instead.
/// </summary>
[Obsolete("Use custom error types derived from ResultError instead. This type will be removed in a future version.",
    DiagnosticId = "DROPBEAR001")]
[ExcludeFromCodeCoverage] // Legacy code
public sealed record LegacyError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of LegacyError.
    /// </summary>
    /// <param name="message">The error message.</param>
    public LegacyError(string message) : base(message)
    {
    }

    /// <summary>
    ///     Creates a LegacyError from a custom error type.
    ///     Helper for migration scenarios.
    /// </summary>
    public static LegacyError FromError<TError>(TError error)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(error);

        var legacy = new LegacyError(error.Message);

        // Copy metadata if present
        if (error.Metadata != null)
        {
            legacy = (LegacyError)legacy.WithMetadata(error.Metadata);
        }

        return legacy;
    }

    /// <summary>
    ///     Converts this LegacyError to a custom error type.
    ///     Helper for migration scenarios.
    /// </summary>
    public TError ToError<TError>()
        where TError : ResultError
    {
        var error = (TError)Activator.CreateInstance(typeof(TError), Message)!;

        // Copy metadata if present
        if (Metadata != null)
        {
            error = (TError)error.WithMetadata(Metadata);
        }

        return error;
    }
}
