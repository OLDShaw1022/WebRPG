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

    // 提供公開房間列表中可顯示的摘要。
    [Serializable]
    public sealed class LobbyRoomSummary
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int MemberCount { get; set; }
        public int Capacity { get; set; }
    }

    // 提供本機所屬房間與隊員的伺服器快照。
    [Serializable]
    public sealed class LobbyRoomSnapshot
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string LeaderId { get; set; } = string.Empty;
        public bool IsPublic { get; set; }
        public int Capacity { get; set; }
        public LobbyMemberSnapshot[] Members { get; set; } = Array.Empty<LobbyMemberSnapshot>();
    }

    // 提供由伺服器識別的玩家名稱與準備狀態。
    [Serializable]
    public sealed class LobbyMemberSnapshot
    {
        public string PlayerId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public bool IsReady { get; set; }
    }

    // 統一大廳訊息類型，避免客戶端與伺服器協定不一致。
    public static class LobbyPacketType
    {
        public const string Hello = "Hello";
        public const string Welcome = "Welcome";
        public const string ListRooms = "ListRooms";
        public const string CreateRoom = "CreateRoom";
        public const string JoinRoom = "JoinRoom";
        public const string LeaveRoom = "LeaveRoom";
        public const string SetReady = "SetReady";
        public const string State = "State";
        public const string Error = "Error";
        public const string RoomChat = "RoomChat";
        public const string Adventure = "Adventure";
    }

    // 定義協定可接受的文字與人數上限。
    public static class LobbyPacketLimits
    {
        public const int UsernameLength = 24;
        public const int RoomNameLength = 40;
        public const int MaximumPlayers = 4;
        public const int ChatMessageLength = 240;
        public const int MessageBytes = 8192;
    }
}
