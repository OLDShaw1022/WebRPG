using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;

namespace WebRPGServer.Tests;

// 以真實 WebSocket 驗證大廳協定、房間限制與斷線清理。
internal static class Program
{
    private static async Task Main(string[] arguments)
    {
        if (arguments.Length == 2 && arguments[0] == "--url")
        {
            using CancellationTokenSource remoteTimeout = new(TimeSpan.FromSeconds(45));
            await RunRemoteAsync(new Uri(arguments[1]), remoteTimeout.Token);
            return;
        }
        await LobbyConcurrencyTests.RunAsync();
        await LobbySendFailureTests.RunAsync();
        if (arguments.Length == 1 && arguments[0] == "--concurrency-only") return;
        MultiplayerProtocolTests.Run();
        TcpListener reservation = new(IPAddress.Loopback, 0);
        reservation.Start();
        int port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();
        ProcessStartInfo start = new("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(Path.GetFullPath("WebRPGServer/bin/Debug/net9.0/WebRPGServer.dll"));
        start.Environment["PORT"] = port.ToString();
        start.Environment["ConnectionStrings__GameDatabase"] = string.Empty;
        using Process server = Process.Start(start)!;
        Task<string> output = server.StandardOutput.ReadToEndAsync();
        Task<string> errors = server.StandardError.ReadToEndAsync();
        try
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
            Uri address = new($"ws://127.0.0.1:{port}/ws?channel=lobby");
            await WaitForServerAsync(port, timeout.Token);
            await RunAsync(address, timeout.Token);
            await MultiplayerWebSocketTests.RunAsync(address, timeout.Token);
            Console.WriteLine("PASS: lobby WebSocket integration scenarios.");
        }
        finally
        {
            if (!server.HasExited) server.Kill(true);
            await server.WaitForExitAsync();
            await output;
            await errors;
        }
    }

    private static async Task RunRemoteAsync(Uri address, CancellationToken token)
    {
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
        Console.WriteLine("PASS: remote WSS create/list/join/ready/leave/cleanup at " + address.Host);
    }

    private static async Task RunAsync(Uri address, CancellationToken token)
    {
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
        Console.WriteLine("PASS: capacity, duplicate join, public/private lists, ready, leader, leave, disconnect, invalid input.");
    }

    private static async Task WaitForServerAsync(int port, CancellationToken token)
    {
        while (true)
        {
            try
            {
                using TcpClient probe = new();
                await probe.ConnectAsync(IPAddress.Loopback, port, token);
                return;
            }
            catch (SocketException) { await Task.Delay(50, token); }
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

// 提供測試專用的封包收送與非同步推播過濾。
internal sealed class LobbyClient : IDisposable
{
    private readonly ClientWebSocket _socket = new();
    private int _requestNumber;
    public string PlayerId { get; private set; } = string.Empty;

    public static async Task<LobbyClient> ConnectAsync(Uri address, string username, CancellationToken token)
    {
        LobbyClient client = new();
        try
        {
            await client._socket.ConnectAsync(address, token);
            JsonObject welcome = await client.RequestAsync("Hello", new() { ["Username"] = username }, token, "Welcome");
            client.PlayerId = welcome["PlayerId"]!.GetValue<string>();
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public async Task<JsonObject> RequestAsync(string type, JsonObject packet, CancellationToken token, string expected = "State")
    {
        string id = (++_requestNumber).ToString();
        packet["Type"] = type;
        packet["RequestId"] = id;
        await SendRawAsync(packet.ToJsonString(), token);
        while (true)
        {
            JsonObject response = await ReadAsync(token);
            if (response["RequestId"]?.GetValue<string>() != id)
            {
                if (response["Type"]?.GetValue<string>() == "Error")
                    throw new InvalidOperationException(response.ToJsonString());
                continue;
            }
            if (response["Type"]!.GetValue<string>() != expected)
                throw new InvalidOperationException($"Expected {expected}, got {response}");
            return response;
        }
    }

    public async Task<JsonObject> ReadTypeAsync(string expected, CancellationToken token)
    {
        while (true)
        {
            JsonObject response = await ReadAsync(token);
            if (response["Type"]!.GetValue<string>() == expected) return response;
        }
    }

    public async Task SendRawAsync(string text, CancellationToken token) =>
        await _socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, token);

    private async Task<JsonObject> ReadAsync(CancellationToken token)
    {
        byte[] buffer = new byte[32768];
        using MemoryStream message = new();
        WebSocketReceiveResult result;
        do
        {
            result = await _socket.ReceiveAsync(buffer, token);
            if (result.MessageType == WebSocketMessageType.Close) throw new InvalidOperationException("Unexpected close.");
            message.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        return JsonNode.Parse(message.ToArray())!.AsObject();
    }

    public void Abort() => _socket.Abort();
    public void Dispose() => _socket.Dispose();
}
