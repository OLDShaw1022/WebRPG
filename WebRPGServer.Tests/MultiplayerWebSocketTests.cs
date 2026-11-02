using System.Text.Json.Nodes;

namespace WebRPGServer.Tests;

// 以獨立 WebSocket 連線驗證多人開局、投票廣播、序列及斷線失敗。
internal static class MultiplayerWebSocketTests
{
    public static async Task RunAsync(Uri address, CancellationToken token)
    {
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
            throw new InvalidOperationException("All clients must receive the same seed.");
        await outsider.RequestAsync("JoinRoom", new() { ["RoomId"] = roomId }, token, "Error");
        await host.SendRawAsync("""{"Type":"Adventure","Adventure":{"Type":"AdventureCommand","Command":"vote","Payload":"1|0"}}""", token);
        JsonObject voteHost = await host.ReadTypeAsync("Adventure", token);
        JsonObject voteMember = await second.ReadTypeAsync("Adventure", token);
        if (voteHost["Adventure"]!["Command"]!.GetValue<string>() != "vote" ||
            voteMember["Adventure"]!["Sequence"]!.GetValue<long>() != 1)
            throw new InvalidOperationException("Votes must be broadcast in order.");
        await second.SendRawAsync("""{"Type":"Adventure","Adventure":{"Type":"AdventureCommand","Command":"vote","Payload":"0|0"}}""", token);
        JsonObject nodeHost = await host.ReadTypeAsync("Adventure", token);
        JsonObject nodeMember = await second.ReadTypeAsync("Adventure", token);
        if (nodeHost["Adventure"]!["Command"]!.GetValue<string>() != "node" ||
            nodeMember["Adventure"]!["Payload"]!.GetValue<string>() != "1|0")
            throw new InvalidOperationException("Tied votes must use the leader's vote.");
        await host.SendRawAsync("""{"Type":"Adventure","Adventure":null}""", token);
        await host.ReadTypeAsync("Error", token);
        second.Abort();
        JsonObject failure = await host.ReadTypeAsync("Adventure", token);
        if (failure["Adventure"]!["Type"]!.GetValue<string>() != "AdventureFailed")
            throw new InvalidOperationException("A disconnected member must fail the remaining party.");
        await host.RequestAsync("LeaveRoom", new(), token);
        Console.WriteLine("PASS: real WebSocket adventure start/seed, no mid-run join, votes/tie/order, null input, disconnect failure.");
    }
}
