using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;
using WebRPGShared;

namespace WebRPGServer;

// 序列化房間操作並向所有已連線玩家推播伺服器狀態。
public sealed class LobbyHub
{
    private const int MaximumRequestIdLength = 64;
    private static readonly TimeSpan ChatSendInterval = TimeSpan.FromSeconds(1);

    private readonly LobbyOptions _options;
    private readonly LobbyRegistry _registry;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, LobbySendSession> _sessions = new();

    public LobbyHub(LobbyOptions options)
    {
        _options = options;
        _registry = new LobbyRegistry(options);
    }

    public async Task HandleConnectionAsync(WebSocket socket, CancellationToken token,
        Func<string, Task<AccountIdentity?>>? authenticate = null)
    {
        string playerId = Guid.NewGuid().ToString("N");
        long? lastChat = null;
        bool registered = false;
        string accessToken = string.Empty;
        LobbySendSession session = new(socket, _options.SendTimeoutSeconds, token);
        try
        {
            while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                string? json = await LobbySocketHelper.ReceiveAsync(socket, token);
                if (json is null) break;
                LobbyPacket? request;
                try { request = JsonSerializer.Deserialize<LobbyPacket>(json); }
                catch (JsonException) { request = null; }
                AccountIdentity? identity = null;
                if (authenticate is not null && request is not null)
                {
                    if (!registered)
                    {
                        using JsonDocument document = JsonDocument.Parse(json);
                        if (document.RootElement.TryGetProperty("AccessToken", out JsonElement property) &&
                            property.ValueKind == JsonValueKind.String) accessToken = property.GetString()
                                ?? string.Empty;
                    }
                    identity = await authenticate(accessToken);
                    if (identity is null)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Authentication required", token);
                        break;
                    }
                    if (!registered) playerId = identity.AccountId;
                }
                await _gate.WaitAsync(token);
                try
                {
                    if (request is null)
                    {
                        EnqueueError(session, string.Empty, "封包格式錯誤或超過大小上限。");
                        continue;
                    }
                    if (request.RequestId?.Length > MaximumRequestIdLength)
                    {
                        EnqueueError(session, string.Empty, "要求識別碼過長。");
                        continue;
                    }
                    if (!registered)
                    {
                        if (request.Type != LobbyPacketType.Hello) throw new ArgumentException("請先連線。");
                        if (_sessions.Count >= _options.MaximumConnections) throw new ArgumentException("大廳連線已滿。");
                        if (_sessions.ContainsKey(playerId)) throw new ArgumentException("此帳號已連線至大廳。");
                        _registry.Connect(playerId, identity?.Username ?? request.Username);
                        _sessions.Add(playerId, session);
                        registered = true;
                        LobbyPacket welcome = _registry.Snapshot(playerId, request.RequestId ?? string.Empty);
                        welcome.Type = LobbyPacketType.Welcome;
                        session.Enqueue(welcome);
                        continue;
                    }
                    if (request.Type == LobbyPacketType.RoomChat)
                    {
                        if (lastChat.HasValue && Stopwatch.GetElapsedTime(lastChat.Value) < ChatSendInterval)
                            throw new ArgumentException("請稍候再傳送聊天訊息。");
                        BroadcastRoomChat(playerId, request);
                        lastChat = Stopwatch.GetTimestamp();
                        continue;
                    }
                    if (request.Type == LobbyPacketType.Adventure)
                    {
                        AdventurePacket adventure = _registry.ExecuteAdventure(playerId, request.Adventure);
                        adventure.RequestId = request.RequestId ?? string.Empty;
                        BroadcastAdventure(adventure, _registry.GetRoomPlayers(playerId));
                        if (adventure.Type == AdventurePacketType.Start) Broadcast(string.Empty);
                        continue;
                    }
                    string[] affected = _registry.GetRoomPlayers(playerId);
                    AdventurePacket? failure = request.Type == LobbyPacketType.LeaveRoom
                        ? _registry.FailAdventure(playerId) : null;
                    bool changed = _registry.Execute(playerId, request);
                    if (failure != null) BroadcastAdventure(failure, affected);
                    session.Enqueue(_registry.Snapshot(playerId, request.RequestId ?? string.Empty));
                    if (changed) Broadcast(playerId);
                }
                catch (ArgumentException exception)
                {
                    EnqueueError(session, request?.RequestId ?? string.Empty, exception.Message);
                }
                finally { _gate.Release(); }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (WebSocketException) { }
        finally
        {
            await _gate.WaitAsync();
            try
            {
                if (registered)
                {
                    string[] affected = _registry.GetRoomPlayers(playerId);
                    AdventurePacket? failure = _registry.FailAdventure(playerId);
                    _sessions.Remove(playerId);
                    if (failure != null) BroadcastAdventure(failure, affected);
                    _registry.Disconnect(playerId);
                    Broadcast(playerId);
                }
            }
            finally { _gate.Release(); }
            await session.DisposeAsync();
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(_options.SendTimeoutSeconds));
                try { await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnected", timeout.Token); }
                catch (Exception exception) when (
                    exception is WebSocketException or OperationCanceledException) { socket.Abort(); }
            }
        }
    }

    private void Broadcast(string excludedPlayerId)
    {
        foreach (KeyValuePair<string, LobbySendSession> pair in _sessions)
        {
            if (pair.Key != excludedPlayerId) pair.Value.Enqueue(_registry.Snapshot(pair.Key));
        }
    }

    private void BroadcastRoomChat(string playerId, LobbyPacket request)
    {
        LobbyRoomSnapshot? room = _registry.Snapshot(playerId).Room;
        if (room is null) throw new ArgumentException("加入房間後才能聊天。");
        string message = request.Message?.Trim() ?? string.Empty;
        if (message.Length == 0 || message.Length > LobbyPacketLimits.ChatMessageLength ||
            message.Any(char.IsControl))
            throw new ArgumentException("請輸入有效的單行聊天訊息。");

        LobbyMemberSnapshot sender = room.Members.Single(member => member.PlayerId == playerId);
        foreach (LobbyMemberSnapshot member in room.Members)
        {
            if (!_sessions.TryGetValue(member.PlayerId, out LobbySendSession? session)) continue;
            session.Enqueue(new LobbyPacket
            {
                Type = LobbyPacketType.RoomChat,
                RoomId = room.Id,
                PlayerId = playerId,
                Username = sender.Username,
                Message = message,
                RequestId = member.PlayerId == playerId ? request.RequestId ?? string.Empty : string.Empty
            });
        }
    }

    private void BroadcastAdventure(AdventurePacket adventure, string[] playerIds)
    {
        LobbyPacket packet = new()
        {
            Type = LobbyPacketType.Adventure,
            PlayerId = adventure.PlayerId,
            Adventure = adventure
        };
        foreach (string playerId in playerIds)
        {
            if (_sessions.TryGetValue(playerId, out LobbySendSession? session)) session.Enqueue(packet);
        }
    }

    private static void EnqueueError(LobbySendSession session, string requestId, string error)
    {
        session.Enqueue(new LobbyPacket
        {
            Type = LobbyPacketType.Error, RequestId = requestId, Error = error
        });
    }
}
