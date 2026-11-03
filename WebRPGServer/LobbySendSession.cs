using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;
using WebRPGShared;

namespace WebRPGServer;

// 以有界佇列依序傳送單一連線的封包，隔離慢連線與房間狀態鎖。
internal sealed class LobbySendSession : IAsyncDisposable
{
    private const int MaximumPendingPackets = 128;
    private readonly WebSocket _socket;
    private readonly int _timeoutSeconds;
    private readonly Channel<byte[]> _packets = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(MaximumPendingPackets)
    {
        SingleReader = true,
        SingleWriter = true,
        AllowSynchronousContinuations = false,
        FullMode = BoundedChannelFullMode.Wait
    });
    private readonly CancellationTokenSource _shutdown;
    private readonly Task _sender;

    public LobbySendSession(WebSocket socket, int timeoutSeconds, CancellationToken token)
    {
        _socket = socket;
        _timeoutSeconds = timeoutSeconds;
        _shutdown = CancellationTokenSource.CreateLinkedTokenSource(token);
        _sender = SendPacketsAsync();
    }

    public void Enqueue(LobbyPacket packet)
    {
        if (_socket.State != WebSocketState.Open || _shutdown.IsCancellationRequested) return;
        // 在呼叫端持有房間鎖時凍結快照，避免排隊期間引用的房間資料被修改。
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(packet);
        if (!_packets.Writer.TryWrite(bytes)) _socket.Abort();
    }

    private async Task SendPacketsAsync()
    {
        try
        {
            await foreach (byte[] bytes in _packets.Reader.ReadAllAsync(_shutdown.Token))
            {
                if (_socket.State != WebSocketState.Open) break;
                await LobbySocketHelper.SendAsync(_socket, bytes, _timeoutSeconds, _shutdown.Token);
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
        finally { _packets.Writer.TryComplete(); }
    }

    public async ValueTask DisposeAsync()
    {
        _packets.Writer.TryComplete();
        _shutdown.Cancel();
        try { await _sender; }
        finally { _shutdown.Dispose(); }
    }
}

