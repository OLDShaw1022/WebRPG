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
            builder.Services.AddSingleton<PostgresAccountStore>();
            builder.Services.AddSingleton<PostgresShopStore>();
            builder.Services.AddSingleton<PostgresTopUpStore>();
            builder.Services.AddScoped<PayPalTopUpService>();
        }

        builder.Services.AddSingleton(new ShopCatalog(builder.Configuration));
        builder.Services.AddSingleton(new ShopTopUpCatalog(builder.Configuration));
        PayPalSettings payPal = new(builder.Configuration);
        if (payPal.Enabled && !payPal.Configured)
            throw new InvalidOperationException(
                "Enabled PayPal checkout requires server credentials and HTTPS callback URLs.");
        builder.Services.AddSingleton(payPal);
        builder.Services.AddHttpClient<PayPalCheckout>(client => client.BaseAddress = new Uri(payPal.Sandbox
            ? "https://api-m.sandbox.paypal.com/" : "https://api-m.paypal.com/"));
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
            await application.Services.GetRequiredService<PostgresAccountStore>()
                .InitializeAsync(CancellationToken.None);
            await application.Services.GetRequiredService<PostgresShopStore>().InitializeAsync(CancellationToken.None);
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
        ShopTopUpEndpoints.MapPayPal(application);

        application.Use(async (context, next) =>
        {
            if (context.Request.Path == "/ws" && context.WebSockets.IsWebSocketRequest &&
                context.Request.Query["channel"] == "lobby")
            {
                using WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                LobbyHub lobbyHub = context.RequestServices.GetRequiredService<LobbyHub>();
                PostgresAccountStore? accounts = context.RequestServices.GetService<PostgresAccountStore>();
                await lobbyHub.HandleConnectionAsync(webSocket, context.RequestAborted,
                    async accessToken => accounts is null ? null :
                        await accounts.AuthenticateAsync(accessToken, context.RequestAborted));
                return;
            }

            await next();
        });

        await application.RunAsync();
    }
}
