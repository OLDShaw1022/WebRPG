namespace WebRPGShared
{
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
