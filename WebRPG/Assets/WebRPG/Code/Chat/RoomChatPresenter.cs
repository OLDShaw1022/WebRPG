using System;
using System.Collections.Generic;
using System.Linq;

namespace WebRPG
{
    // Connects either room log view to the same room chat state.
    public sealed class RoomChatPresenter : IDisposable
    {
        private const int MaximumAdventureLogEntries = 80;
        private const string NewLine = "\n";
        private readonly AdventureModel _adventure;
        private readonly List<(string Text, bool IsRoomEntry)> _adventureLog =
            new List<(string Text, bool IsRoomEntry)>();
        private string _roomId;

        private readonly LobbyModel _model;
        private readonly RoomChatView _view;

        public RoomChatPresenter(LobbyModel model, RoomChatView view, AdventureModel adventure = null)
        {
            _model = model;
            _view = view;
            _adventure = adventure;
            if (!_view) return;
            _roomId = _model.Room?.Id;
            if (_adventure != null)
            {
                if (!string.IsNullOrEmpty(_model.RoomLog))
                    foreach (string line in _model.RoomLog.Split(new[] { NewLine }, StringSplitOptions.None))
                        AppendEntry(line, true);
                foreach (string line in _adventure.Journal) AppendEntry(line, false);
                _model.RoomLogEntryAdded += AddRoomEntry;
                _adventure.JournalEntryAdded += AddAdventureEntry;
            }
            _model.ChatChanged += Refresh;
            _model.ChatDelivered += _view.ClearDelivered;
            _view.SendRequested += Send;
            Refresh();
        }

        public void Dispose()
        {
            if (!_view) return;
            _model.ChatChanged -= Refresh;
            if (_adventure != null)
            {
                _model.RoomLogEntryAdded -= AddRoomEntry;
                _adventure.JournalEntryAdded -= AddAdventureEntry;
            }
            _model.ChatDelivered -= _view.ClearDelivered;
            _view.SendRequested -= Send;
        }

        private void Send(string message) => _model.SendChatAsync(message).Forget();

        private void AddRoomEntry(string line) => AppendEntry(line, true);

        private void AddAdventureEntry(string line)
        {
            AppendEntry(line, false);
            Refresh();
        }

        private void AppendEntry(string line, bool isRoomEntry)
        {
            _adventureLog.Add((line, isRoomEntry));
            if (_adventureLog.Count > MaximumAdventureLogEntries) _adventureLog.RemoveAt(0);
        }

        private void Refresh()
        {
            string roomId = _model.Room?.Id;
            if (_roomId != roomId)
            {
                _adventureLog.RemoveAll(entry => entry.IsRoomEntry);
                _roomId = roomId;
            }
            string log = _adventure == null ? _model.RoomLog :
                string.Join(NewLine, _adventureLog.Select(entry => entry.Text));
            _view.Render(roomId, log, _model.ChatError, _model.CanChat);
        }
    }
}
