using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;

namespace WebRPGServer.Tests;

// 提供測試專用的封包收送與非同步推播過濾。
internal sealed class LobbyClient : IDisposable
{
    private readonly ClientWebSocket _socket = new();
    private int _requestNumber;
    public string PlayerId { get; private set; } = string.Empty;

    public static async Task<LobbyClient> ConnectAsync(Uri address, string username, CancellationToken token, string? accessToken = null)
    {
        LobbyClient client = new();
        try
        {
            await client._socket.ConnectAsync(address, token);
            JsonObject hello = new() { ["Username"] = username };
            if (accessToken is not null) hello["AccessToken"] = accessToken;
            JsonObject welcome = await client.RequestAsync("Hello", hello, token, "Welcome");
            client.PlayerId = welcome["PlayerId"]!.GetValue<string>();
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public async Task<JsonObject> RequestAsync(string type, JsonObject packet, CancellationToken token,
        string expected = "State", Action<JsonObject>? observe = null)
    {
        string id = (++_requestNumber).ToString();
        packet["Type"] = type;
        packet["RequestId"] = id;
        await SendRawAsync(packet.ToJsonString(), token);
        while (true)
        {
            JsonObject response = await ReadAsync(token);
            observe?.Invoke(response);
            if (response["RequestId"]?.GetValue<string>() != id)
            {
                if (response["Type"]?.GetValue<string>() == "Error")
                    throw new InvalidOperationException(response.ToJsonString());
                continue;
            }
            if (response["Type"]!.GetValue<string>() != expected)
                throw new InvalidOperationException($"Expected {expected}, got {response}");
            return response;
        }
    }

    public async Task<JsonObject> ReadTypeAsync(string expected, CancellationToken token)
    {
        while (true)
        {
            JsonObject response = await ReadAsync(token);
            if (response["Type"]!.GetValue<string>() == expected) return response;
        }
    }

    public async Task SendRawAsync(string text, CancellationToken token) =>
        await _socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, token);

    private async Task<JsonObject> ReadAsync(CancellationToken token)
    {
        byte[] buffer = new byte[32768];
        using MemoryStream message = new();
        WebSocketReceiveResult result;
        do
        {
            result = await _socket.ReceiveAsync(buffer, token);
            if (result.MessageType == WebSocketMessageType.Close)
                throw new InvalidOperationException($"WebSocket closed: {result.CloseStatus}: {result.CloseStatusDescription}");
            message.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        return JsonNode.Parse(message.ToArray())!.AsObject();
    }

    public void Abort() => _socket.Abort();
    public void Dispose() => _socket.Dispose();
}
