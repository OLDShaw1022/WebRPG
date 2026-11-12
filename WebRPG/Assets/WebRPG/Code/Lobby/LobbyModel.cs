using System;
using System.Collections.Generic;
using System.Linq;
using NativeWebSocket;
using Newtonsoft.Json;
using UnityEngine;
using WebRPGShared;

namespace WebRPG
{
    // 管理房間大廳的 WebSocket、要求狀態與完整伺服器快照。
    public sealed class LobbyModel : ILobbyModel
    {
        private const string ConnectingMessage = "正在連線…";
        private const string ConnectedMessage = "已連線，可建立或加入房間。";
        private const string DisconnectedMessage = "連線已中斷，請返回主選單後重新進入大廳。";
        private const string InitialMessage = "準備連線…";
        private const string ConnectionFailedFormat = "連線失敗：{0}。請返回主選單後重新進入大廳。";
        private const string InvalidPacketMessage = "伺服器回應格式錯誤。";
        private const string InvalidNameMessage = "玩家名稱須為 1～24 個字元。";
        private const string InvalidServerMessage = "登入伺服器設定無效。請重新登入。";
        private const string PendingMessage = "正在等待伺服器回應…";
        private const string TimeoutMessage = "伺服器回應逾時，請返回主選單後重新進入大廳。";
        private readonly LobbyServerConfiguration[] _servers;
        private readonly float _requestTimeoutSeconds;
        private readonly AccountModel _account;
        private const string TokenProperty = "AccessToken";
        private const string AuthenticationRequiredMessage = "請重新登入後再連線。";

        private const int MaximumRoomLogEntries = 80;
        private const string ChatLineFormat = "【聊天】{0}：{1}";
        private const string JoinedLineFormat = "【房間】{0} 加入房間。";
        private const string LeftLineFormat = "【房間】{0} 離開房間。";
        private const string ChatTimeoutMessage = "聊天送出逾時，可重新送出。";
        private const string ChatSendFailureMessage = "無法送出聊天，請再試一次。";
        private const string ChatLengthMessage = "聊天訊息長度超過上限。";
        private const string NewLine = "\n";
        private readonly Queue<string> _roomLog = new Queue<string>();
        private string _chatRequestId = string.Empty;
        private string _chatDraft = string.Empty;
        private float _chatTime;

        public event Action ChatChanged;
        public event Action<string> RoomLogEntryAdded;
        public event Action<string> ChatDelivered;
        public string RoomLog { get; private set; } = string.Empty;
        public string ChatError { get; private set; } = string.Empty;
        public bool CanChat => IsConnected && Room != null && _chatRequestId.Length == 0;

        private readonly Dictionary<string, string> _adventureRequests = new Dictionary<string, string>();
        private WebSocket _socket;
        private bool _disposed;
        private int _requestNumber;
        private string _pendingId = string.Empty;
        private float _pendingTime;
        public event Action Changed;
        public event Action<AdventurePacket> AdventureReceived;
        public bool IsConnected { get; private set; }
        public bool IsBusy { get; private set; }
        public string PlayerId { get; private set; } = string.Empty;
        public string Status { get; private set; } = InitialMessage;
        public LobbyRoomSnapshot Room { get; private set; }
        public LobbyRoomSummary[] Rooms { get; private set; } = Array.Empty<LobbyRoomSummary>();

        public LobbyModel(LobbyClientConfiguration configuration, bool includeEditorServers = false, AccountModel account = null)
        {
            configuration.Validate();
            _account = account;
            _requestTimeoutSeconds = configuration.RequestTimeoutSeconds;
            _servers = configuration.GetAvailableServers(includeEditorServers);
        }

        public async Awaitable ConnectAsync()
        {
            if (_disposed || IsBusy || IsConnected) return;
            if (_account == null || !_account.IsLoggedIn)
            {
                Status = AuthenticationRequiredMessage;
                Changed?.Invoke();
                return;
            }
            int serverIndex = _account.ServerIndex;
            string name = _account.Username.Trim();
            if (name.Length == 0 || name.Length > LobbyPacketLimits.UsernameLength)
            {
                Status = InvalidNameMessage;
                Changed?.Invoke();
                return;
            }
            if (serverIndex < 0 || serverIndex >= _servers.Length)
            {
                Status = InvalidServerMessage;
                Changed?.Invoke();
                return;
            }
            WebSocket socket = new WebSocket(_servers[serverIndex].Url);
            _socket = socket;
            IsBusy = true;
            _pendingTime = 0f;
            Status = ConnectingMessage;
            socket.OnOpen += () => SendHelloAsync(socket, name).Forget();
            socket.OnMessage += bytes =>
            {
                if (!_disposed && socket == _socket)
                    HandlePacket(System.Text.Encoding.UTF8.GetString(bytes));
            };
            socket.OnError += error =>
            {
                if (!_disposed && socket == _socket)
                    Disconnect(string.Format(ConnectionFailedFormat, error));
            };
            socket.OnClose += code =>
            {
                if (!_disposed && socket == _socket) Disconnect(DisconnectedMessage);
            };
            Changed?.Invoke();
            try { await socket.Connect(); }
            catch (Exception exception)
            {
                if (!_disposed && socket == _socket)
                    Disconnect(string.Format(ConnectionFailedFormat, exception.Message));
            }
        }

