#region

using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Base;
#pragma warning disable CS1574, CS1584, CS1581, CS1580

#endregion

namespace DropBear.Codex.Core.Results.Extensions;

/// <summary>
///     Extension helpers for interrogating a <see cref="ResultError" />'s <see cref="ErrorSeverity" />.
/// </summary>
/// <remarks>
///     These methods are simple value checks and do not mutate state.
///     If the error instance is null, a <see cref="NullReferenceException" /> will occur when accessing its members.
/// </remarks>
/// <example>
///     <code><![CDATA[
///     if (error.IsCritical() || error.IsHigh())
///     {
///         // escalate / alert
///     }
///     ]]></code>
/// </example>
/// <seealso cref="ResultError" />
/// <seealso cref="ErrorSeverity" />
public static class ErrorExtensions
{
    /// <summary>
    ///     Determines whether the specified error is classified as <see cref="ErrorSeverity.Critical" />.
    /// </summary>
    /// <param name="error">The <see cref="ResultError" /> instance to inspect.</param>
    /// <returns>
    ///     <c>true</c> if <paramref name="error" /> has <see cref="ErrorSeverity.Critical" /> severity; otherwise,
    ///     <c>false</c>.
    /// </returns>
    public static bool IsCritical(this ResultError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.Severity == ErrorSeverity.Critical;
    }

    /// <summary>
    ///     Determines whether the specified error is classified as <see cref="ErrorSeverity.High" />.
    /// </summary>
    /// <param name="error">The <see cref="ResultError" /> instance to inspect.</param>
    /// <returns>
    ///     <c>true</c> if <paramref name="error" /> has <see cref="ErrorSeverity.High" /> severity; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsHigh(this ResultError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.Severity == ErrorSeverity.High;
    }

    /// <summary>
    ///     Determines whether the specified error is classified as <see cref="ErrorSeverity.Medium" />.
    /// </summary>
    /// <param name="error">The <see cref="ResultError" /> instance to inspect.</param>
    /// <returns>
    ///     <c>true</c> if <paramref name="error" /> has <see cref="ErrorSeverity.Medium" /> severity; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsMedium(this ResultError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.Severity == ErrorSeverity.Medium;
    }

    /// <summary>
    ///     Determines whether the specified error is classified as <see cref="ErrorSeverity.Low" />.
    /// </summary>
    /// <param name="error">The <see cref="ResultError" /> instance to inspect.</param>
    /// <returns>
    ///     <c>true</c> if <paramref name="error" /> has <see cref="ErrorSeverity.Low" /> severity; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsLow(this ResultError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.Severity == ErrorSeverity.Low;
    }
}
