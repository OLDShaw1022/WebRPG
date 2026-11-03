using NUnit.Framework;
using System.Text.Json.Nodes;
using static WebRPGServer.Tests.MultiplayerProtocolTestHelper;

namespace WebRPGServer.Tests;

// 僅在明確指定時驗證遠端大廳連線。
[TestFixture]
public sealed class RemoteLobbyTests
{
    [Test]
    [Explicit("Requires an explicitly selected remote endpoint.")]
    [Category("Remote")]
    public async Task RemoteLobbySupportsCreateJoinReadyAndCleanup()
    {
        string? endpoint = TestContext.Parameters["ServerUrl"] ?? Environment.GetEnvironmentVariable("WEBRPG_TEST_SERVER_URL");
        Assert.That(endpoint, Is.Not.Null.And.Not.Empty, "Provide ServerUrl through TestRunParameters or WEBRPG_TEST_SERVER_URL.");
        Uri address = new(endpoint!);
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(45));
        CancellationToken token = timeout.Token;
        using LobbyClient host = await LobbyClient.ConnectAsync(address, "部署驗證隊長", token);
        using LobbyClient second = await LobbyClient.ConnectAsync(address, "部署驗證隊友", token);
        JsonObject created = await host.RequestAsync("CreateRoom", new()
        {
            ["RoomName"] = "連線驗證-" + Guid.NewGuid().ToString("N")[..8], ["IsPublic"] = true
        }, token);
        string roomId = created["Room"]!["Id"]!.GetValue<string>();
        JsonObject list = await second.RequestAsync("ListRooms", new(), token);
        Check(list["Rooms"]!.AsArray().Any(room => room!["Id"]!.GetValue<string>() == roomId), "Remote public room is missing.");
        await second.RequestAsync("JoinRoom", new() { ["RoomId"] = roomId }, token);
        await second.RequestAsync("SetReady", new() { ["IsReady"] = true }, token);
        JsonObject state = await host.RequestAsync("ListRooms", new(), token);
        Check(state["Room"]!["Members"]!.AsArray().Count == 2, "Remote room must have two members.");
        Check(state["Room"]!["Members"]!.AsArray().Single(member =>
            member!["PlayerId"]!.GetValue<string>() == second.PlayerId)!["IsReady"]!.GetValue<bool>(), "Remote ready state is missing.");
        await second.RequestAsync("LeaveRoom", new(), token);
        await host.RequestAsync("LeaveRoom", new(), token);
        JsonObject cleaned = await second.RequestAsync("ListRooms", new(), token);
        Check(!cleaned["Rooms"]!.AsArray().Any(room => room!["Id"]!.GetValue<string>() == roomId), "Remote smoke-test room was not removed.");

    }

}
