namespace WebRPG
{
    // 記錄玩家是否已從主選單進入冒險。
    public sealed class MainMenuModel
    {
        public bool HasEnteredAdventure { get; private set; }

        public void EnterAdventure()
        {
            HasEnteredAdventure = true;
        }
    }
}
