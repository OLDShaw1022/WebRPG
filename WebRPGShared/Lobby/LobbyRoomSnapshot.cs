using System;

namespace WebRPGShared
{
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
}
