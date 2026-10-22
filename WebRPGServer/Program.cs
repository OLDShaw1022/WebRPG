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
        builder.Services.AddSingleton<ChatHub>();

        WebApplication app = builder.Build();
        await TestDatabaseConnectionAsync(builder.Configuration, app.Logger);

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

    private static async Task TestDatabaseConnectionAsync(
        IConfiguration configuration,
        ILogger logger)
    {
        string? connectionString = configuration.GetConnectionString("GameDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogWarning("Database connection test skipped: GameDatabase is not configured.");
            return;
        }

        using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(10));

        try
        {
            await using NpgsqlDataSource dataSource = NpgsqlDataSource.Create(connectionString);
            await using NpgsqlCommand command = dataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(cancellationTokenSource.Token);
            logger.LogInformation("Database connection test succeeded.");
        }
        catch (OperationCanceledException)
        {
            logger.LogError("Database connection test timed out.");
        }
        catch (NpgsqlException exception)
        {
            logger.LogError("Database connection test failed ({ErrorType}).", exception.GetType().Name);
        }
        catch (ArgumentException)
        {
            logger.LogError("Database connection test failed: invalid connection string.");
        }
    }
}