using System;
using System.Collections;
using TMPro;
using UnityEngine;
using WebRPGShared;

namespace WebRPG
{
    public sealed class ChatView : MonoBehaviour, IChatView
    {
        [SerializeField]
        private GameObject _loginPanel;

        [SerializeField]
        private GameObject _chatPanel;

        [SerializeField]
        private TMP_InputField _usernameInput;

        [SerializeField]
        private TMP_Text _loginHint;

        [SerializeField]
        private UnityEngine.UI.Button _connectButton;

        [SerializeField]
        private TMP_Text _channelStatus;

        [SerializeField]
        private TMP_InputField _messageInput;

        [SerializeField]
        private UnityEngine.UI.Button _sendButton;

        [SerializeField]
        private UnityEngine.UI.ScrollRect _messageScrollRect;

        [SerializeField]
        private RectTransform _messageContent;

        [SerializeField]
        private ChatMessageView _incomingMessagePrefab;

        [SerializeField]
        private ChatMessageView _outgoingMessagePrefab;

        public event Action ConnectRequested;
        public event Action SendRequested;

        public string UsernameText => _usernameInput.text;

        public string MessageText
        {
            get => _messageInput.text;
            set => _messageInput.text = value;
        }

        private void Awake()
        {
            for (int childIndex = _messageContent.childCount - 1; childIndex >= 0; childIndex--)
            {
                Destroy(_messageContent.GetChild(childIndex).gameObject);
            }

            _loginPanel.SetActive(true);
            _chatPanel.SetActive(false);
            _sendButton.interactable = false;
            _connectButton.onClick.AddListener(HandleConnectClicked);
            _sendButton.onClick.AddListener(HandleSendClicked);
        }

        private void OnDestroy()
        {
            if (_connectButton)
            {
                _connectButton.onClick.RemoveListener(HandleConnectClicked);
            }

            if (_sendButton)
            {
                _sendButton.onClick.RemoveListener(HandleSendClicked);
            }
        }

        public void SetLoginHint(string hint)
        {
            _loginHint.text = hint;
        }

        public void SetChannelStatus(string status)
        {
            _channelStatus.text = status;
        }

        public void SetConnectEnabled(bool enabled)
        {
            _connectButton.interactable = enabled;
        }

        public void SetSendEnabled(bool enabled)
        {
            _sendButton.interactable = enabled;
        }

        public void ShowChat()
        {
            _loginPanel.SetActive(false);
            _chatPanel.SetActive(true);
        }

        public void ShowMessage(ChatPacket packet, bool isOutgoing)
        {
            ChatMessageView messagePrefab = isOutgoing ? _outgoingMessagePrefab : _incomingMessagePrefab;
            ChatMessageView messageView = Instantiate(messagePrefab, _messageContent, false);
            messageView.SetMessage(packet.Username, packet.Text, packet.TimestampUtc);
            StartCoroutine(ScrollToLatestMessage());
        }

        public void ReportError(Exception exception)
        {
            _channelStatus.text = exception.Message;
            if (_loginPanel.activeSelf)
            {
                _loginHint.text = exception.Message;
            }

            Debug.LogException(exception, this);
        }

        private void HandleConnectClicked()
        {
            ConnectRequested?.Invoke();
        }

        private void HandleSendClicked()
        {
            SendRequested?.Invoke();
        }

        private IEnumerator ScrollToLatestMessage()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            _messageScrollRect.verticalNormalizedPosition = 0f;
        }
    }
}