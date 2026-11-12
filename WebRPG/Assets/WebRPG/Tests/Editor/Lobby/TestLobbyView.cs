using System;

namespace WebRPG.Tests
{
    // 模擬大廳操作並保存最後一次呈現資料。
    internal sealed class TestLobbyView : ILobbyView
    {
        public event Action<string, bool> CreateRequested;
        public event Action<string> JoinRequested;
        public event Action RefreshRequested;
        public event Action LeaveRequested;
        public event Action ReadyRequested;
        public event Action BackRequested;
        public LobbyScreenModel Screen;
        public void Render(LobbyScreenModel screen) => Screen = screen;
        public void Create() => CreateRequested?.Invoke("Room", true);
        public void Join() => JoinRequested?.Invoke(" room-id ");
        public void Refresh() => RefreshRequested?.Invoke();
        public void Leave() => LeaveRequested?.Invoke();
        public void Ready() => ReadyRequested?.Invoke();
        public void Back() => BackRequested?.Invoke();
    }
}
