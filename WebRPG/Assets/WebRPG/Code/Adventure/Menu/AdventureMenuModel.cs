namespace WebRPG
{
    // 保存選單狀態，依單人模式決定是否暫停挑戰。
    public sealed class AdventureMenuModel
    {
        private readonly bool _pauseOnOpen;

        public AdventureMenuModel(bool pauseOnOpen)
        {
            _pauseOnOpen = pauseOnOpen;
        }

        public AdventureMenuScreen Screen { get; private set; }
        public bool IsPaused => _pauseOnOpen && Screen != AdventureMenuScreen.Hidden;

        public void Navigate(AdventureMenuScreen screen)
        {
            Screen = screen;
        }
    }
}
