using NUnit.Framework;

namespace WebRPG.Tests
{
    // 驗證選單暫停僅套用於指定的單人模式。
    public sealed class AdventureMenuModelTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void MenuSettingsAndConfirmationRespectPauseMode(bool singlePlayer)
        {
            AdventureMenuModel model = new AdventureMenuModel(singlePlayer);
            Assert.That(model.IsPaused, Is.False);
            foreach (AdventureMenuScreen screen in new[] { AdventureMenuScreen.Menu, AdventureMenuScreen.Settings,
                AdventureMenuScreen.ConfirmReturn })
            {
                model.Navigate(screen);
                Assert.That(model.IsPaused, Is.EqualTo(singlePlayer));
            }
            model.Navigate(AdventureMenuScreen.Hidden);
            Assert.That(model.IsPaused, Is.False);
        }
    }
}
