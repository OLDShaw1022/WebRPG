using System;

namespace WebRPGShared
{
    // 提供公開房間列表中可顯示的摘要。
    [Serializable]
    public sealed class LobbyRoomSummary
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int MemberCount { get; set; }
        public int Capacity { get; set; }
    }
}
