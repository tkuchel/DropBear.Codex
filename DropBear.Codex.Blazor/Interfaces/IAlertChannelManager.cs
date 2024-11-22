namespace DropBear.Codex.Blazor.Interfaces;

public interface IAlertChannelManager
{
    string GetChannelId(Guid? userId = null);
    bool IsValidChannel(string channelId);
}
