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
}
