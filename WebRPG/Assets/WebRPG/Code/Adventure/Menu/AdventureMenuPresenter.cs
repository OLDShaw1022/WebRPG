using System;

namespace WebRPG
{
    // 協調選單導航、單人暫停與結束本輪後返回主選單。
    public sealed class AdventureMenuPresenter : IDisposable
    {
        private readonly AdventureMenuModel _model;
        private readonly IAdventureMenuView _view;
        private readonly Action<bool> _pause;
        private readonly Action _abandon;
        private readonly Action _restart;
        private readonly Action _return;
        private bool _disposed;

        public AdventureMenuPresenter(AdventureMenuModel model, IAdventureMenuView view,
            Action<bool> pause, Action abandon, Action restart, Action returnToMenu)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _pause = pause ?? throw new ArgumentNullException(nameof(pause));
            _abandon = abandon ?? throw new ArgumentNullException(nameof(abandon));
            _restart = restart ?? throw new ArgumentNullException(nameof(restart));
            _return = returnToMenu ?? throw new ArgumentNullException(nameof(returnToMenu));
            _view.ResumeRequested += Close;
            _view.AbandonRequested += HandleAbandon;
            _view.RestartRequested += HandleRestart;
            _view.SettingsRequested += HandleSettings;
            _view.ReturnRequested += HandleReturn;
            _view.ConfirmReturnRequested += HandleConfirmReturn;
            _view.BackRequested += Open;
            Refresh();
        }

        public void Open()
        {
            if (_disposed)
            {
                return;
            }
            _model.Navigate(AdventureMenuScreen.Menu);
            Refresh();
        }

        public void Close()
        {
            _model.Navigate(AdventureMenuScreen.Hidden);
            Refresh();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _view.ResumeRequested -= Close;
            _view.AbandonRequested -= HandleAbandon;
            _view.RestartRequested -= HandleRestart;
            _view.SettingsRequested -= HandleSettings;
            _view.ReturnRequested -= HandleReturn;
            _view.ConfirmReturnRequested -= HandleConfirmReturn;
            _view.BackRequested -= Open;
        }

        private void HandleAbandon()
        {
            Close();
            _abandon();
        }

        private void HandleRestart()
        {
            Close();
            _restart();
        }

        private void HandleSettings()
        {
            _model.Navigate(AdventureMenuScreen.Settings);
            Refresh();
        }

        private void HandleReturn()
        {
            _model.Navigate(AdventureMenuScreen.ConfirmReturn);
            Refresh();
        }

        private void HandleConfirmReturn()
        {
            if (_model.Screen != AdventureMenuScreen.ConfirmReturn)
            {
                return;
            }
            Close();
            _return();
        }

        private void Refresh()
        {
            _pause(_model.IsPaused);
            _view.Render(_model.Screen);
        }
    }
}
