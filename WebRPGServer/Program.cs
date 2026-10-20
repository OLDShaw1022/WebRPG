using System.Net.WebSockets;
using System.Text;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
string port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

WebApplication app = builder.Build();

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    using WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
    Console.WriteLine("有 client 連線了");

    await SendText(webSocket, "歡迎連線到 WebSocket server！");

    var buffer = new byte[4096];
    try
    {
        while (webSocket.State == WebSocketState.Open)
        {
            using var memoryStream = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await webSocket.ReceiveAsync(buffer, context.RequestAborted);
                memoryStream.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
                break;
            }

            byte[] messageBytes = memoryStream.ToArray();
            if (result.MessageType == WebSocketMessageType.Text)
            {
                string message = Encoding.UTF8.GetString(messageBytes);
                var log = $"測試文字：{message}";
                Console.WriteLine(log);
                await SendText(webSocket, $"Server 收到: {log}");
            }
            else if (result.MessageType == WebSocketMessageType.Binary)
            {
                string message = Convert.ToHexString(messageBytes);
                var log = $"位元組：{string.Join(" ", message.Chunk(2).Select(bytes => new string(bytes)))}";
                Console.WriteLine(log);
                await SendText(webSocket, $"Server 收到: {log}");
            }
        }
    }
    catch (WebSocketException exception)
    {
        Console.WriteLine($"連線異常: {exception.Message}");
    }
    catch (OperationCanceledException)
    {
    }

    Console.WriteLine("client 已斷線");
});

app.Run();
return;

static Task SendText(WebSocket ws, string text)
{
    byte[] bytes = Encoding.UTF8.GetBytes(text);
    return ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
}