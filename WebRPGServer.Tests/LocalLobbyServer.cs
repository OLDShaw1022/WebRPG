using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WebRPGServer;

namespace WebRPGServer.Tests;

// 在本機 Kestrel 執行隔離的大廳測試主機，認證依測試情境注入。
internal sealed class LocalLobbyServer : IAsyncDisposable
{
    private readonly WebApplication _application;
    public Uri Address { get; }

    private LocalLobbyServer(WebApplication application, Uri address)
    {
        _application = application;
        Address = address;
    }

    public static async Task<LocalLobbyServer> StartAsync(Func<string, Task<AccountIdentity?>>? authenticate = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        WebApplication application = builder.Build();
        application.UseWebSockets();
        LobbyHub hub = new(new LobbyOptions());
        application.Run(async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }
            using System.Net.WebSockets.WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();
            await hub.HandleConnectionAsync(socket, context.RequestAborted, authenticate);
        });
        await application.StartAsync();
        IServer server = application.Services.GetRequiredService<IServer>();
        string address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        UriBuilder socketAddress = new(address) { Scheme = "ws", Path = "/ws", Query = "channel=lobby" };
        return new LocalLobbyServer(application, socketAddress.Uri);
    }

    public async ValueTask DisposeAsync()
    {
        await _application.StopAsync();
        await _application.DisposeAsync();
    }
}
