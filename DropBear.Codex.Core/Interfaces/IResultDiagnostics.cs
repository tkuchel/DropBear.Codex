#region

using System.Diagnostics;
using DropBear.Codex.Core.Results.Diagnostics;

#endregion

namespace DropBear.Codex.Core.Interfaces;

/// <summary>
///     Provides diagnostic capabilities for result operations.
/// </summary>
public interface IResultDiagnostics
{
    /// <summary>
    ///     Gets diagnostic information about a result.
    /// </summary>
    DiagnosticInfo GetDiagnostics();

    /// <summary>
    ///     Gets the trace context for a result operation.
    /// </summary>
    ActivityContext GetTraceContext();
}
