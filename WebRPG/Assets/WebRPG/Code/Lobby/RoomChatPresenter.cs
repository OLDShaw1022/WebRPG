using System;

namespace WebRPG
{
    // Connects either room log view to the same room chat state.
    public sealed class RoomChatPresenter : IDisposable
    {
        private readonly LobbyModel _model;
        private readonly RoomChatView _view;

        public RoomChatPresenter(LobbyModel model, RoomChatView view)
        {
            _model = model;
            _view = view;
            if (!_view) return;
            _model.ChatChanged += Refresh;
            _model.ChatDelivered += _view.ClearDelivered;
            _view.SendRequested += Send;
            Refresh();
        }

        public void Dispose()
        {
            if (!_view) return;
            _model.ChatChanged -= Refresh;
            _model.ChatDelivered -= _view.ClearDelivered;
            _view.SendRequested -= Send;
        }

        private void Send(string message) => _model.SendChatAsync(message).Forget();

        private void Refresh() => _view.Render(_model.Room?.Id, _model.RoomLog, _model.ChatError, _model.CanChat);
    }
}
