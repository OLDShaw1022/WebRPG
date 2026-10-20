using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WebRPGShared;

namespace WebRPG
{

public sealed class ChatUIController : MonoBehaviour
{
    [SerializeField] private ChatWebSocketClient _chatWebSocketClient;
    [SerializeField] private GameObject _loginPanel;
    [SerializeField] private GameObject _chatPanel;
    [SerializeField] private TMP_InputField _usernameInput;
    [SerializeField] private TMP_Text _loginHint;
    [SerializeField] private Button _connectButton;
    [SerializeField] private TMP_Text _channelStatus;
    [SerializeField] private TMP_InputField _messageInput;
    [SerializeField] private Button _sendButton;
    [SerializeField] private ScrollRect _messageScrollRect;
    [SerializeField] private RectTransform _messageContent;
    [SerializeField] private GameObject _incomingMessagePrefab;
    [SerializeField] private GameObject _outgoingMessagePrefab;

    private string _connectionId;
    private bool _hasStartedConnecting;

    private void Awake()
    {
        for (int childIndex = _messageContent.childCount - 1; childIndex >= 0; childIndex--)
        {
            Destroy(_messageContent.GetChild(childIndex).gameObject);
        }

        _loginPanel.SetActive(true);
        _chatPanel.SetActive(false);
        _connectButton.onClick.AddListener(Connect);
        _sendButton.onClick.AddListener(SendMessage);
        _chatWebSocketClient.PacketReceived += HandlePacket;
        _chatWebSocketClient.ConnectionStateChanged += HandleConnectionStateChanged;
        _sendButton.interactable = false;
    }

    private void OnDestroy()
    {
        if (_connectButton != null)
        {
            _connectButton.onClick.RemoveListener(Connect);
        }

        if (_sendButton != null)
        {
            _sendButton.onClick.RemoveListener(SendMessage);
        }

        if (_chatWebSocketClient != null)
        {
            _chatWebSocketClient.PacketReceived -= HandlePacket;
            _chatWebSocketClient.ConnectionStateChanged -= HandleConnectionStateChanged;
        }
    }

    private void Connect()
    {
        string username = _usernameInput.text.Trim();
        if (username.Length == 0 || username.Length > ChatPacketLimits.UsernameLength)
        {
            _loginHint.text = "請輸入 1 到 24 個字元的使用者名稱。";
            return;
        }

        _loginHint.text = "正在連接聊天室...";
        _hasStartedConnecting = true;
        _connectButton.interactable = false;
        _chatWebSocketClient.Connect(username);
    }

    private void SendMessage()
    {
        string text = _messageInput.text.Trim();
        if (text.Length == 0 || text.Length > ChatPacketLimits.MessageLength || !_chatWebSocketClient.IsJoined)
        {
            return;
        }

        _chatWebSocketClient.SendChat(text);
        _messageInput.text = string.Empty;
    }

    private void HandlePacket(ChatPacket packet)
    {
        if (packet.Type == ChatPacketType.Joined)
        {
            _connectionId = packet.ConnectionId;
            _loginPanel.SetActive(false);
            _chatPanel.SetActive(true);
            _sendButton.interactable = true;
            _loginHint.text = string.Empty;
            return;
        }

        if (packet.Type == ChatPacketType.Chat)
        {
            CreateMessage(packet);
        }
        else if (packet.Type == ChatPacketType.Error)
        {
            _loginHint.text = packet.Text;
        }
    }

    private void HandleConnectionStateChanged(string status)
    {
        _channelStatus.text = status;
        bool joined = _chatWebSocketClient.IsJoined;
        _sendButton.interactable = joined;
        if (!_chatPanel.activeSelf)
        {
            _connectButton.interactable = !_hasStartedConnecting && !joined && status != "Connecting";
            if (status.StartsWith("Connection failed"))
            {
                _loginHint.text = status;
            }
        }
    }

    private void CreateMessage(ChatPacket packet)
    {
        bool isOutgoing = packet.ConnectionId == _connectionId;
        GameObject prefab = isOutgoing ? _outgoingMessagePrefab : _incomingMessagePrefab;
        GameObject message = Instantiate(prefab, _messageContent, false);
        TMP_Text author = message.transform.Find("Author")?.GetComponent<TMP_Text>();
        TMP_Text messageText = message.transform.Find("MessageText")?.GetComponent<TMP_Text>();
        if (author != null)
        {
            author.text = packet.Username;
        }

        if (messageText != null)
        {
            messageText.text = packet.Text;
            messageText.richText = false;
        }

        StartCoroutine(ScrollToLatestMessage());
    }

    private IEnumerator ScrollToLatestMessage()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        _messageScrollRect.verticalNormalizedPosition = 0f;
    }
}
}
