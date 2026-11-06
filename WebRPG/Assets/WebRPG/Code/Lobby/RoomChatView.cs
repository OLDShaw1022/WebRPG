using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WebRPGShared;

namespace WebRPG
{
    // Shows the room log and submits plain text through the active chat presenter.
    public sealed class RoomChatView : MonoBehaviour
    {
        private const string JournalSeparator = "\n\n";
        private const float FollowBottomThreshold = 0.01f;
        [SerializeField] private TMP_InputField _input;
        [SerializeField] private UnityEngine.UI.Button _send;
        [SerializeField] private TextMeshProUGUI _error;
        [SerializeField] private TextMeshProUGUI _log;
        [SerializeField] private ScrollRect _scroll;
        private string _roomId;
        private string _roomLog = string.Empty;
        private string _journal = string.Empty;

        public event Action<string> SendRequested;

        private void Awake()
        {
            _input.characterLimit = LobbyPacketLimits.ChatMessageLength;
            _input.richText = false;
            _log.richText = false;
            _send.onClick.AddListener(Send);
        }

        public void Render(string roomId, string roomLog, string error, bool canChat)
        {
            if (_roomId != roomId)
            {
                _input.SetTextWithoutNotify(string.Empty);
                _roomId = roomId;
            }
            _roomLog = roomLog;
            _error.text = error;
            _input.interactable = roomId != null;
            _send.interactable = canChat;
            RenderLog();
        }

        public void ClearDelivered(string draft)
        {
            if (_input.text == draft) _input.SetTextWithoutNotify(string.Empty);
        }

        public void SetJournal(string journal)
        {
            if (_journal == journal) return;
            _journal = journal;
            RenderLog();
        }

        private void Send() => SendRequested?.Invoke(_input.text);

        private void RenderLog()
        {
            string text = string.IsNullOrEmpty(_journal) ? _roomLog :
                string.IsNullOrEmpty(_roomLog) ? _journal : _journal + JournalSeparator + _roomLog;
            if (_log.text == text) return;
            bool followLatest = _scroll.verticalNormalizedPosition <= FollowBottomThreshold || _log.text.Length == 0;
            _log.text = text;
            if (!isActiveAndEnabled) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_scroll.content);
            if (followLatest) _scroll.verticalNormalizedPosition = 0f;
        }
    }
}
