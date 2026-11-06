using WebRPGShared;

namespace WebRPGServer;

// 載入並驗證大廳的 JSON 容量與連線設定。
public sealed class LobbyOptions
{
    public int RoomCapacity { get; set; } = 4;
    public int MaximumRooms { get; set; } = 100;
    public int MaximumConnections { get; set; } = 256;
    public int SendTimeoutSeconds { get; set; } = 3;

    public void Validate()
    {
        if (RoomCapacity < 1 || RoomCapacity > LobbyPacketLimits.MaximumPlayers ||
            MaximumRooms < 1 || MaximumConnections < 1 || SendTimeoutSeconds < 1)
            throw new InvalidOperationException("Invalid Lobby configuration.");
    }
}
