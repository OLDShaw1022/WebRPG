using WebRPGShared;

namespace WebRPGServer;

// 定義聊天訊息儲存區的初始化、寫入與歷史查詢介面。
public interface IChatMessageStore
{
    public Task InitializeAsync(CancellationToken cancellationToken);

    public Task SaveMessageAsync(ChatPacket message, CancellationToken cancellationToken);

    public Task<IReadOnlyList<ChatPacket>> GetRecentMessagesAsync(
        int count,
        CancellationToken cancellationToken);
}