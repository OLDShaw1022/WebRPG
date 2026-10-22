using System.Net.WebSockets;
using Npgsql;

namespace WebRPGServer;

public static class Program
{
    public static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        string port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

        string? connectionString = builder.Configuration.GetConnectionString("GameDatabase");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
            builder.Services.AddSingleton<IChatMessageStore, PostgresChatMessageStore>();
        }

        builder.Services.AddSingleton<ChatHub>();

        WebApplication app = builder.Build();

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            IChatMessageStore messageStore = app.Services.GetRequiredService<IChatMessageStore>();
            await messageStore.InitializeAsync(CancellationToken.None);
            app.Logger.LogInformation("Chat message database is ready.");
        }
        else
        {
            app.Logger.LogWarning(
                "Chat message persistence is disabled: GameDatabase is not configured.");
        }

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

        await app.RunAsync();
    }
}