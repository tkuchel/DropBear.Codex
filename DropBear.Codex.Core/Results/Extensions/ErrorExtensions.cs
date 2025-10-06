#region

using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Extensions;

public static class ErrorExtensions
{
    public static bool IsCritical(this ResultError error) =>
        error.Severity == ErrorSeverity.Critical;

    public static bool IsHigh(this ResultError error) =>
        error.Severity == ErrorSeverity.High;

    public static bool IsMedium(this ResultError error) =>
        error.Severity == ErrorSeverity.Medium;

    public static bool IsLow(this ResultError error) =>
        error.Severity == ErrorSeverity.Low;
}
