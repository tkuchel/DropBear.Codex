#region

using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Diagnostics;

#endregion

namespace DropBear.Codex.Core.Results.Extensions;

/// <summary>
///     Extensions for diagnostic capabilities.
/// </summary>
public static class DiagnosticExtensions
{
    public static Result<T, TError> WithDiagnostics<T, TError>(
        this Result<T, TError> result,
        Action<DiagnosticInfo> diagnosticHandler)
        where TError : ResultError
    {
        var info = ((result as IResultDiagnostics)!).GetDiagnostics();
        diagnosticHandler(info);
        return result;
    }

    public static Result<T, TError> WithTiming<T, TError>(
        this Result<T, TError> result,
        Action<OperationTiming> timingHandler)
        where TError : ResultError
    {
        var timing = new OperationTiming(DateTime.UtcNow, DateTime.UtcNow);
        timingHandler(timing);
        return result;
    }
}
