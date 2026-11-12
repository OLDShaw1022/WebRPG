using System;

namespace WebRPG
{
    // 定義冒險畫面的操作事件與資料呈現介面。
    public interface IAdventureView
    {
        public event Action StartRequested;
        public event Action AbandonRequested;
        public event Action<int> ChoiceRequested;
        public event Action<string> CharacterFocusRequested;
        public event Action MapToggleRequested;
        public event Action<int> SkillRequested;
        public event Action<float> BattleSpeedRequested;
        public void Render(AdventureScreenModel screen);
    }
}
