using System.Net.WebSockets;
using Npgsql;
using Microsoft.AspNetCore.HttpOverrides;

namespace WebRPGServer;

// 啟動聊天與房間大廳伺服器，設定資料庫與 /ws WebSocket 端點。
public static class Program
{
    private const int WebSocketKeepAliveIntervalSeconds = 30;
    public static async Task Main(string[] arguments)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(arguments);
        string port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

        string? connectionString = builder.Configuration.GetConnectionString("GameDatabase");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
            builder.Services.AddSingleton<IChatMessageStore, PostgresChatMessageStore>();
            builder.Services.AddSingleton<PostgresAccountStore>();
        }

        builder.Services.AddSingleton<ChatHub>();
        AccountEndpoints.AddServices(builder.Services);
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
        AccountCorsHelper.AddServices(builder.Services, builder.Configuration);
        LobbyOptions lobbyOptions = builder.Configuration.GetSection("Lobby").Get<LobbyOptions>() ?? new LobbyOptions();
        lobbyOptions.Validate();
        builder.Services.AddSingleton(lobbyOptions);
        builder.Services.AddSingleton<LobbyHub>();

        WebApplication application = builder.Build();

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            IChatMessageStore messageStore = application.Services.GetRequiredService<IChatMessageStore>();
            await messageStore.InitializeAsync(CancellationToken.None);
            await application.Services.GetRequiredService<PostgresAccountStore>().InitializeAsync(CancellationToken.None);
            application.Logger.LogInformation("Chat message database is ready.");
        }
        else
        {
            application.Logger.LogWarning(
                "Chat message persistence is disabled: GameDatabase is not configured.");
        }

        application.UseForwardedHeaders();
        application.UseWebSockets(new WebSocketOptions
        {
            KeepAliveInterval = TimeSpan.FromSeconds(WebSocketKeepAliveIntervalSeconds)
        });
        application.UseRouting();
        application.UseCors();
        application.UseRateLimiter();
        AccountEndpoints.Map(application);

        application.Use(async (context, next) =>
        {
            if (context.Request.Path == "/ws" && context.WebSockets.IsWebSocketRequest)
            {
                using WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                if (context.Request.Query["channel"] == "lobby")
                {
                    LobbyHub lobbyHub = context.RequestServices.GetRequiredService<LobbyHub>();
                    PostgresAccountStore? accounts = context.RequestServices.GetService<PostgresAccountStore>();
                    await lobbyHub.HandleConnectionAsync(webSocket, context.RequestAborted,
                        async accessToken => accounts is null ? null :
                            await accounts.AuthenticateAsync(accessToken, context.RequestAborted));
                }
                else
                {
                    ChatHub chatHub = context.RequestServices.GetRequiredService<ChatHub>();
                    await chatHub.HandleConnectionAsync(webSocket, context.RequestAborted);
                }
                return;
            }

            await next();
        });

        await application.RunAsync();
    }
}
