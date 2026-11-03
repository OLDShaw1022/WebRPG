using NUnit.Framework;
using System.Text.Json.Nodes;

namespace WebRPGServer.Tests;

// 以獨立 WebSocket 連線驗證多人開局、投票廣播、序列及斷線失敗。
[TestFixture]
[Category("Integration")]
public sealed class MultiplayerWebSocketTests
{
    private LocalLobbyServer _server = null!;

    [SetUp]
    public async Task SetUpAsync() => _server = await LocalLobbyServer.StartAsync();

    [TearDown]
    public async Task TearDownAsync()
    {
        if (_server is not null) await _server.DisposeAsync();
    }

    [Test]
    public async Task AdventureBroadcastsVotesAndFailsOnDisconnect()
    {
        Uri address = _server.Address;
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
        CancellationToken token = timeout.Token;
        using LobbyClient host = await LobbyClient.ConnectAsync(address, "Adventure host", token);
        using LobbyClient second = await LobbyClient.ConnectAsync(address, "Adventure member", token);
        using LobbyClient outsider = await LobbyClient.ConnectAsync(address, "Outsider", token);
        JsonObject created = await host.RequestAsync("CreateRoom",
            new() { ["RoomName"] = "Adventure integration", ["IsPublic"] = true }, token);
        string roomId = created["Room"]!["Id"]!.GetValue<string>();
        await second.RequestAsync("JoinRoom", new() { ["RoomId"] = roomId }, token);
        await host.RequestAsync("SetReady", new() { ["IsReady"] = true }, token);
        await second.RequestAsync("SetReady", new() { ["IsReady"] = true }, token);
        await host.SendRawAsync("""{"Type":"Adventure","Adventure":{"Type":"AdventureStart"}}""", token);
        JsonObject hostStart = await host.ReadTypeAsync("Adventure", token);
        JsonObject memberStart = await second.ReadTypeAsync("Adventure", token);
        if (hostStart["Adventure"]!["Payload"]!.GetValue<string>() != memberStart["Adventure"]!["Payload"]!.GetValue<string>())
            Assert.Fail("All clients must receive the same seed.");
        await outsider.RequestAsync("JoinRoom", new() { ["RoomId"] = roomId }, token, "Error");
        await host.SendRawAsync("""{"Type":"Adventure","Adventure":{"Type":"AdventureCommand","Command":"vote","Payload":"1|0"}}""", token);
        JsonObject voteHost = await host.ReadTypeAsync("Adventure", token);
        JsonObject voteMember = await second.ReadTypeAsync("Adventure", token);
        if (voteHost["Adventure"]!["Command"]!.GetValue<string>() != "vote" ||
            voteMember["Adventure"]!["Sequence"]!.GetValue<long>() != 1)
            Assert.Fail("Votes must be broadcast in order.");
        await second.SendRawAsync("""{"Type":"Adventure","Adventure":{"Type":"AdventureCommand","Command":"vote","Payload":"0|0"}}""", token);
        JsonObject nodeHost = await host.ReadTypeAsync("Adventure", token);
        JsonObject nodeMember = await second.ReadTypeAsync("Adventure", token);
        if (nodeHost["Adventure"]!["Command"]!.GetValue<string>() != "node" ||
            nodeMember["Adventure"]!["Payload"]!.GetValue<string>() != "1|0")
            Assert.Fail("Tied votes must use the leader's vote.");
        await host.SendRawAsync("""{"Type":"Adventure","Adventure":null}""", token);
        await host.ReadTypeAsync("Error", token);
        second.Abort();
        JsonObject failure = await host.ReadTypeAsync("Adventure", token);
        if (failure["Adventure"]!["Type"]!.GetValue<string>() != "AdventureFailed")
            Assert.Fail("A disconnected member must fail the remaining party.");
        await host.RequestAsync("LeaveRoom", new(), token);

    }
}
