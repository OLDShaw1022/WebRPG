using WebRPGShared;

namespace WebRPGServer;

public interface IChatMessageStore
{
    public Task InitializeAsync(CancellationToken cancellationToken);

    public Task SaveMessageAsync(ChatPacket message, CancellationToken cancellationToken);

    public Task<IReadOnlyList<ChatPacket>> GetRecentMessagesAsync(
        int count,
        CancellationToken cancellationToken);
}