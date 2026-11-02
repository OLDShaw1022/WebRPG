using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using WebRPGShared;

namespace WebRPGServer;

// 處理有大小上限的大廳 WebSocket 訊息與限時傳送。
internal static class LobbySocketHelper
{
    public static async Task<string?> ReceiveAsync(WebSocket socket, CancellationToken token)
    {
        byte[] buffer = new byte[4096];
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

    public static async Task SendAsync(WebSocket socket, LobbyPacket packet, int timeoutSeconds)
    {
        if (socket.State != WebSocketState.Open) return;
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(packet);
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, timeout.Token);
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            socket.Abort();
        }
    }
}
