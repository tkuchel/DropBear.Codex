#region

using System.Collections.Concurrent;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Core.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Services;

public class AlertChannelManager : IAlertChannelManager
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<AlertChannelManager>();
    private readonly ConcurrentDictionary<string, DateTime> _activeChannels = new();

    public string GetChannelId(Guid? userId = null)
    {
        var channelId = userId.HasValue
            ? $"User.{userId}"
            : "Global";

        _activeChannels.AddOrUpdate(channelId,
            DateTime.UtcNow,
            (_, _) => DateTime.UtcNow);

        Logger.Debug("Channel registered: {ChannelId}", channelId);
        return channelId;
    }

    public bool IsValidChannel(string channelId)
    {
        var isValid = _activeChannels.ContainsKey(channelId);
        Logger.Debug("Channel validation check: {ChannelId} - Valid: {IsValid}", channelId, isValid);
        return isValid;
    }

    internal void RemoveChannel(string channelId)
    {
        if (_activeChannels.TryRemove(channelId, out _))
        {
            Logger.Debug("Channel removed: {ChannelId}", channelId);
        }
    }
}
