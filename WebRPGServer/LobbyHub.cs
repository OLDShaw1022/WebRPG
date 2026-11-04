using System.Net.WebSockets;
using System.Text.Json;
using WebRPGShared;

namespace WebRPGServer;

// 序列化房間操作並向所有已連線玩家推播伺服器狀態。
public sealed class LobbyHub
{
    private const int MaximumRequestIdLength = 64;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, LobbySendSession> _sessions = new();
    private readonly LobbyRegistry _registry;
    private readonly LobbyOptions _options;

    public LobbyHub(LobbyOptions options)
    {
        _options = options;
        _registry = new LobbyRegistry(options);
    }

    public async Task HandleConnectionAsync(WebSocket socket, CancellationToken token)
    {
        string playerId = Guid.NewGuid().ToString("N");
        bool registered = false;
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
                        _registry.Connect(playerId, request.Username);
                        _sessions.Add(playerId, session);
                        registered = true;
                        LobbyPacket welcome = _registry.Snapshot(playerId, request.RequestId ?? string.Empty);
                        welcome.Type = LobbyPacketType.Welcome;
                        session.Enqueue(welcome);
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
                catch (Exception exception) when (exception is WebSocketException or OperationCanceledException) { socket.Abort(); }
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
