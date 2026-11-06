using NUnit.Framework;
using System.Text.Json.Nodes;
using static WebRPGServer.Tests.MultiplayerProtocolTestHelper;

namespace WebRPGServer.Tests;

// 驗證本機大廳的完整 WebSocket 操作流程。
[TestFixture]
[Category("Integration")]
public sealed class LobbyWebSocketTests
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
    public async Task LobbySupportsMembershipAndRejectsInvalidInput()
    {
        Uri address = _server.Address;
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
        CancellationToken token = timeout.Token;
        using LobbyClient host = await LobbyClient.ConnectAsync(address, "隊長", token);
        using LobbyClient second = await LobbyClient.ConnectAsync(address, "隊友", token);
        using LobbyClient third = await LobbyClient.ConnectAsync(address, "Third", token);
        using LobbyClient fourth = await LobbyClient.ConnectAsync(address, "Fourth", token);
        using LobbyClient fifth = await LobbyClient.ConnectAsync(address, "Fifth", token);
        Check(host.PlayerId != second.PlayerId, "Server must assign unique player IDs.");
        JsonObject created = await host.RequestAsync("CreateRoom", new() { ["RoomName"] = "測試房間", ["IsPublic"] = true }, token);
        string roomId = created["Room"]!["Id"]!.GetValue<string>();
        Check(created["Room"]!["Members"]!.AsArray().Count == 1, "Creator must join the room.");
        JsonObject list = await second.RequestAsync("ListRooms", new(), token);
        Check(list["Rooms"]!.AsArray().Any(r => r!["Id"]!.GetValue<string>() == roomId), "Public room must be listed.");
        await second.RequestAsync("JoinRoom", new() { ["RoomId"] = roomId }, token);
        await second.RequestAsync("JoinRoom", new() { ["RoomId"] = roomId }, token, "Error");
        await third.RequestAsync("JoinRoom", new() { ["RoomId"] = roomId }, token);
        await fourth.RequestAsync("JoinRoom", new() { ["RoomId"] = roomId }, token);
        await fifth.RequestAsync("JoinRoom", new() { ["RoomId"] = roomId }, token, "Error");
        JsonObject ready = await second.RequestAsync("SetReady", new() { ["IsReady"] = true }, token);
        Check(ready["Room"]!["Members"]!.AsArray().Single(m => m!["PlayerId"]!.GetValue<string>() == second.PlayerId)!["IsReady"]!.GetValue<bool>(), "Ready must be stored per player.");
        JsonObject afterLeave = await host.RequestAsync("LeaveRoom", new(), token);
        Check(afterLeave["Room"] is null, "Leaving must clear the sender's room.");
        JsonObject newHost = await second.RequestAsync("ListRooms", new(), token);
        Check(newHost["Room"]!["LeaderId"]!.GetValue<string>() == second.PlayerId, "Oldest remaining member becomes leader.");
        await fifth.RequestAsync("JoinRoom", new() { ["RoomId"] = roomId }, token);
        fifth.Abort();
        JsonObject cleaned;
        do
        {
            cleaned = await second.RequestAsync("ListRooms", new(), token);
            if (cleaned["Room"]!["Members"]!.AsArray().Count == 3) break;
            await Task.Delay(30, token);
        } while (true);
        Check(cleaned["Room"]!["Members"]!.AsArray().All(m => m!["PlayerId"]!.GetValue<string>() != fifth.PlayerId), "Disconnected member must be removed.");
        await second.RequestAsync("LeaveRoom", new(), token);
        await third.RequestAsync("LeaveRoom", new(), token);
        await fourth.RequestAsync("LeaveRoom", new(), token);
        JsonObject empty = await host.RequestAsync("ListRooms", new(), token);
        Check(empty["Rooms"]!.AsArray().Count == 0, "Empty rooms must be deleted.");
        await host.RequestAsync("CreateRoom", new() { ["RoomName"] = "Private", ["IsPublic"] = false }, token);
        JsonObject privateList = await second.RequestAsync("ListRooms", new(), token);
        Check(privateList["Rooms"]!.AsArray().Count == 0, "Private rooms must not be listed.");
        await second.RequestAsync("SetReady", new() { ["IsReady"] = true }, token, "Error");
        await second.RequestAsync("JoinRoom", new() { ["RoomId"] = "missing" }, token, "Error");
        await host.RequestAsync("CreateRoom", new() { ["RoomName"] = "duplicate" }, token, "Error");
        await host.RequestAsync("Unsupported", new(), token, "Error");
        await host.RequestAsync("LeaveRoom", new(), token);
        await host.RequestAsync("CreateRoom", new() { ["RoomName"] = "   " }, token, "Error");
        await host.SendRawAsync("{", token);
        await host.ReadTypeAsync("Error", token);
        await host.SendRawAsync(new string('x', 9000), token);
        await host.ReadTypeAsync("Error", token);
        await host.RequestAsync("ListRooms", new(), token);

    }

    // Verifies server identity and room membership on the chat path.
    [Test]
    public async Task RoomChatRequiresMembershipAndUsesServerIdentity()
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        CancellationToken token = timeout.Token;
        using LobbyClient host = await LobbyClient.ConnectAsync(_server.Address, "Host", token);
        using LobbyClient member = await LobbyClient.ConnectAsync(_server.Address, "Member", token);
        JsonObject room = await host.RequestAsync("CreateRoom", new() { ["RoomName"] = "Chat" }, token);
        string roomId = room["Room"]!["Id"]!.GetValue<string>();
        await member.RequestAsync("RoomChat", new() { ["Message"] = "outside" }, token, "Error");
        await member.RequestAsync("JoinRoom", new() { ["RoomId"] = roomId }, token);
        await host.RequestAsync("RoomChat", new() { ["Message"] = "  " }, token, "Error");
        await host.RequestAsync("RoomChat", new() { ["Message"] = new string('x', 241) }, token, "Error");
        using LobbyClient outsider = await LobbyClient.ConnectAsync(_server.Address, "Outsider", token);
        await outsider.RequestAsync("CreateRoom", new() { ["RoomName"] = "Other" }, token);
        await host.RequestAsync("RoomChat",
            new() { ["Message"] = "hello", ["Username"] = "Spoofed" }, token, "RoomChat");
        JsonObject received = await member.ReadTypeAsync("RoomChat", token);
        Assert.That(received["Message"]!.GetValue<string>(), Is.EqualTo("hello"));
        Assert.That(received["Username"]!.GetValue<string>(), Is.EqualTo("Host"));
        Assert.That(received["RoomId"]!.GetValue<string>(), Is.EqualTo(roomId));
        await host.RequestAsync("RoomChat", new() { ["Message"] = "too fast" }, token, "Error");
        await outsider.RequestAsync("ListRooms", new(), token, observe: AssertNoChat);
        await outsider.RequestAsync("LeaveRoom", new(), token, observe: AssertNoChat);
        await outsider.RequestAsync("JoinRoom", new() { ["RoomId"] = roomId }, token, observe: AssertNoChat);
        await outsider.RequestAsync("ListRooms", new(), token, observe: AssertNoChat);
        await member.RequestAsync("LeaveRoom", new(), token);
        await member.RequestAsync("RoomChat", new() { ["Message"] = "left" }, token, "Error");

        static void AssertNoChat(JsonObject packet) =>
            Assert.That(packet["Type"]?.GetValue<string>(), Is.Not.EqualTo("RoomChat"));
    }
}