        private async Awaitable SendHelloAsync(WebSocket socket, string username)
        {
            if (_disposed || socket != _socket)
            {
                await CloseSocketAsync(socket);
                return;
            }
            await SendPacketAsync(new LobbyPacket { Type = LobbyPacketType.Hello, Username = username });
        }

        public async Awaitable SendAsync(LobbyPacket packet)
        {
            if (_disposed || !IsConnected || IsBusy) return;
            await SendPacketAsync(packet);
        }

        public async Awaitable SendChatAsync(string draft)
        {
            if (_disposed || !CanChat || string.IsNullOrWhiteSpace(draft)) return;
            string message = draft.Trim();
            if (message.Length > LobbyPacketLimits.ChatMessageLength)
            {
                ChatError = ChatLengthMessage;
                ChatChanged?.Invoke();
                return;
            }

            string requestId = (++_requestNumber).ToString();
            _chatRequestId = requestId;
            _chatDraft = draft;
            _chatTime = 0f;
            ChatError = string.Empty;
            ChatChanged?.Invoke();
            await LobbyPacketSendHelper.SendAsync(_socket, new LobbyPacket
            {
                Type = LobbyPacketType.RoomChat,
                RequestId = requestId,
                Message = message
            }, (_, exception) =>
            {
                if (_chatRequestId == requestId) CompleteChat(ChatSendFailureMessage);
            });
        }

        public async Awaitable SendAdventureAsync(AdventurePacket packet)
        {
            if (_disposed || !IsConnected || packet == null) return;
            WebSocket socket = _socket;
            packet.RequestId = (++_requestNumber).ToString();
            _adventureRequests.Add(packet.RequestId, Room?.Id ?? string.Empty);
            await LobbyPacketSendHelper.SendAsync(socket, new LobbyPacket
            {
                Type = LobbyPacketType.Adventure,
                RequestId = packet.RequestId,
                Adventure = packet
            }, HandleSendFailure);
        }

        private async Awaitable SendPacketAsync(LobbyPacket packet)
        {
            WebSocket socket = _socket;
            packet.RequestId = (++_requestNumber).ToString();
            _pendingId = packet.RequestId;
            _pendingTime = 0f;
            IsBusy = true;
            Status = PendingMessage;
            Changed?.Invoke();
            if (packet.Type == LobbyPacketType.Hello && _account != null)
            {
                Newtonsoft.Json.Linq.JObject json = Newtonsoft.Json.Linq.JObject.FromObject(packet);
                json[TokenProperty] = _account.AccessToken;
                try { await socket.SendText(json.ToString(Formatting.None)); }
                catch (Exception exception) { HandleSendFailure(socket, exception); }
            }
            else await LobbyPacketSendHelper.SendAsync(socket, packet, HandleSendFailure);
        }

        private void HandleSendFailure(WebSocket socket, Exception exception)
        {
            if (!_disposed && socket == _socket)
                Disconnect(string.Format(ConnectionFailedFormat, exception.Message));
        }

