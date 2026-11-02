using System;
using System.Linq;
using NativeWebSocket;
using Newtonsoft.Json;
using UnityEngine;
using WebRPGShared;

namespace WebRPG
{
    // 定義大廳 Presenter 使用的連線與伺服器狀態介面。
    public interface ILobbyModel
    {
        public event Action Changed;
        public string[] ServerNames { get; }
        public int DefaultServerIndex { get; }
        public bool IsConnected { get; }
        public bool IsBusy { get; }
        public string PlayerId { get; }
        public string Status { get; }
        public LobbyRoomSnapshot Room { get; }
        public LobbyRoomSummary[] Rooms { get; }
        public Awaitable ConnectAsync(int serverIndex, string username);
        public Awaitable SendAsync(LobbyPacket packet);
    }

    // 管理房間大廳的 WebSocket、要求狀態與完整伺服器快照。
    public sealed class LobbyModel : ILobbyModel
    {
        private const string ConnectingMessage = "正在連線…";
        private const string ConnectedMessage = "已連線，可建立或加入房間。";
        private const string DisconnectedMessage = "連線已中斷，請重新連線。";
        private const string InitialMessage = "請輸入玩家名稱並連線。";
        private const string ConnectionFailedFormat = "連線失敗：{0}";
        private const string InvalidPacketMessage = "伺服器回應格式錯誤。";
        private const string InvalidNameMessage = "玩家名稱須為 1～24 個字元。";
        private const string InvalidServerMessage = "請選擇有效的伺服器。";
        private const string PendingMessage = "正在等待伺服器回應…";
        private const string TimeoutMessage = "伺服器回應逾時，請重新連線。";
        private readonly LobbyServerConfiguration[] _servers;
        private readonly float _requestTimeoutSeconds;

        private WebSocket _socket;
        private bool _disposed;
        private int _requestNumber;
        private string _pendingId = string.Empty;
        private float _pendingTime;
        public event Action Changed;
        public string[] ServerNames { get; }
        public int DefaultServerIndex { get; }
        public bool IsConnected { get; private set; }
        public bool IsBusy { get; private set; }
        public string PlayerId { get; private set; } = string.Empty;
        public string Status { get; private set; } = InitialMessage;
        public LobbyRoomSnapshot Room { get; private set; }
        public LobbyRoomSummary[] Rooms { get; private set; } = Array.Empty<LobbyRoomSummary>();

        public LobbyModel(LobbyClientConfiguration configuration, bool includeEditorServers = false)
        {
            configuration.Validate();
            _requestTimeoutSeconds = configuration.RequestTimeoutSeconds;
            _servers = configuration.GetAvailableServers(includeEditorServers);
            ServerNames = _servers.Select(server => server.Name).ToArray();
            DefaultServerIndex = Array.FindIndex(_servers, server => server.Id == configuration.DefaultServerId);
        }

        public async Awaitable ConnectAsync(int serverIndex, string username)
        {
            if (_disposed || IsBusy || IsConnected) return;
            string name = username?.Trim() ?? string.Empty;
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

        private async Awaitable SendPacketAsync(LobbyPacket packet)
        {
            WebSocket socket = _socket;
            packet.RequestId = (++_requestNumber).ToString();
            _pendingId = packet.RequestId;
            _pendingTime = 0f;
            IsBusy = true;
            Status = PendingMessage;
            Changed?.Invoke();
            try { await socket.SendText(JsonConvert.SerializeObject(packet)); }
            catch (Exception exception)
            {
                if (!_disposed && socket == _socket)
                    Disconnect(string.Format(ConnectionFailedFormat, exception.Message));
            }
        }

        private void HandlePacket(string json)
        {
            LobbyPacket packet;
            try { packet = JsonConvert.DeserializeObject<LobbyPacket>(json); }
            catch (JsonException) { Disconnect(InvalidPacketMessage); return; }
            if (packet == null) { Disconnect(InvalidPacketMessage); return; }
            if (packet.Type == LobbyPacketType.Welcome || packet.Type == LobbyPacketType.State)
            {
                PlayerId = packet.PlayerId;
                Room = packet.Room;
                Rooms = packet.Rooms ?? Array.Empty<LobbyRoomSummary>();
                IsConnected = true;
                if (packet.RequestId == _pendingId)
                {
                    IsBusy = false;
                    _pendingId = string.Empty;
                    Status = ConnectedMessage;
                }
            }
            else if (packet.Type == LobbyPacketType.Error)
            {
                IsBusy = false;
                _pendingId = string.Empty;
                Status = packet.Error;
            }
            else return;
            Changed?.Invoke();
        }

        public void Tick(float deltaTime)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            _socket?.DispatchMessageQueue();
#endif
            if (!IsBusy || _disposed) return;
            _pendingTime += deltaTime;
            if (_pendingTime >= _requestTimeoutSeconds) Disconnect(TimeoutMessage);
        }

        private void Disconnect(string message)
        {
            WebSocket socket = _socket;
            _socket = null;
            IsConnected = false;
            IsBusy = false;
            PlayerId = string.Empty;
            Room = null;
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
            IsConnected = false;
            IsBusy = false;
            Room = null;
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
