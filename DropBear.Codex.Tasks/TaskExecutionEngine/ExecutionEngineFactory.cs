#region

using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Tasks.Errors;
using DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;
using DropBear.Codex.Tasks.TaskExecutionEngine.Messages;
using DropBear.Codex.Tasks.TaskExecutionEngine.Models;
using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine;

/// <summary>
///     Provides a factory for creating instances of <see cref="ExecutionEngine" />.
/// </summary>
public sealed class ExecutionEngineFactory : IExecutionEngineFactory
{
    private readonly ILogger _logger;
    private readonly IOptions<ExecutionOptions> _options;
    private readonly IAsyncPublisher<Guid, TaskProgressMessage> _progressPublisher;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAsyncPublisher<Guid, TaskCompletedMessage> _taskCompletedPublisher;
    private readonly IAsyncPublisher<Guid, TaskFailedMessage> _taskFailedPublisher;
    private readonly IAsyncPublisher<Guid, TaskStartedMessage> _taskStartedPublisher;

    public ExecutionEngineFactory(
        IOptions<ExecutionOptions> options,
        IServiceScopeFactory scopeFactory,
        IAsyncPublisher<Guid, TaskProgressMessage> progressPublisher,
        IAsyncPublisher<Guid, TaskStartedMessage> taskStartedPublisher,
        IAsyncPublisher<Guid, TaskCompletedMessage> taskCompletedPublisher,
        IAsyncPublisher<Guid, TaskFailedMessage> taskFailedPublisher)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _progressPublisher = progressPublisher ?? throw new ArgumentNullException(nameof(progressPublisher));
        _taskStartedPublisher = taskStartedPublisher ?? throw new ArgumentNullException(nameof(taskStartedPublisher));
        _taskCompletedPublisher =
            taskCompletedPublisher ?? throw new ArgumentNullException(nameof(taskCompletedPublisher));
        _taskFailedPublisher = taskFailedPublisher ?? throw new ArgumentNullException(nameof(taskFailedPublisher));
        _logger = LoggerFactory.Logger.ForContext<ExecutionEngineFactory>();
    }

    /// <summary>
    ///     Creates a new instance of <see cref="ExecutionEngine" /> with the specified channel ID.
    /// </summary>
    public Result<ExecutionEngine, ExecutionEngineError> CreateExecutionEngine(Guid channelId)
    {
        try
        {
            var validationResult = ValidateChannelId(channelId)
                .Bind(_ => ValidateDependencies());

            if (!validationResult.IsSuccess)
            {
                _logger.Error("Validation failed: {ErrorMessage}", validationResult.Error?.Message);
                return Result<ExecutionEngine, ExecutionEngineError>.Failure(validationResult.Error!);
            }

            return CreateEngineInstance(channelId);
        }
        catch (Exception ex)
        {
            var error = new ExecutionEngineError("Unexpected error creating execution engine", ex);
            _logger.Error(ex, "Failed to create ExecutionEngine: {ErrorMessage}", error.Message);
            return Result<ExecutionEngine, ExecutionEngineError>.Failure(error);
        }
    }

    private Result<Unit, ExecutionEngineError> ValidateChannelId(Guid channelId)
    {
        if (channelId == Guid.Empty)
        {
            _logger.Error("Invalid channel ID provided");
            return Result<Unit, ExecutionEngineError>.Failure(
                new ExecutionEngineError("Channel ID cannot be empty"));
        }

        return Result<Unit, ExecutionEngineError>.Success(Unit.Value);
    }

    private Result<Unit, ExecutionEngineError> ValidateDependencies()
    {
        try
        {
            if (_options.Value == null)
            {
                _logger.Error("ExecutionOptions is not configured");
                return Result<Unit, ExecutionEngineError>.Failure(
                    new ExecutionEngineError("ExecutionOptions is not configured"));
            }

            using var scope = _scopeFactory.CreateScope();
            if (scope == null)
            {
                _logger.Error("Failed to create service scope");
                return Result<Unit, ExecutionEngineError>.Failure(
                    new ExecutionEngineError("Failed to create service scope"));
            }

            return Result<Unit, ExecutionEngineError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to validate dependencies");
            return Result<Unit, ExecutionEngineError>.Failure(
                new ExecutionEngineError("Failed to validate dependencies", ex));
        }
    }

    private Result<ExecutionEngine, ExecutionEngineError> CreateEngineInstance(Guid channelId)
    {
        try
        {
            _logger.Debug("Creating ExecutionEngine instance for Channel ID: {ChannelId}", channelId);

            var engine = new ExecutionEngine(
                channelId,
                _options,
                _scopeFactory,
                _progressPublisher,
                _taskStartedPublisher,
                _taskCompletedPublisher,
                _taskFailedPublisher
            );

            _logger.Information("Successfully created ExecutionEngine for Channel ID: {ChannelId}", channelId);
            return Result<ExecutionEngine, ExecutionEngineError>.Success(engine);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to create ExecutionEngine instance");
            return Result<ExecutionEngine, ExecutionEngineError>.Failure(
                new ExecutionEngineError("Failed to create ExecutionEngine instance", ex));
        }
    }
}
