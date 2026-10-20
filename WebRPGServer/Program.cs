using System.Net.WebSockets;

namespace WebRPGServer;

public static class Program
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        string port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
        builder.Services.AddSingleton<ChatHub>();

        WebApplication app = builder.Build();

        app.UseWebSockets(new WebSocketOptions
        {
            KeepAliveInterval = TimeSpan.FromSeconds(30)
        });

        app.Use(async (context, next) =>
        {
            if (context.Request.Path == "/ws" && context.WebSockets.IsWebSocketRequest)
            {
                using WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                ChatHub chatHub = context.RequestServices.GetRequiredService<ChatHub>();
                await chatHub.HandleConnectionAsync(webSocket, context.RequestAborted);
                return;
            }

            await next();
        });

        app.Run();
    }

}
