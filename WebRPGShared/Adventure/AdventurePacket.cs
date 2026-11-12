using System;

namespace WebRPGShared
{
    // 承載多人冒險的操作、回合與伺服器序列。
    [Serializable]
    public sealed class AdventurePacket
    {
        public string Type { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public string RoomId { get; set; } = string.Empty;
        public string PlayerId { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public int Turn { get; set; }
        public long Sequence { get; set; }
        public string Payload { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
    }
}
