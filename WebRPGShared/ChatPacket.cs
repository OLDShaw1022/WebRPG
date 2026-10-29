using System;

namespace WebRPGShared
{
    // 定義客戶端與伺服器交換的聊天訊息資料。
    [Serializable]
    public sealed class ChatPacket
    {
        public string Type { get; set; }
        public string Username { get; set; }
        public string Text { get; set; }
        public string ConnectionId { get; set; }
        public DateTimeOffset TimestampUtc { get; set; }
    }

    // 定義聊天協定使用的訊息類型常數。
    public static class ChatPacketType
    {
        public const string Join = "Join";
        public const string Joined = "Joined";
        public const string Chat = "Chat";
        public const string Error = "Error";
    }

    // 定義聊天使用者名稱與訊息的長度上限。
    public static class ChatPacketLimits
    {
        public const int UsernameLength = 24;
        public const int MessageLength = 200;
    }
}
