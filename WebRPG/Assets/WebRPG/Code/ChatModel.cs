using System;
using System.Threading;
using NativeWebSocket;
using Newtonsoft.Json;
using UnityEngine;
using WebRPGShared;

namespace WebRPG
{
    public sealed class ChatModel : IChatModel
    {
        private const string ServerUrl = "wss://ws.oldshaw-webrpg.com/ws";

        private WebSocket _webSocket;
        private readonly CancellationTokenSource _shutdownCancellation = new CancellationTokenSource();
        private bool _isReconnecting;
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

        public async Awaitable ConnectAsync(string username)
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

        public async Awaitable<bool> SendChatAsync(string text)
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

        private async Awaitable OpenConnectionAsync()
        {
            _isJoining = true;
            _hasJoined = false;
            ConnectionStateChanged?.Invoke("Connecting");
            WebSocket socket = new(ServerUrl);
            _webSocket = socket;
            socket.OnOpen += () => SendJoinAsync(socket).Forget();
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

        private async Awaitable SendJoinAsync(WebSocket socket)
        {
            try
            {
                if (_isShuttingDown)
                {
                    await socket.Close();
                    return;
                }

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
            if (_isShuttingDown)
            {
                return;
            }

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
            StartReconnecting();
        }

        private void HandleConnectionFailure(WebSocket socket, string error)
        {
            if (socket != _webSocket || _isShuttingDown)
            {
                return;
            }

            _hasJoined = false;
            ConnectionStateChanged?.Invoke($"Connection failed: {error}");
            StartReconnecting();
        }

        private void StartReconnecting()
        {
            if (_isShuttingDown || _isReconnecting)
            {
                return;
            }

            _isReconnecting = true;
            ReconnectAsync().Forget();
        }

        private async Awaitable ReconnectAsync()
        {
            float[] delays = { 1f, 2f, 4f, 8f, 16f, 30f };
            int attempt = 0;
            CancellationToken cancellationToken = _shutdownCancellation.Token;

            try
            {
                await WaitForConnectionAttemptAsync(cancellationToken);
                while (!_isShuttingDown && !_hasJoined)
                {
                    await AwaitableExtensions.WaitForSecondsRealtimeAsync(delays[Math.Min(attempt, delays.Length - 1)], cancellationToken);
                    if (_isShuttingDown || _hasJoined)
                    {
                        break;
                    }

                    OpenConnectionAsync().Forget();
                    attempt++;
                    await WaitForConnectionAttemptAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                if (!_isShuttingDown)
                {
                    ConnectionStateChanged?.Invoke($"Connection failed: {exception.Message}");
                }
            }
            finally
            {
                _isReconnecting = false;
            }
        }

        private async Awaitable WaitForConnectionAttemptAsync(CancellationToken cancellationToken)
        {
            while (!_hasJoined && _isJoining && !_isShuttingDown)
            {
                await Awaitable.NextFrameAsync(cancellationToken);
            }
        }

        public async Awaitable DisposeAsync()
        {
            if (_isShuttingDown)
            {
                return;
            }

            _isShuttingDown = true;
            _hasJoined = false;
            _shutdownCancellation.Cancel();

            try
            {
                if (_webSocket != null)
                {
                    await _webSocket.Close();
                }
            }
            finally
            {
                _shutdownCancellation.Dispose();
            }
        }
    }
}
