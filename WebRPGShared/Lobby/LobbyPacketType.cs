namespace WebRPGShared
{
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
}
