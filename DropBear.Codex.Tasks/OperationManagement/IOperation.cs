using DropBear.Codex.Core;

namespace DropBear.Codex.Tasks.OperationManagement;

public interface IOperation
{
    TimeSpan ExecuteTimeout { get; set; }
    TimeSpan RollbackTimeout { get; set; }
    bool ContinueOnFailure { get; set; }

    Task<Result> ExecuteAsync(CancellationToken cancellationToken = default);
    Task<Result> RollbackAsync(CancellationToken cancellationToken = default);

    event EventHandler<ProgressEventArgs> ProgressChanged;
    event EventHandler<LogEventArgs> Log;
}

public interface IOperation<T> : IOperation
{
    new Task<Result<T>> ExecuteAsync(CancellationToken cancellationToken = default);
}
