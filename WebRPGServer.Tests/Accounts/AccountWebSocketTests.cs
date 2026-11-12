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

    [TestCase(true)]
    [TestCase(false)]
    public async Task LobbyReauthenticationPreservesRoomAndRequiresSameAccount(bool sameAccount)
    {
        int rotated = 0;
        await using LocalLobbyServer server = await LocalLobbyServer.StartAsync(token =>
            Task.FromResult<AccountIdentity?>(token switch
            {
                "peer-token" => new("peer-account", "Peer"),
                "original-token" when Volatile.Read(ref rotated) == 0 => new("original-account", "Player"),
                "renewed-token" when Volatile.Read(ref rotated) != 0 =>
                    new(sameAccount ? "original-account" : "different-account", "Player"),
                _ => null
            }));
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(TestTimeoutSeconds));
        CancellationToken cancellation = timeout.Token;
        using LobbyClient client = await LobbyClient.ConnectAsync(server.Address, "Player", cancellation,
            "original-token");
        JsonObject created = await client.RequestAsync("CreateRoom", new() { ["RoomName"] = "Refresh" }, cancellation);
        string roomId = created["Room"]!["Id"]!.GetValue<string>();
        using LobbyClient peer = await LobbyClient.ConnectAsync(server.Address, "Peer", cancellation, "peer-token");
        await peer.RequestAsync("JoinRoom", new() { ["RoomId"] = roomId }, cancellation);
        await client.RequestAsync("SetReady", new() { ["IsReady"] = true }, cancellation);
        await peer.RequestAsync("SetReady", new() { ["IsReady"] = true }, cancellation);
        await client.SendRawAsync("""{"Type":"Adventure","Adventure":{"Type":"AdventureStart"}}""", cancellation);
        await client.ReadTypeAsync("Adventure", cancellation);
        await peer.ReadTypeAsync("Adventure", cancellation);
        await client.RequestAsync("ListRooms", new(), cancellation);
        Volatile.Write(ref rotated, 1);

        JsonObject reauthentication = new() { ["AccessToken"] = "renewed-token" };
        if (!sameAccount)
        {
            InvalidOperationException? error = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await client.RequestAsync("Hello", reauthentication, cancellation, "Welcome"));
            Assert.That(error!.Message, Does.Contain("PolicyViolation"));
            return;
        }

        JsonObject welcome = await client.RequestAsync("Hello", reauthentication, cancellation, "Welcome");
        Assert.That(welcome["PlayerId"]!.GetValue<string>(), Is.EqualTo(client.PlayerId));
        Assert.That(welcome["Room"]!["Id"]!.GetValue<string>(), Is.EqualTo(roomId));
        await client.SendRawAsync(
            """{"Type":"Adventure","Adventure":{"Type":"AdventureCommand","Command":"vote","Payload":"1|0"}}""",
            cancellation);
        JsonObject vote = await peer.ReadTypeAsync("Adventure", cancellation);
        Assert.That(vote["Adventure"]!["Command"]!.GetValue<string>(), Is.EqualTo("vote"));
        Assert.That(vote["Adventure"]!["Sequence"]!.GetValue<long>(), Is.EqualTo(1));
    }

    [Test]
    public async Task MissingTokenIsRejected()
    {
        await using LocalLobbyServer server = await LocalLobbyServer.StartAsync(
            _ => Task.FromResult<AccountIdentity?>(null));
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(TestTimeoutSeconds));
        using ClientWebSocket socket = new();
        await socket.ConnectAsync(server.Address, timeout.Token);
        await socket.SendAsync(
            Encoding.UTF8.GetBytes("{\"Type\":\"Hello\",\"Username\":\"Spoofed\"}"), WebSocketMessageType.Text, true,
            timeout.Token);
        WebSocketReceiveResult response = await socket.ReceiveAsync(new byte[BufferBytes], timeout.Token);
        Assert.That(response.CloseStatus, Is.EqualTo(WebSocketCloseStatus.PolicyViolation));
    }

    [Test]
    public async Task AccountIdentityOverridesClientNameAndRevokedTokenCannotAct()
    {
        bool revoked = false;
        await using LocalLobbyServer server = await LocalLobbyServer.StartAsync(token =>
            Task.FromResult<AccountIdentity?>(!revoked && token == "test-token" ? new("fixed-account-id", "註冊名稱")
                : null));
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(TestTimeoutSeconds));
        using ClientWebSocket socket = new();
        await socket.ConnectAsync(server.Address, timeout.Token);
        await socket.SendAsync(
            Encoding.UTF8.GetBytes("{\"Type\":\"Hello\",\"Username\":\"Spoofed\",\"AccessToken\":\"test-token\"}"),
            WebSocketMessageType.Text, true, timeout.Token);
        byte[] buffer = new byte[BufferBytes];
        WebSocketReceiveResult welcome = await socket.ReceiveAsync(buffer, timeout.Token);
        Assert.That(JsonNode.Parse(buffer.AsSpan(0, welcome.Count))!["PlayerId"]!.GetValue<string>(),
            Is.EqualTo("fixed-account-id"));
        await socket.SendAsync(
            Encoding.UTF8.GetBytes("{\"Type\":\"CreateRoom\",\"RoomName\":\"Test\",\"IsPublic\":true}"),
            WebSocketMessageType.Text, true, timeout.Token);
        WebSocketReceiveResult state = await socket.ReceiveAsync(buffer, timeout.Token);
        Assert.That(JsonNode.Parse(buffer.AsSpan(0, state.Count))!["Room"]!["Members"]![0]!["Username"]!
            .GetValue<string>(), Is.EqualTo("註冊名稱"));
        revoked = true;
        await socket.SendAsync(
            Encoding.UTF8.GetBytes("{\"Type\":\"ListRooms\"}"), WebSocketMessageType.Text, true, timeout.Token);
        WebSocketReceiveResult rejected = await socket.ReceiveAsync(buffer, timeout.Token);
        Assert.That(rejected.CloseStatus, Is.EqualTo(WebSocketCloseStatus.PolicyViolation));
    }
}
