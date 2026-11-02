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

    // 處理主選單的單人遊戲與離開操作，協調冒險啟動及選單隱藏。
    public sealed class MainMenuPresenter : IDisposable
    {
        private readonly MainMenuModel _model;
        private readonly IMainMenuView _view;
        private readonly Func<bool> _startAdventure;
        private readonly Action _quitGame;
        private readonly Func<bool> _openLobby;
        private bool _disposed;

        public MainMenuPresenter(MainMenuModel model, IMainMenuView view,
            Func<bool> startAdventure, Action quitGame, Func<bool> openLobby = null)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _startAdventure = startAdventure ?? throw new ArgumentNullException(nameof(startAdventure));
            _quitGame = quitGame ?? throw new ArgumentNullException(nameof(quitGame));
            _openLobby = openLobby;
            _view.MultiplayerRequested += HandleMultiplayer;
            _view.SinglePlayerRequested += HandleSinglePlayer;
            _view.QuitRequested += HandleQuit;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _view.MultiplayerRequested -= HandleMultiplayer;
            _view.SinglePlayerRequested -= HandleSinglePlayer;
            _view.QuitRequested -= HandleQuit;
        }

        private void HandleSinglePlayer()
        {
            if (_model.HasEnteredAdventure || !_startAdventure())
            {
                return;
            }

            _model.EnterAdventure();
            _view.Hide();
        }

        private void HandleMultiplayer()
        {
            if (_model.HasEnteredAdventure || _openLobby == null) return;
            if (_openLobby()) _view.Hide();
        }

        private void HandleQuit()
        {
            _quitGame();
        }
    }
}
