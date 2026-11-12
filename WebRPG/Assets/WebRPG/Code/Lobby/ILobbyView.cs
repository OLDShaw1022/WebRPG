using System;

namespace WebRPG
{
    // 定義大廳畫面的操作與呈現介面。
    public interface ILobbyView
    {
        public event Action<string, bool> CreateRequested;
        public event Action<string> JoinRequested;
        public event Action RefreshRequested;
        public event Action LeaveRequested;
        public event Action ReadyRequested;
        public event Action BackRequested;
        public void Render(LobbyScreenModel screen);
    }
}
