using System;
using NUnit.Framework;

namespace WebRPG.Tests
{
    // 驗證主選單的冒險啟動、失敗重試、離開與事件解除。
    public sealed class MainMenuPresenterTests
    {
        [Test]
        public void SinglePlayerStartsOnceAndHidesMenu()
        {
            MainMenuModel model = new MainMenuModel();
            TestMainMenuView view = new TestMainMenuView();
            int starts = 0;
            using (MainMenuPresenter presenter = new MainMenuPresenter(
                model, view, () => { starts++; return true; }, () => { }))
            {
                Assert.That(starts, Is.Zero);
                view.StartSinglePlayer();
                view.StartSinglePlayer();
                Assert.That(starts, Is.EqualTo(1));
                Assert.That(model.HasEnteredAdventure, Is.True);
                Assert.That(view.Hidden, Is.True);
            }
        }

        [Test]
        public void FailedStartKeepsMenuAvailableForRetry()
        {
            MainMenuModel model = new MainMenuModel();
            TestMainMenuView view = new TestMainMenuView();
            bool succeeds = false;
            using (MainMenuPresenter presenter = new MainMenuPresenter(
                model, view, () => succeeds, () => { }))
            {
                view.StartSinglePlayer();
                Assert.That(model.HasEnteredAdventure, Is.False);
                Assert.That(view.Hidden, Is.False);
                succeeds = true;
                view.StartSinglePlayer();
                Assert.That(model.HasEnteredAdventure, Is.True);
                Assert.That(view.Hidden, Is.True);
            }
        }

        [Test]
        public void QuitIsForwardedAndDisposeUnsubscribes()
        {
            TestMainMenuView view = new TestMainMenuView();
            int starts = 0;
            int quits = 0;
            MainMenuPresenter presenter = new MainMenuPresenter(
                new MainMenuModel(), view,
                () => { starts++; return true; }, () => quits++);
            view.Quit();
            Assert.That(quits, Is.EqualTo(1));
            presenter.Dispose();
            presenter.Dispose();
            view.Quit();
            view.StartSinglePlayer();
            Assert.That(quits, Is.EqualTo(1));
            Assert.That(starts, Is.Zero);
        }

        // 模擬主選單操作並記錄隱藏狀態，供 Presenter 測試使用。
        private sealed class TestMainMenuView : IMainMenuView
        {
            public event Action SinglePlayerRequested;
            public event Action QuitRequested;
            public bool Hidden { get; private set; }
            public void Hide() => Hidden = true;
            public void StartSinglePlayer() => SinglePlayerRequested?.Invoke();
            public void Quit() => QuitRequested?.Invoke();
        }
    }
}
