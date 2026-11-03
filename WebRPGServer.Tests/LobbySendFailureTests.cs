using WebRPGServer;
using WebRPGShared;

namespace WebRPGServer.Tests;

// 驗證傳送逾時、取消與佇列滿載只終止對應連線且完整清理。
internal static class LobbySendFailureTests
{
    public static async Task RunAsync()
    {
        foreach (string scenario in new[] { "timeout", "cancellation", "overflow" })
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(8));
            using CancellationTokenSource connectionCancellation = new();
            LobbyHub hub = new(new LobbyOptions { SendTimeoutSeconds = scenario == "timeout" ? 1 : 5 });
            using ControlledLobbySocket slow = new();
            using ControlledLobbySocket observer = new();
            Task slowConnection = hub.HandleConnectionAsync(slow, connectionCancellation.Token);
            Task observerConnection = hub.HandleConnectionAsync(observer, timeout.Token);
            try
            {
                await slow.RequestAsync(new LobbyPacket { Type = LobbyPacketType.Hello, Username = "Slow" }, timeout.Token);
                await observer.RequestAsync(new LobbyPacket { Type = LobbyPacketType.Hello, Username = "Observer" }, timeout.Token);
                LobbyPacket created = await slow.RequestAsync(new LobbyPacket
                {
                    Type = LobbyPacketType.CreateRoom, RoomName = scenario, IsPublic = true
                }, timeout.Token);
                slow.PauseSending();
                slow.Submit(new LobbyPacket { Type = LobbyPacketType.ListRooms });
                await slow.SendStarted.WaitAsync(timeout.Token);
                if (scenario == "cancellation") connectionCancellation.Cancel();
                if (scenario == "overflow")
                {
                    for (int index = 0; index < 140; index++)
                        slow.Submit(new LobbyPacket { Type = LobbyPacketType.ListRooms });
                }
                await slowConnection.WaitAsync(TimeSpan.FromSeconds(scenario == "timeout" ? 3 : 2), timeout.Token);
                LobbyPacket state = await observer.RequestAsync(new LobbyPacket { Type = LobbyPacketType.ListRooms }, timeout.Token);
                if (state.Rooms.Any(room => room.Id == created.Room.Id))
                    throw new InvalidOperationException(scenario + " did not remove the disconnected room.");
                if (slow.ConcurrentSendDetected)
                    throw new InvalidOperationException(scenario + " caused concurrent sends.");
            }
            finally
            {
                slow.ResumeSending();
                slow.Abort();
                observer.Abort();
                await Task.WhenAll(slowConnection, observerConnection).WaitAsync(timeout.Token);
            }
        }
        Console.WriteLine("PASS: send timeout, cancellation, bounded queue overflow and room cleanup.");
    }
}
