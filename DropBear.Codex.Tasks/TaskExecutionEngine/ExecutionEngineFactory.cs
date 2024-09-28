#region

using DropBear.Codex.Core.Logging;
using DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine;

/// <summary>
///     Provides a factory for creating instances of <see cref="ExecutionEngine" />.
/// </summary>
public class ExecutionEngineFactory : IExecutionEngineFactory
{
    private readonly ILogger _logger;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ExecutionEngineFactory" /> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve dependencies.</param>
    public ExecutionEngineFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = LoggerFactory.Logger.ForContext<ExecutionEngineFactory>();
    }

    /// <summary>
    ///     Creates a new instance of <see cref="ExecutionEngine" /> with the specified channel ID.
    /// </summary>
    /// <param name="channelId">The channel ID associated with the execution engine.</param>
    /// <returns>A new instance of <see cref="ExecutionEngine" />.</returns>
    public ExecutionEngine CreateExecutionEngine(Guid channelId)
    {
        if (channelId == Guid.Empty)
        {
            _logger.Error("Invalid channel ID provided to ExecutionEngineFactory.");
            throw new ArgumentException("Channel ID cannot be empty.", nameof(channelId));
        }

        try
        {
            _logger.Information("Creating ExecutionEngine instance for Channel ID: {ChannelId}", channelId);

            var executionEngine = ActivatorUtilities.CreateInstance<ExecutionEngine>(_serviceProvider, channelId);

            _logger.Information("Successfully created ExecutionEngine instance for Channel ID: {ChannelId}",
                channelId);

            return executionEngine;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to create ExecutionEngine instance for Channel ID: {ChannelId}", channelId);
            throw;
        }
    }
}