        private void HandlePacket(string json)
        {
            LobbyPacket packet;
            try { packet = JsonConvert.DeserializeObject<LobbyPacket>(json); }
            catch (JsonException) { Disconnect(InvalidPacketMessage); return; }
            if (packet == null) { Disconnect(InvalidPacketMessage); return; }
            if (packet.Type == LobbyPacketType.RoomChat)
            {
                if (Room == null || packet.RoomId != Room.Id) return;
                AppendRoomLog(string.Format(ChatLineFormat, packet.Username, packet.Message));
                if (_chatRequestId.Length != 0 && packet.RequestId == _chatRequestId)
                {
                    ChatDelivered?.Invoke(_chatDraft);
                    CompleteChat(string.Empty);
                }
                else ChatChanged?.Invoke();
                return;
            }
            if (packet.Type == LobbyPacketType.Adventure)
            {
                if (packet.Adventure != null)
                {
                    if (packet.Adventure.PlayerId == PlayerId && !string.IsNullOrEmpty(packet.Adventure.RequestId))
                        _adventureRequests.Remove(packet.Adventure.RequestId);
                    AdventureReceived?.Invoke(packet.Adventure);
                }
                return;
            }
            if (packet.Type == LobbyPacketType.Welcome || packet.Type == LobbyPacketType.State)
            {
                PlayerId = packet.PlayerId;
                LobbyRoomSnapshot previousRoom = Room;
                Room = packet.Room;
                UpdateRoomLog(previousRoom);
                Rooms = packet.Rooms ?? Array.Empty<LobbyRoomSummary>();
                IsConnected = true;
                ChatChanged?.Invoke();
                if (!string.IsNullOrEmpty(_pendingId) && packet.RequestId == _pendingId)
                {
                    IsBusy = false;
                    _pendingId = string.Empty;
                    Status = ConnectedMessage;
                }
            }
            else if (packet.Type == LobbyPacketType.Error)
            {
                if (string.IsNullOrEmpty(packet.RequestId)) return;
                if (packet.RequestId == _chatRequestId)
                {
                    CompleteChat(packet.Error);
                    return;
                }
                if (_adventureRequests.TryGetValue(packet.RequestId, out string roomId))
                {
                    _adventureRequests.Remove(packet.RequestId);
                    AdventureReceived?.Invoke(new AdventurePacket
                    {
                        Type = AdventurePacketType.Error, RequestId = packet.RequestId,
                        RoomId = roomId, PlayerId = PlayerId, Error = packet.Error
                    });
                    if (!IsBusy && Room?.Id == roomId)
                    {
                        Status = packet.Error;
                        Changed?.Invoke();
                    }
                    return;
                }
                if (string.IsNullOrEmpty(_pendingId) || packet.RequestId != _pendingId) return;
                IsBusy = false;
                _pendingId = string.Empty;
                Status = packet.Error;
            }
            else return;
            Changed?.Invoke();
        }

        public void Tick(float deltaTime)
        {
            if (_disposed) return;
            if (_chatRequestId.Length != 0)
            {
                _chatTime += deltaTime;
                if (_chatTime >= _requestTimeoutSeconds) CompleteChat(ChatTimeoutMessage);
            }
            if (!IsBusy) return;
            _pendingTime += deltaTime;
            if (_pendingTime >= _requestTimeoutSeconds) Disconnect(TimeoutMessage);
        }

        private void UpdateRoomLog(LobbyRoomSnapshot previousRoom)
        {
            if (Room?.Id != previousRoom?.Id) ClearRoomLog();
            if (Room == null) return;
            LobbyMemberSnapshot[] previousMembers = previousRoom?.Id == Room.Id
                ? previousRoom.Members : Array.Empty<LobbyMemberSnapshot>();
            foreach (LobbyMemberSnapshot member in previousMembers)
                if (!Room.Members.Any(current => current.PlayerId == member.PlayerId))
                    AppendRoomLog(string.Format(LeftLineFormat, member.Username));
            foreach (LobbyMemberSnapshot member in Room.Members)
                if (!previousMembers.Any(previous => previous.PlayerId == member.PlayerId))
                    AppendRoomLog(string.Format(JoinedLineFormat, member.Username));
        }

        private void AppendRoomLog(string line)
        {
            _roomLog.Enqueue(line);
            if (_roomLog.Count > MaximumRoomLogEntries) _roomLog.Dequeue();
            RoomLog = string.Join(NewLine, _roomLog);
            RoomLogEntryAdded?.Invoke(line);
        }

        private void ClearRoomLog()
        {
            _roomLog.Clear();
            RoomLog = string.Empty;
            CompleteChat(string.Empty);
        }

        private void CompleteChat(string error)
        {
            _chatRequestId = string.Empty;
            _chatDraft = string.Empty;
            ChatError = error;
            ChatChanged?.Invoke();
        }

        private void Disconnect(string message)
        {
            WebSocket socket = _socket;
            _socket = null;
            _adventureRequests.Clear();
            IsConnected = false;
            IsBusy = false;
            PlayerId = string.Empty;
            Room = null;
            ClearRoomLog();
            Rooms = Array.Empty<LobbyRoomSummary>();
            _pendingId = string.Empty;
            Status = message;
            if (socket != null) CloseSocketAsync(socket).Forget();
            Changed?.Invoke();
        }

        public async Awaitable DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            WebSocket socket = _socket;
            _socket = null;
            _adventureRequests.Clear();
            IsConnected = false;
            IsBusy = false;
            Room = null;
            ClearRoomLog();
            Rooms = Array.Empty<LobbyRoomSummary>();
            if (socket != null) await CloseSocketAsync(socket);
        }

        private async Awaitable CloseSocketAsync(WebSocket socket)
        {
            try { await socket.Close(); }
            catch (Exception exception) { Debug.LogException(exception); }
            finally
            {
#if !UNITY_WEBGL || UNITY_EDITOR
                socket.CancelConnection();
#endif
            }
        }
    }
}
