using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using WebRPGShared;

namespace WebRPG
{
    // 綁定大廳 Prefab 的固定控制項並顯示房間與成員。
    public sealed class LobbyView : MonoBehaviour, ILobbyView
    {
        [SerializeField] private GameObject _browsePage;
        [SerializeField] private GameObject _roomPage;
        [SerializeField] private RoomChatView _roomChat;
        public RoomChatView RoomChat => _roomChat;

        [SerializeField] private TMP_Dropdown _serverDropdown;
        [SerializeField] private TMP_InputField _username;
        [SerializeField] private TMP_InputField _roomName;
        [SerializeField] private TMP_InputField _roomId;
        [SerializeField] private UnityEngine.UI.Toggle _publicToggle;
        [SerializeField] private UnityEngine.UI.Button _connectButton;
        [SerializeField] private UnityEngine.UI.Button _createButton;
        [SerializeField] private UnityEngine.UI.Button _joinButton;
        [SerializeField] private UnityEngine.UI.Button _refreshButton;
        [SerializeField] private UnityEngine.UI.Button _leaveButton;
        [SerializeField] private UnityEngine.UI.Button _readyButton;
        [SerializeField] private TextMeshProUGUI _readyLabel;
        [SerializeField] private UnityEngine.UI.Button _backButton;
        [SerializeField] private TextMeshProUGUI _status;
        [SerializeField] private TextMeshProUGUI _members;
        [SerializeField] private RectTransform _rooms;
        [SerializeField] private LobbyRoomRowView _roomTemplate;
        private readonly List<LobbyRoomRowView> _rows = new List<LobbyRoomRowView>();
        private bool _hasAccount;
        public event Action<int, string> ConnectRequested;
        public event Action<string, bool> CreateRequested;
        public event Action<string> JoinRequested;
        public event Action RefreshRequested;
        public event Action LeaveRequested;
        public event Action ReadyRequested;
        public event Action BackRequested;

        private void Awake()
        {
            _connectButton.onClick.AddListener(Connect);
            _createButton.onClick.AddListener(Create);
            _joinButton.onClick.AddListener(Join);
            _refreshButton.onClick.AddListener(Refresh);
            _leaveButton.onClick.AddListener(Leave);
            _readyButton.onClick.AddListener(Ready);
            _backButton.onClick.AddListener(Back);
            _roomTemplate.gameObject.SetActive(false);
            _status.richText = false;
            _members.richText = false;
        }

        public void ConfigureServers(string[] names, int defaultServerIndex)
        {
            _serverDropdown.ClearOptions();
            _serverDropdown.AddOptions(new List<string>(names));
            if (!_hasAccount) _serverDropdown.SetValueWithoutNotify(defaultServerIndex);
            _serverDropdown.RefreshShownValue();
        }

        public void Render(LobbyScreenModel screen)
        {
            if (_browsePage) _browsePage.SetActive(!screen.InRoom);
            if (_roomPage) _roomPage.SetActive(screen.InRoom);
            _status.text = screen.Status;
            _members.text = screen.Members;
            _readyLabel.text = screen.ReadyLabel;
            bool available = screen.Connected && !screen.Busy;
            _serverDropdown.interactable = !_hasAccount && !screen.Connected && !screen.Busy;
            _username.interactable = !_hasAccount && !screen.Connected && !screen.Busy;
            _connectButton.interactable = !screen.Connected && !screen.Busy;
            _createButton.interactable = available && !screen.InRoom;
            _joinButton.interactable = available && !screen.InRoom;
            _roomName.interactable = available && !screen.InRoom;
            _roomId.interactable = available && !screen.InRoom;
            _publicToggle.interactable = available && !screen.InRoom;
            _refreshButton.interactable = available;
            _leaveButton.interactable = available && screen.InRoom;
            _readyButton.interactable = available && screen.InRoom;
            while (_rows.Count < screen.Rooms.Length)
                _rows.Add(Instantiate(_roomTemplate, _rooms));
            for (int index = 0; index < _rows.Count; index++)
            {
                bool visible = index < screen.Rooms.Length;
                _rows[index].gameObject.SetActive(visible);
                if (visible) _rows[index].Bind(screen.Rooms[index], available && !screen.InRoom, SelectRoom);
            }
        }

        private void Connect() => ConnectRequested?.Invoke(_serverDropdown.value, _username.text);
        public void SetAccount(string username, int serverIndex)
        {
            _hasAccount = true;
            _username.text = username;
            _serverDropdown.SetValueWithoutNotify(serverIndex);
            _serverDropdown.interactable = false;
            _username.interactable = false;
        }
        private void Create() => CreateRequested?.Invoke(_roomName.text, _publicToggle.isOn);
        private void Join() => JoinRequested?.Invoke(_roomId.text);
        private void SelectRoom(string roomId) => JoinRequested?.Invoke(roomId);
        private void Refresh() => RefreshRequested?.Invoke();
        private void Leave() => LeaveRequested?.Invoke();
        private void Ready() => ReadyRequested?.Invoke();
        private void Back() => BackRequested?.Invoke();
    }
}
