using NUnit.Framework;
using WebRPGServer;
using WebRPGShared;
using static WebRPGServer.Tests.MultiplayerProtocolTestHelper;

namespace WebRPGServer.Tests;

// 驗證多人投票、回合隔離、權限與失敗終止的伺服器規則。
[TestFixture]
[Category("Unit")]
public sealed class MultiplayerProtocolTests
{
    [Test]
    public void AdventureProtocolEnforcesVotesTurnsAndAuthority()
    {
        LobbyRegistry registry = new(new LobbyOptions());
        registry.Connect("a", "A");
        registry.Connect("b", "B");
        registry.Execute("a", new LobbyPacket { Type = LobbyPacketType.CreateRoom, RoomName = "Party", IsPublic = true });
        string room = registry.Snapshot("a").Room.Id;
        registry.Execute("b", new LobbyPacket { Type = LobbyPacketType.JoinRoom, RoomId = room });
        Reject(() => registry.ExecuteAdventure("b", new AdventurePacket { Type = AdventurePacketType.Start }));
        Reject(() => registry.ExecuteAdventure("a", null!));
        registry.Execute("a", new LobbyPacket { Type = LobbyPacketType.SetReady, IsReady = true });
        registry.Execute("b", new LobbyPacket { Type = LobbyPacketType.SetReady, IsReady = true });
        registry.ExecuteAdventure("a", new AdventurePacket { Type = AdventurePacketType.Start });
        AdventurePacket vote = registry.ExecuteAdventure("a",
            new AdventurePacket { Type = AdventurePacketType.Command, Command = "vote", Payload = "1|0" });
        Check(vote.Command == "vote", "A vote must wait for all members.");
        registry.ExecuteAdventure("a", new AdventurePacket { Type = AdventurePacketType.Command, Command = "vote", Payload = "0|0" });
        AdventurePacket selected = registry.ExecuteAdventure("b",
            new AdventurePacket { Type = AdventurePacketType.Command, Command = "vote", Payload = "1|0" });
        Check(selected.Command == "node" && selected.Payload == "0|0", "Tie must use the leader's current vote.");
        Reject(() => registry.ExecuteAdventure("b",
            new AdventurePacket { Type = AdventurePacketType.Command, Command = "speed", Payload = "2|0", Turn = 1 }));
        Reject(() => registry.ExecuteAdventure("a",
            new AdventurePacket { Type = AdventurePacketType.Command, Command = "speed", Payload = "3|0", Turn = 1 }));
        Reject(() => registry.ExecuteAdventure("a",
            new AdventurePacket { Type = AdventurePacketType.Command, Command = "speed", Payload = "2|0", Turn = 0 }));
        AdventurePacket speed = registry.ExecuteAdventure("a",
            new AdventurePacket { Type = AdventurePacketType.Command, Command = "speed", Payload = "2|0", Turn = 1 });
        Check(speed.Command == "speed" && speed.Payload == "2|0" && speed.Sequence == selected.Sequence + 1,
            "Only a valid leader speed change should advance the shared sequence.");
        AdventurePacket pendingTick = registry.ExecuteAdventure("a",
            new AdventurePacket { Type = AdventurePacketType.Command, Command = "tick", Payload = "1|0", Turn = 0 });
        Check(pendingTick.Turn == 1, "An in-flight tick must adopt the new turn.");
        Reject(() => registry.ExecuteAdventure("a",
            new AdventurePacket { Type = AdventurePacketType.Command, Command = "skill", Payload = "0|0", Turn = 0 }));
        Reject(() => registry.ExecuteAdventure("b",
            new AdventurePacket { Type = AdventurePacketType.Command, Command = "tick", Payload = "1|0", Turn = 1 }));
        Reject(() => registry.ExecuteAdventure("b",
            new AdventurePacket { Type = AdventurePacketType.Command, Command = "hit", Payload = "0|500", Turn = 1 }));
        AdventurePacket? failure = registry.FailAdventure("b");
        Check(failure?.Type == AdventurePacketType.Failed, "Disconnect must terminate the run.");
        Reject(() => registry.ExecuteAdventure("a",
            new AdventurePacket { Type = AdventurePacketType.Command, Command = "vote", Payload = "0|0", Turn = 1 }));

    }

}
