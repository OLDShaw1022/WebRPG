using System.Net.WebSockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}"); // 0.0.0.0 = 允許區網其他裝置連線；只想本機用可改成 localhost

var app = builder.Build();

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

app.Map("/ws", async (HttpContext context) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    using var ws = await context.WebSockets.AcceptWebSocketAsync();
    Console.WriteLine("有 client 連線了");

    await SendText(ws, "歡迎連線到 WebSocket server！");

    var buffer = new byte[4096];
    try
    {
        while (ws.State == WebSocketState.Open)
        {
            // 累積多段資料，避免大訊息被截斷
            using var ms = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(buffer, context.RequestAborted);
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
                break;
            }

            var msg = Encoding.UTF8.GetString(ms.ToArray());
            Console.WriteLine($"收到訊息: {msg}");
            await SendText(ws, $"Server 收到: {msg}");
        }
    }
    catch (WebSocketException ex)
    {
        Console.WriteLine($"連線異常: {ex.Message}");
    }
    catch (OperationCanceledException) { }

    Console.WriteLine("client 已斷線");
});

app.Run();

static Task SendText(WebSocket ws, string text)
{
    var bytes = Encoding.UTF8.GetBytes(text);
    return ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
}