using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;
using WebRPGShared;

namespace WebRPGServer.Tests;

// 提供可暫停傳送的記憶體 WebSocket，驗證慢連線與封包順序。
internal sealed class ControlledLobbySocket : WebSocket
{
    private readonly Channel<byte[]> _incoming = Channel.CreateUnbounded<byte[]>();
    private readonly Channel<LobbyPacket> _outgoing = Channel.CreateUnbounded<LobbyPacket>();
    private readonly CancellationTokenSource _aborted = new();
    private WebSocketState _state = WebSocketState.Open;
    private TaskCompletionSource? _sendRelease;
    private TaskCompletionSource _sendStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _sending;
    public bool ConcurrentSendDetected { get; private set; }
    public override WebSocketState State => _state;
    public override WebSocketCloseStatus? CloseStatus => null;
    public override string? CloseStatusDescription => null;
    public override string? SubProtocol => null;

    public void PauseSending() => _sendRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task SendStarted => _sendStarted.Task;
    public void ResumeSending() => _sendRelease?.TrySetResult();

    public void Submit(LobbyPacket packet) => _incoming.Writer.TryWrite(JsonSerializer.SerializeToUtf8Bytes(packet));

    public async Task<LobbyPacket> ReadAsync(CancellationToken token) => await _outgoing.Reader.ReadAsync(token);

    public async Task<LobbyPacket> RequestAsync(LobbyPacket packet, CancellationToken token)
    {
        packet.RequestId = Guid.NewGuid().ToString("N");
        Submit(packet);
        while (true)
        {
            LobbyPacket response = await ReadAsync(token);
            if (response.RequestId == packet.RequestId) return response;
        }
    }

    public override async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType,
        bool endOfMessage, CancellationToken cancellationToken)
    {
        if (Interlocked.Increment(ref _sending) != 1) ConcurrentSendDetected = true;
        try
        {
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _aborted.Token);
            if (_sendRelease != null)
            {
                _sendStarted.TrySetResult();
                await _sendRelease.Task.WaitAsync(linked.Token);
            }
            await _outgoing.Writer.WriteAsync(JsonSerializer.Deserialize<LobbyPacket>(buffer.AsSpan())!, linked.Token);
        }
        finally { Interlocked.Decrement(ref _sending); }
    }

    public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
    {
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _aborted.Token);
        try
        {
            byte[] message = await _incoming.Reader.ReadAsync(linked.Token);
            message.AsSpan().CopyTo(buffer.AsSpan());
            return new WebSocketReceiveResult(message.Length, WebSocketMessageType.Text, true);
        }
        catch (OperationCanceledException) when (_aborted.IsCancellationRequested)
        {
            throw new WebSocketException("Test socket aborted.");
        }
    }

    public override void Abort()
    {
        _state = WebSocketState.Aborted;
        _aborted.Cancel();
    }

    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
    {
        _state = WebSocketState.Closed;
        return Task.CompletedTask;
    }

    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) =>
        CloseAsync(closeStatus, statusDescription, cancellationToken);

    public override void Dispose()
    {
        Abort();
        _aborted.Dispose();
    }
}


