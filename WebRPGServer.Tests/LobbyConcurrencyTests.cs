using WebRPGServer;
using WebRPGShared;

namespace WebRPGServer.Tests;

// 驗證慢連線不阻塞大廳操作、快照順序及斷線清理。
internal static class LobbyConcurrencyTests
{
    public static async Task RunAsync()
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        CancellationToken token = timeout.Token;
        LobbyHub hub = new(new LobbyOptions { SendTimeoutSeconds = 5 });
        using ControlledLobbySocket slow = new();
        using ControlledLobbySocket fast = new();
        using ControlledLobbySocket observer = new();
        Task slowConnection = hub.HandleConnectionAsync(slow, token);
        Task fastConnection = hub.HandleConnectionAsync(fast, token);
        Task observerConnection = hub.HandleConnectionAsync(observer, token);
        try
        {
            await slow.RequestAsync(new LobbyPacket { Type = LobbyPacketType.Hello, Username = "Slow" }, token);
            await fast.RequestAsync(new LobbyPacket { Type = LobbyPacketType.Hello, Username = "Fast" }, token);
            await observer.RequestAsync(new LobbyPacket { Type = LobbyPacketType.Hello, Username = "Observer" }, token);
            LobbyPacket created = await slow.RequestAsync(new LobbyPacket
            {
                Type = LobbyPacketType.CreateRoom, RoomName = "Ordered", IsPublic = true
            }, token);
            string roomId = created.Room.Id;
            slow.PauseSending();
            fast.Submit(new LobbyPacket { Type = LobbyPacketType.JoinRoom, RoomId = roomId, RequestId = "join" });
            await slow.SendStarted.WaitAsync(token);
            // 慢連線維持暫停，其他連線的加入與後續操作仍須完成。
            using CancellationTokenSource progress = new(TimeSpan.FromSeconds(2));
            LobbyPacket joined = await fast.ReadAsync(progress.Token);
            while (joined.RequestId != "join") joined = await fast.ReadAsync(progress.Token);
            await fast.RequestAsync(new LobbyPacket { Type = LobbyPacketType.SetReady, IsReady = true }, progress.Token);
            await fast.RequestAsync(new LobbyPacket { Type = LobbyPacketType.SetReady, IsReady = false }, progress.Token);
            fast.Abort();
            await fastConnection.WaitAsync(progress.Token);
            LobbyPacket state = await observer.RequestAsync(new LobbyPacket { Type = LobbyPacketType.ListRooms }, progress.Token);
            if (state.Rooms.Single(room => room.Id == roomId).MemberCount != 1)
                throw new InvalidOperationException("Disconnect cleanup was blocked by a slow recipient.");

            slow.ResumeSending();
            LobbyPacket[] snapshots = new LobbyPacket[4];
            for (int index = 0; index < snapshots.Length; index++) snapshots[index] = await slow.ReadAsync(token);
            if (snapshots[0].Room.Members.Length != 2 || snapshots[0].Room.Members[1].IsReady ||
                !snapshots[1].Room.Members[1].IsReady || snapshots[2].Room.Members[1].IsReady ||
                snapshots[3].Room.Members.Length != 1)
                throw new InvalidOperationException("Queued snapshots were mutated or delivered out of order.");
            if (slow.ConcurrentSendDetected || fast.ConcurrentSendDetected || observer.ConcurrentSendDetected)
                throw new InvalidOperationException("A connection performed concurrent sends.");
        }
        finally
        {
            slow.ResumeSending();
            slow.Abort();
            fast.Abort();
            observer.Abort();
            await Task.WhenAll(slowConnection, fastConnection, observerConnection).WaitAsync(token);
        }
        Console.WriteLine("PASS: slow-recipient isolation, ordered immutable snapshots and disconnect cleanup.");
    }
}

