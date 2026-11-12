using System;

namespace WebRPG
{
    // 定義挑戰選單的操作事件與呈現介面。
    public interface IAdventureMenuView
    {
        public event Action ResumeRequested;
        public event Action AbandonRequested;
        public event Action RestartRequested;
        public event Action SettingsRequested;
        public event Action ReturnRequested;
        public event Action ConfirmReturnRequested;
        public event Action BackRequested;
        public void Render(AdventureMenuScreen screen);
    }
}
