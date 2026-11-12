using System;

namespace WebRPG
{
    // 定義伺服器選項的顯示名稱、連線位址與 Editor 限制。
    [Serializable]
    public sealed class LobbyServerConfiguration
    {
        public string Id;
        public string Name;
        public string Url;
        public bool EditorOnly;
    }
}
