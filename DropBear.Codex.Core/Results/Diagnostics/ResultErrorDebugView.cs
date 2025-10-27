using System.Diagnostics.CodeAnalysis;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Errors;

namespace DropBear.Codex.Core.Results.Diagnostics;

/// <summary>
///     Debugger type proxy for ResultError to provide better debugging experience.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by debugger via DebuggerTypeProxy attribute")]
internal sealed class ResultErrorDebugView
{
    private readonly ResultError _error;

    public ResultErrorDebugView(ResultError error)
    {
        _error = error;
    }

    public string Message => _error.Message;
    public string? Code => _error.Code;
    public ErrorSeverity Severity => _error.Severity;
    public ErrorCategory Category => _error.Category;
    public DateTimeOffset Timestamp => _error.Timestamp;
    public string ErrorId => _error.ErrorId;
    public TimeSpan Age => _error.Age;
    public bool HasException => _error.HasException;
    public Exception? SourceException => _error.SourceException;
    public string? StackTrace => _error.StackTrace;
    public bool HasMetadata => _error.HasMetadata;
    public IReadOnlyDictionary<string, object> Metadata => _error.Metadata;
    public string FullExceptionMessage => _error.GetFullExceptionMessage();
}
