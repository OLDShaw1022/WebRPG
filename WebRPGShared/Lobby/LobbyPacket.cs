using System;

namespace WebRPGShared
{
    // 定義房間大廳的要求、回應與完整狀態推播。
    [Serializable]
    public sealed class LobbyPacket
    {
        public string Type { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string PlayerId { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public string RoomId { get; set; } = string.Empty;
        public bool IsPublic { get; set; } = true;
        public bool IsReady { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public LobbyRoomSummary[] Rooms { get; set; } = Array.Empty<LobbyRoomSummary>();
        public LobbyRoomSnapshot Room { get; set; }
        public AdventurePacket Adventure { get; set; }
    }
}
