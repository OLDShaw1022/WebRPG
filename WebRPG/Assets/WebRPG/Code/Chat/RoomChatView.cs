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
        private const float FollowBottomThreshold = 0.01f;
        [SerializeField]
        private TMP_InputField _input;
        [SerializeField]
        private UnityEngine.UI.Button _send;
        [SerializeField]
        private TextMeshProUGUI _error;
        [SerializeField]
        private TextMeshProUGUI _log;
        [SerializeField]
        private ScrollRect _scroll;
        private string _roomId;

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
            _error.text = error;
            _input.interactable = roomId != null;
            _send.interactable = canChat;
            RenderLog(roomLog);
        }

        public void ClearDelivered(string draft)
        {
            if (_input.text == draft) _input.SetTextWithoutNotify(string.Empty);
        }

        private void Send() => SendRequested?.Invoke(_input.text);

        private void RenderLog(string text)
        {
            if (_log.text == text) return;
            bool followLatest = _scroll.verticalNormalizedPosition <= FollowBottomThreshold || _log.text.Length == 0;
            _log.text = text;
            if (!isActiveAndEnabled) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_scroll.content);
            if (followLatest) _scroll.verticalNormalizedPosition = 0f;
        }
    }
}
