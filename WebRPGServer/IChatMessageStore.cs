using WebRPGShared;

namespace WebRPGServer;

public interface IChatMessageStore
{
    Task InitializeAsync(CancellationToken cancellationToken);

    Task SaveMessageAsync(ChatPacket message, CancellationToken cancellationToken);

    Task<IReadOnlyList<ChatPacket>> GetRecentMessagesAsync(
        int count,
        CancellationToken cancellationToken);
}