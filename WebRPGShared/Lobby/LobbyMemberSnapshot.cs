using System;

namespace WebRPGShared
{
    // 提供由伺服器識別的玩家名稱與準備狀態。
    [Serializable]
    public sealed class LobbyMemberSnapshot
    {
        public string PlayerId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public bool IsReady { get; set; }
    }
}
