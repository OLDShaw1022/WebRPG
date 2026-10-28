namespace WebRPG
{
    public sealed class MainMenuModel
    {
        public bool HasEnteredAdventure { get; private set; }

        public void EnterAdventure()
        {
            HasEnteredAdventure = true;
        }
    }
}
