using System.Net.WebSockets;
using System.Text;
using WebRPGShared;

namespace WebRPGServer;

// 處理有大小上限的大廳 WebSocket 訊息與限時傳送。
internal static class LobbySocketHelper
{
    private const int ReceiveBufferSizeBytes = 4096;
    public static async Task<string?> ReceiveAsync(WebSocket socket, CancellationToken token)
    {
        byte[] buffer = new byte[ReceiveBufferSizeBytes];
        using MemoryStream message = new();
        bool valid = true;
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, token);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            valid &= result.MessageType == WebSocketMessageType.Text &&
                message.Length + result.Count <= LobbyPacketLimits.MessageBytes;
            if (valid) message.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        return valid ? Encoding.UTF8.GetString(message.ToArray()) : string.Empty;
    }

    public static async Task SendAsync(WebSocket socket, byte[] bytes, int timeoutSeconds, CancellationToken token)
    {
        if (socket.State != WebSocketState.Open) return;
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, timeout.Token);
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            socket.Abort();
        }
    }
}
