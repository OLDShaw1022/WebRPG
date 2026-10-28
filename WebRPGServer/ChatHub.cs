// 管理聊天室連線、封包驗證、訊息廣播與歷史訊息載入。
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using WebRPGShared;

namespace WebRPGServer;

public sealed class ChatHub
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false
    };

    private readonly ConcurrentDictionary<string, ChatSession> _sessions = new();
    private readonly IChatMessageStore? _messageStore;

    public ChatHub(IChatMessageStore? messageStore = null)
    {
        _messageStore = messageStore;
    }

    public async Task HandleConnectionAsync(WebSocket webSocket, CancellationToken cancellationToken)
    {
        ChatSession? session = null;
        try
        {
            while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                string? json = await ReceiveTextAsync(webSocket, cancellationToken);
                if (json is null)
                {
                    break;
                }

                ChatPacket? packet;
                try
                {
                    packet = JsonSerializer.Deserialize<ChatPacket>(json, JsonOptions);
                }
                catch (JsonException)
                {
                    await SendErrorAsync(session, webSocket, "Invalid JSON packet.", cancellationToken);
                    continue;
                }

                if (packet is null)
                {
                    await SendErrorAsync(session, webSocket, "Packet is empty.", cancellationToken);
                    continue;
                }

                if (session is null)
                {
                    if (!string.Equals(packet.Type, ChatPacketType.Join, StringComparison.Ordinal))
                    {
                        await SendErrorAsync(null, webSocket, "Join is required before chatting.", cancellationToken);
                        continue;
                    }

                    string username = packet.Username?.Trim() ?? string.Empty;
                    if (username.Length == 0 || username.Length > ChatPacketLimits.UsernameLength)
                    {
                        await SendErrorAsync(null, webSocket, "Username must contain 1 to 24 characters.",
                            cancellationToken);
                        continue;
                    }

                    session = new ChatSession(Guid.NewGuid().ToString("N"), username, webSocket);
                    await SendAsync(session, new ChatPacket
                    {
                        Type = ChatPacketType.Joined,
                        Username = session.Username,
                        ConnectionId = session.ConnectionId
                    }, cancellationToken);

                    if (_messageStore is not null)
                    {
                        await session.SendLock.WaitAsync(cancellationToken);
                        try
                        {
                            _sessions.TryAdd(session.ConnectionId, session);
                            IReadOnlyList<ChatPacket> history =
                                await _messageStore.GetRecentMessagesAsync(50, cancellationToken);

                            foreach (ChatPacket historyMessage in history)
                            {
                                await SendUnlockedAsync(session, historyMessage, cancellationToken);
                            }
                        }
                        finally
                        {
                            session.SendLock.Release();
                        }
                    }
                    else
                    {
                        _sessions.TryAdd(session.ConnectionId, session);
                    }

                    Console.WriteLine($"{session.Username} joined ({session.ConnectionId}).");
                    continue;
                }

                if (!string.Equals(packet.Type, ChatPacketType.Chat, StringComparison.Ordinal))
                {
                    await SendErrorAsync(session, webSocket, "Unsupported packet type.", cancellationToken);
                    continue;
                }

                string text = packet.Text?.Trim() ?? string.Empty;
                if (text.Length == 0 || text.Length > ChatPacketLimits.MessageLength)
                {
                    await SendErrorAsync(session, webSocket, "Message must contain 1 to 200 characters.",
                        cancellationToken);
                    continue;
                }

                ChatPacket message = new()
                {
                    Type = ChatPacketType.Chat,
                    Username = session.Username,
                    Text = text,
                    ConnectionId = session.ConnectionId,
                    TimestampUtc = DateTimeOffset.UtcNow
                };

                if (_messageStore is not null)
                {
                    await _messageStore.SaveMessageAsync(message, cancellationToken);
                }

                await BroadcastAsync(message, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (WebSocketException exception)
        {
            Console.WriteLine($"WebSocket disconnected: {exception.Message}");
        }
        finally
        {
            if (session is not null)
            {
                _sessions.TryRemove(session.ConnectionId, out _);
                Console.WriteLine($"{session.Username} left.");
            }

            if (webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnected",
                        CancellationToken.None);
                }
                catch (WebSocketException)
                {
                }
            }
        }
    }

    private async Task BroadcastAsync(ChatPacket packet, CancellationToken cancellationToken)
    {
        string json = JsonSerializer.Serialize(packet, JsonOptions);
        byte[] payload = Encoding.UTF8.GetBytes(json);
        foreach (ChatSession session in _sessions.Values)
        {
            if (session.Socket.State != WebSocketState.Open)
            {
                continue;
            }

            await session.SendLock.WaitAsync(cancellationToken);
            try
            {
                if (session.Socket.State == WebSocketState.Open)
                {
                    await session.Socket.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken);
                }
            }
            catch (WebSocketException)
            {
                _sessions.TryRemove(session.ConnectionId, out _);
            }
            finally
            {
                session.SendLock.Release();
            }
        }
    }

    private static async Task SendErrorAsync(ChatSession? session, WebSocket socket, string text,
        CancellationToken cancellationToken)
    {
        ChatPacket packet = new() { Type = ChatPacketType.Error, Text = text };
        if (session is null)
        {
            byte[] payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(packet, JsonOptions));
            await socket.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken);
            return;
        }

        await SendAsync(session, packet, cancellationToken);
    }

    private static async Task SendAsync(ChatSession session, ChatPacket packet, CancellationToken cancellationToken)
    {
        await session.SendLock.WaitAsync(cancellationToken);
        try
        {
            await SendUnlockedAsync(session, packet, cancellationToken);
        }
        finally
        {
            session.SendLock.Release();
        }
    }

    private static async Task SendUnlockedAsync(
        ChatSession session,
        ChatPacket packet,
        CancellationToken cancellationToken)
    {
        if (session.Socket.State != WebSocketState.Open)
        {
            return;
        }

        byte[] payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(packet, JsonOptions));
        await session.Socket.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken);
    }

    private static async Task<string?> ReceiveTextAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[4096];
        using MemoryStream message = new();
        WebSocketReceiveResult result;
        bool isTextMessage = true;
        do
        {
            result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            isTextMessage &= result.MessageType == WebSocketMessageType.Text;
            if (isTextMessage)
            {
                message.Write(buffer, 0, result.Count);
            }
        } while (!result.EndOfMessage);

        return isTextMessage ? Encoding.UTF8.GetString(message.ToArray()) : string.Empty;
    }

    private sealed class ChatSession
    {
        public ChatSession(string connectionId, string username, WebSocket socket)
        {
            ConnectionId = connectionId;
            Username = username;
            Socket = socket;
        }

        public string ConnectionId { get; }
        public string Username { get; }
        public WebSocket Socket { get; }
        public SemaphoreSlim SendLock { get; } = new(1, 1);
    }
}