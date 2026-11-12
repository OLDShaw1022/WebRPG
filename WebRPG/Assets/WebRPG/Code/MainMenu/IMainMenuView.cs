using System;

namespace WebRPG
{
    // 定義主選單的單人遊戲、離開事件與隱藏介面。
    public interface IMainMenuView
    {
        public event Action SinglePlayerRequested;
        public event Action MultiplayerRequested;
        public event Action QuitRequested;
        public void Hide();
    }
}
