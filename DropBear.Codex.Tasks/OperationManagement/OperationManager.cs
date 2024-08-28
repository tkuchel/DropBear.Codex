#region

using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Transactions;
using DropBear.Codex.Core;
using DropBear.Codex.Core.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Tasks.OperationManagement;

public class OperationManager : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly ConcurrentBag<Exception> _exceptions = new();
    private readonly ILogger _logger;
    private readonly ConcurrentQueue<IOperation> _operations = new();
    private readonly ConcurrentQueue<IOperation> _rollbackOperations = new();

    public OperationManager()
    {
        _logger = LoggerFactory.Logger.ForContext<OperationManager>();
    }

    public IReadOnlyCollection<IOperation> Operations => new ReadOnlyCollection<IOperation>(_operations.ToList());

    public IReadOnlyCollection<IOperation> RollbackOperations =>
        new ReadOnlyCollection<IOperation>(_rollbackOperations.ToList());

    public void Dispose()
    {
        _cancellationTokenSource.Dispose();
    }

    public void OnProgressChanged(ProgressEventArgs e)
    {
        ProgressChanged?.Invoke(this, e);
    }

    public event EventHandler<EventArgs>? OperationStarted;
    public event EventHandler<EventArgs>? OperationCompleted;
    public event EventHandler<OperationFailedEventArgs>? OperationFailed;
    public event EventHandler<EventArgs>? RollbackStarted;
    public event EventHandler<ProgressEventArgs>? ProgressChanged;
    public event EventHandler<LogEventArgs>? Log;

    public void AddOperation(IOperation operation)
    {
        if (operation == null)
        {
            throw new ArgumentNullException(nameof(operation), "Operation cannot be null");
        }

        _operations.Enqueue(operation);
        _rollbackOperations.Enqueue(operation);

        _logger.Information("Operation and rollback operation added.");
    }

    public async Task<Result> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var operationsCopy = _operations.ToList();
        var rollbackOperationsCopy = _rollbackOperations.ToList();

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var totalOperations = operationsCopy.Count;
        var completedOperations = 0;

        _logger.Information("Starting execution of {TotalOperations} operations.", totalOperations);

        try
        {
            foreach (var operation in operationsCopy)
            {
                cancellationToken.ThrowIfCancellationRequested();

                OperationStarted?.Invoke(this, EventArgs.Empty);
                _logger.Information("Operation started.");

                var result = await ExecuteOperationAsync(operation, cancellationToken).ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    var exception = new InvalidOperationException(result.ErrorMessage, result.Exception);
                    _exceptions.Add(exception);
                    OperationFailed?.Invoke(this, new OperationFailedEventArgs(exception));
                    _logger.Error("Operation failed: {ErrorMessage}", result.ErrorMessage);

                    if (!operation.ContinueOnFailure)
                    {
                        break;
                    }
                }

                OperationCompleted?.Invoke(this, EventArgs.Empty);
                _logger.Information("Operation completed.");
                completedOperations++;
                ReportProgress(completedOperations, totalOperations);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Warning("Operation was canceled.");
            return Result.Failure(new Collection<Exception>(_exceptions.ToList()));
        }
        catch (Exception ex)
        {
            _exceptions.Add(ex);
            return Result.Failure(new Collection<Exception>(_exceptions.ToList()));
        }

        if (!_exceptions.IsEmpty)
        {
            RollbackStarted?.Invoke(this, EventArgs.Empty);
            _logger.Information("Starting rollback due to failures.");
            var rollbackResult =
                await ExecuteRollbacksAsync(rollbackOperationsCopy, cancellationToken).ConfigureAwait(false);

            if (!rollbackResult.IsSuccess)
            {
                _exceptions.Add(new InvalidOperationException("Rollback failed", rollbackResult.Exception));
            }

            return Result.Failure(new Collection<Exception>(_exceptions.ToList()));
        }

        scope.Complete();
        _logger.Information("All operations completed successfully.");
        return Result.Success();
    }

    public async Task<Result<List<T>>> ExecuteWithResultsAsync<T>(CancellationToken cancellationToken = default)
    {
        var operationsCopy = _operations.ToList();
        var rollbackOperationsCopy = _rollbackOperations.ToList();

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var totalOperations = operationsCopy.Count;
        var completedOperations = 0;
        var results = new List<T>();

        _logger.Information("Starting execution of {TotalOperations} operations.", totalOperations);

        try
        {
            foreach (var operation in operationsCopy)
            {
                cancellationToken.ThrowIfCancellationRequested();

                OperationStarted?.Invoke(this, EventArgs.Empty);
                _logger.Information("Operation started.");

                if (operation is IOperation<T> typedOperation)
                {
                    var result = await ExecuteOperationWithResultAsync(typedOperation, cancellationToken)
                        .ConfigureAwait(false);
                    if (!result.IsSuccess)
                    {
                        var exception = new InvalidOperationException(result.ErrorMessage, result.Exception);
                        _exceptions.Add(exception);
                        OperationFailed?.Invoke(this, new OperationFailedEventArgs(exception));
                        _logger.Error("Operation failed: {ErrorMessage}", result.ErrorMessage);

                        if (!operation.ContinueOnFailure)
                        {
                            break;
                        }
                    }

                    if (result.IsSuccess)
                    {
                        results.Add(result.Value);
                    }
                    else
                    {
                        var exception = new InvalidOperationException("Unexpected operation result type.");
                        _exceptions.Add(exception);
                        OperationFailed?.Invoke(this, new OperationFailedEventArgs(exception));
                        _logger.Error("Unexpected operation result type.");
                        break;
                    }
                }
                else
                {
                    var result = await ExecuteOperationAsync(operation, cancellationToken).ConfigureAwait(false);
                    if (!result.IsSuccess)
                    {
                        var exception = new InvalidOperationException(result.ErrorMessage, result.Exception);
                        _exceptions.Add(exception);
                        OperationFailed?.Invoke(this, new OperationFailedEventArgs(exception));
                        _logger.Error("Operation failed: {ErrorMessage}", result.ErrorMessage);

                        if (!operation.ContinueOnFailure)
                        {
                            break;
                        }
                    }
                }

                OperationCompleted?.Invoke(this, EventArgs.Empty);
                _logger.Information("Operation completed.");
                completedOperations++;
                ReportProgress(completedOperations, totalOperations);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Warning("Operation was canceled.");
            return Result<List<T>>.Failure(new Collection<Exception>(_exceptions.ToList()));
        }
        catch (Exception ex)
        {
            _exceptions.Add(ex);
            return Result<List<T>>.Failure(new Collection<Exception>(_exceptions.ToList()));
        }

        if (!_exceptions.IsEmpty)
        {
            RollbackStarted?.Invoke(this, EventArgs.Empty);
            _logger.Information("Starting rollback due to failures.");
            var rollbackResult =
                await ExecuteRollbacksAsync(rollbackOperationsCopy, cancellationToken).ConfigureAwait(false);
            return rollbackResult.IsSuccess
                ? Result<List<T>>.Failure(new Collection<Exception>(_exceptions.ToList()))
                : Result<List<T>>.Failure(rollbackResult.ErrorMessage);
        }

        scope.Complete();
        _logger.Information("All operations completed successfully.");
        return Result<List<T>>.Success(results);
    }

    private static async Task<Result> ExecuteRollbacksAsync(List<IOperation> rollbackOperations,
        CancellationToken cancellationToken = default)
    {
        var rollbackExceptions = new List<Exception>();

        var rollbackTasks = rollbackOperations.Select(async operation =>
        {
            var result = await operation.RollbackAsync(cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                rollbackExceptions.Add(new InvalidOperationException(result.ErrorMessage, result.Exception));
                Console.WriteLine($"Rollback failed: {result.ErrorMessage}");
            }
            else
            {
                Console.WriteLine("Rollback succeeded");
            }
        });

        await Task.WhenAll(rollbackTasks).ConfigureAwait(false);

        return rollbackExceptions.Count == 0
            ? Result.Success()
            : Result.Failure(new Collection<Exception>(rollbackExceptions));
    }

    private static async Task<Result> ExecuteOperationAsync(IOperation operation,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(operation.ExecuteTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var result = await operation.ExecuteAsync(linkedCts.Token).ConfigureAwait(false);
            return result;
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure("Operation canceled.", ex);
        }
        catch (OperationCanceledException ex) when (timeoutCts.IsCancellationRequested)
        {
            return Result.Failure("Operation timed out.", ex);
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message, ex);
        }
    }

    private static async Task<Result<T>> ExecuteOperationWithResultAsync<T>(IOperation<T> operation,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(operation.ExecuteTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var result = await operation.ExecuteAsync(linkedCts.Token).ConfigureAwait(false);
            return result;
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            return Result<T>.Failure("Operation canceled.", ex);
        }
        catch (OperationCanceledException ex) when (timeoutCts.IsCancellationRequested)
        {
            return Result<T>.Failure("Operation timed out.", ex);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(ex.Message, ex);
        }
    }

    private void ReportProgress(int completedOperations, int totalOperations)
    {
        var progressPercentage = (int)(completedOperations / (double)totalOperations * 100);
        ProgressChanged?.Invoke(this,
            new ProgressEventArgs(progressPercentage,
                $"Completed {completedOperations} of {totalOperations} operations."));
        _logger.Information(
            "Progress: {ProgressPercentage}% - Completed {CompletedOperations} of {TotalOperations} operations.",
            progressPercentage, completedOperations, totalOperations);
    }

    private void LogMessage(string message)
    {
        Log?.Invoke(this, new LogEventArgs(message));
    }

    public void Cancel()
    {
        _cancellationTokenSource.Cancel();
    }
}
