using System.Net.WebSockets;
using System.Text.Json;
using WebRPGShared;

namespace WebRPGServer;

// 序列化房間操作並向所有已連線玩家推播伺服器狀態。
public sealed class LobbyHub
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, WebSocket> _sessions = new();
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
                        await SendErrorAsync(socket, string.Empty, "封包格式錯誤或超過大小上限。");
                        continue;
                    }
                    if (request.RequestId?.Length > 64)
                    {
                        await SendErrorAsync(socket, string.Empty, "要求識別碼過長。");
                        continue;
                    }
                    if (!registered)
                    {
                        if (request.Type != LobbyPacketType.Hello) throw new ArgumentException("請先連線。");
                        if (_sessions.Count >= _options.MaximumConnections) throw new ArgumentException("大廳連線已滿。");
                        _registry.Connect(playerId, request.Username);
                        _sessions.Add(playerId, socket);
                        registered = true;
                        LobbyPacket welcome = _registry.Snapshot(playerId, request.RequestId ?? string.Empty);
                        welcome.Type = LobbyPacketType.Welcome;
                        await LobbySocketHelper.SendAsync(socket, welcome, _options.SendTimeoutSeconds);
                        continue;
                    }
                    bool changed = _registry.Execute(playerId, request);
                    await LobbySocketHelper.SendAsync(socket, _registry.Snapshot(playerId, request.RequestId ?? string.Empty),
                        _options.SendTimeoutSeconds);
                    if (changed) await BroadcastAsync(playerId);
                }
                catch (ArgumentException exception)
                {
                    await SendErrorAsync(socket, request?.RequestId ?? string.Empty, exception.Message);
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
                    _sessions.Remove(playerId);
                    _registry.Disconnect(playerId);
                    await BroadcastAsync(playerId);
                }
            }
            finally { _gate.Release(); }
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(_options.SendTimeoutSeconds));
                try { await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnected", timeout.Token); }
                catch (Exception exception) when (exception is WebSocketException or OperationCanceledException) { socket.Abort(); }
            }
        }
    }

    private async Task BroadcastAsync(string excludedPlayerId)
    {
        await Task.WhenAll(_sessions.Where(pair => pair.Key != excludedPlayerId).Select(pair =>
            LobbySocketHelper.SendAsync(pair.Value, _registry.Snapshot(pair.Key), _options.SendTimeoutSeconds)));
    }

    private async Task SendErrorAsync(WebSocket socket, string requestId, string error)
    {
        await LobbySocketHelper.SendAsync(socket, new LobbyPacket
        {
            Type = LobbyPacketType.Error, RequestId = requestId, Error = error
        }, _options.SendTimeoutSeconds);
    }
}
