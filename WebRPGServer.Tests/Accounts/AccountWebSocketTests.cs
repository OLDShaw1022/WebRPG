using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using NUnit.Framework;
using WebRPGServer;

namespace WebRPGServer.Tests;

// 驗證大廳使用固定帳號身分，拒絕未認證與已撤銷的登入。
public sealed class AccountWebSocketTests
{
    private const int BufferBytes = 8192;
    private const int TestTimeoutSeconds = 10;

    [Test]
    public async Task LobbyClientSendsTokenAndReportsAuthenticationRejection()
    {
        await using LocalLobbyServer server = await LocalLobbyServer.StartAsync(token =>
            Task.FromResult<AccountIdentity?>(token == "test-token" ? new("fixed-account-id", "Test") : null));
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(TestTimeoutSeconds));
        using LobbyClient client = await LobbyClient.ConnectAsync(server.Address, "Test", timeout.Token, "test-token");
        Assert.That(client.PlayerId, Is.EqualTo("fixed-account-id"));
        InvalidOperationException? error = Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            using LobbyClient rejected = await LobbyClient.ConnectAsync(server.Address, "Test", timeout.Token);
        });
        Assert.That(error!.Message, Does.Contain("PolicyViolation").And.Contain("Authentication required"));
    }

    [Test]
    public async Task MissingTokenIsRejected()
    {
        await using LocalLobbyServer server = await LocalLobbyServer.StartAsync(_ => Task.FromResult<AccountIdentity?>(null));
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(TestTimeoutSeconds));
        using ClientWebSocket socket = new();
        await socket.ConnectAsync(server.Address, timeout.Token);
        await socket.SendAsync(Encoding.UTF8.GetBytes("{\"Type\":\"Hello\",\"Username\":\"Spoofed\"}"), WebSocketMessageType.Text, true, timeout.Token);
        WebSocketReceiveResult response = await socket.ReceiveAsync(new byte[BufferBytes], timeout.Token);
        Assert.That(response.CloseStatus, Is.EqualTo(WebSocketCloseStatus.PolicyViolation));
    }

    [Test]
    public async Task AccountIdentityOverridesClientNameAndRevokedTokenCannotAct()
    {
        bool revoked = false;
        await using LocalLobbyServer server = await LocalLobbyServer.StartAsync(token =>
            Task.FromResult<AccountIdentity?>(!revoked && token == "test-token" ? new("fixed-account-id", "註冊名稱") : null));
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(TestTimeoutSeconds));
        using ClientWebSocket socket = new();
        await socket.ConnectAsync(server.Address, timeout.Token);
        await socket.SendAsync(Encoding.UTF8.GetBytes("{\"Type\":\"Hello\",\"Username\":\"Spoofed\",\"AccessToken\":\"test-token\"}"), WebSocketMessageType.Text, true, timeout.Token);
        byte[] buffer = new byte[BufferBytes];
        WebSocketReceiveResult welcome = await socket.ReceiveAsync(buffer, timeout.Token);
        Assert.That(JsonNode.Parse(buffer.AsSpan(0, welcome.Count))!["PlayerId"]!.GetValue<string>(), Is.EqualTo("fixed-account-id"));
        await socket.SendAsync(Encoding.UTF8.GetBytes("{\"Type\":\"CreateRoom\",\"RoomName\":\"Test\",\"IsPublic\":true}"), WebSocketMessageType.Text, true, timeout.Token);
        WebSocketReceiveResult state = await socket.ReceiveAsync(buffer, timeout.Token);
        Assert.That(JsonNode.Parse(buffer.AsSpan(0, state.Count))!["Room"]!["Members"]![0]!["Username"]!.GetValue<string>(), Is.EqualTo("註冊名稱"));
        revoked = true;
        await socket.SendAsync(Encoding.UTF8.GetBytes("{\"Type\":\"ListRooms\"}"), WebSocketMessageType.Text, true, timeout.Token);
        WebSocketReceiveResult rejected = await socket.ReceiveAsync(buffer, timeout.Token);
        Assert.That(rejected.CloseStatus, Is.EqualTo(WebSocketCloseStatus.PolicyViolation));
    }
}
