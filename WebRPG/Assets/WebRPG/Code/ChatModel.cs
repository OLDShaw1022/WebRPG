using System;
using System.Collections;
using System.Threading.Tasks;
using NativeWebSocket;
using Newtonsoft.Json;
using UnityEngine;
using WebRPGShared;

namespace WebRPG
{
    public sealed class ChatModel : MonoBehaviour, IChatModel
    {
        [SerializeField] private string _serverUrl = "wss://ws.oldshaw-webrpg.com/ws";

        private WebSocket _webSocket;
        private Coroutine _reconnectRoutine;
        private string _username;
        private bool _isJoining;
        private bool _hasJoined;
        private bool _isShuttingDown;

        public event Action<ChatPacket> PacketReceived;
        public event Action<string> ConnectionStateChanged;

        public bool IsJoined => _hasJoined && _webSocket?.State == WebSocketState.Open;

        public bool IsUsernameValid(string username)
        {
            string trimmedUsername = username?.Trim();
            return !string.IsNullOrEmpty(trimmedUsername)
                && trimmedUsername.Length <= ChatPacketLimits.UsernameLength;
        }

        public async Task ConnectAsync(string username)
        {
            if (_isShuttingDown || _isJoining || IsJoined)
            {
                return;
            }

            _username = username?.Trim();
            if (!IsUsernameValid(_username))
            {
                return;
            }

            await OpenConnectionAsync();
        }

        public async Task<bool> SendChatAsync(string text)
        {
            string trimmedText = text?.Trim();
            if (!IsJoined || string.IsNullOrEmpty(trimmedText) || trimmedText.Length > ChatPacketLimits.MessageLength)
            {
                return false;
            }

            WebSocket socket = _webSocket;
            await socket.SendText(JsonConvert.SerializeObject(new ChatPacket
            {
                Type = ChatPacketType.Chat,
                Text = trimmedText
            }));
            return socket == _webSocket && IsJoined;
        }

        private async Task OpenConnectionAsync()
        {
            _isJoining = true;
            _hasJoined = false;
            ConnectionStateChanged?.Invoke("Connecting");
            WebSocket socket = new(_serverUrl);
            _webSocket = socket;
            socket.OnOpen += () => _ = SendJoinAsync(socket);
            socket.OnMessage += bytes => HandlePacket(System.Text.Encoding.UTF8.GetString(bytes));
            socket.OnError += error => ConnectionStateChanged?.Invoke($"Connection error: {error}");
            socket.OnClose += code => HandleClosed(socket, code);

            try
            {
                await socket.Connect();
            }
            catch (Exception exception)
            {
                HandleConnectionFailure(socket, exception.Message);
            }
            finally
            {
                _isJoining = false;
            }
        }

        private async Task SendJoinAsync(WebSocket socket)
        {
            try
            {
                await socket.SendText(JsonConvert.SerializeObject(new ChatPacket
                {
                    Type = ChatPacketType.Join,
                    Username = _username
                }));
            }
            catch (Exception exception)
            {
                ConnectionStateChanged?.Invoke($"Connection error: {exception.Message}");
            }
        }

        private void HandlePacket(string json)
        {
            ChatPacket packet;
            try
            {
                packet = JsonConvert.DeserializeObject<ChatPacket>(json);
            }
            catch (JsonException exception)
            {
                ConnectionStateChanged?.Invoke($"Invalid server packet: {exception.Message}");
                return;
            }

            if (packet == null)
            {
                return;
            }

            if (packet.Type == ChatPacketType.Joined)
            {
                _hasJoined = true;
                ConnectionStateChanged?.Invoke("Connected");
            }
            else if (packet.Type == ChatPacketType.Error)
            {
                ConnectionStateChanged?.Invoke(packet.Text ?? "Server error");
            }

            PacketReceived?.Invoke(packet);
        }

        private void HandleClosed(WebSocket socket, WebSocketCloseCode code)
        {
            if (socket != _webSocket || _isShuttingDown)
            {
                return;
            }

            bool hadJoined = _hasJoined;
            _hasJoined = false;
            ConnectionStateChanged?.Invoke(hadJoined ? "Disconnected. Reconnecting..." : "Connection failed. Retrying...");
            if (_reconnectRoutine == null)
            {
                _reconnectRoutine = StartCoroutine(ReconnectAsync());
            }
        }

        private void HandleConnectionFailure(WebSocket socket, string error)
        {
            if (socket != _webSocket || _isShuttingDown)
            {
                return;
            }

            _hasJoined = false;
            ConnectionStateChanged?.Invoke($"Connection failed: {error}");
            if (_reconnectRoutine == null)
            {
                _reconnectRoutine = StartCoroutine(ReconnectAsync());
            }
        }

        private IEnumerator ReconnectAsync()
        {
            int[] delays = { 1, 2, 4, 8, 16, 30 };
            int attempt = 0;
            yield return new WaitUntil(() => !_isJoining || _isShuttingDown);
            while (!_isShuttingDown && !_hasJoined)
            {
                yield return new WaitForSecondsRealtime(delays[Math.Min(attempt, delays.Length - 1)]);
                if (_isShuttingDown || _hasJoined)
                {
                    break;
                }

                _ = OpenConnectionAsync();
                attempt++;
                yield return new WaitUntil(() => _hasJoined || !_isJoining || _isShuttingDown);
            }

            _reconnectRoutine = null;
        }

        private void OnApplicationQuit()
        {
            _isShuttingDown = true;
            if (_reconnectRoutine != null)
            {
                StopCoroutine(_reconnectRoutine);
                _reconnectRoutine = null;
            }

            _ = CloseConnectionAsync();
        }

        private async Task CloseConnectionAsync()
        {
            try
            {
                if (_webSocket != null)
                {
                    await _webSocket.Close();
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }
}
