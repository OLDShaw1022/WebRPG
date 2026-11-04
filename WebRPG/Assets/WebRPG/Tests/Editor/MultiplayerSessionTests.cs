using System;
using System.Linq;
using NUnit.Framework;
using WebRPGShared;

namespace WebRPG.Tests
{
    // 驗證多個獨立客戶端接收相同操作後維持完整戰鬥與進度一致。
    public sealed class MultiplayerSessionTests
    {
        [Test]
        public void LeaderBatchesNetworkStepsWithoutWaitingForEcho()
        {
            TestMultiplayerLobbyModel lobby = new TestMultiplayerLobbyModel
            {
                PlayerId = "self",
                Room = new LobbyRoomSnapshot
                {
                    Id = "room", LeaderId = "self",
                    Members = new[] { new LobbyMemberSnapshot { PlayerId = "self" } }
                }
            };
            using (MultiplayerAdventureSession session = new MultiplayerAdventureSession(
                lobby, 1, AdventureModelScenarioHelper.WinningConfiguration()))
            {
                for (int step = 0; step < 20; step++) session.Tick(0.05f);
                Assert.That(lobby.Sent.Count, Is.EqualTo(10));
                Assert.That(lobby.Sent.All(packet => packet.Command == AdventureCommand.Tick &&
                    packet.Payload == "2|0"), Is.True);
            }
        }

        [TestCase(2)]
        [TestCase(4)]
        public void SeparateClientsReplaySameCombatAndIndividualRewards(int count)
        {
            AdventureConfiguration configuration = AdventureModelScenarioHelper.WinningConfiguration();
            configuration.BasicAttack.Damage = 1000;
            LobbyMemberSnapshot[] members = Enumerable.Range(0, count).Select(index =>
                new LobbyMemberSnapshot { PlayerId = index.ToString(), Username = index.ToString() }).ToArray();
            TestMultiplayerLobbyModel[] lobbies = members.Select(member => new TestMultiplayerLobbyModel
            {
                PlayerId = member.PlayerId,
                Room = new LobbyRoomSnapshot { Id = "room", LeaderId = "0", Members = members }
            }).ToArray();
            MultiplayerAdventureSession[] sessions = lobbies.Select(lobby =>
                new MultiplayerAdventureSession(lobby, 123, configuration)).ToArray();
            try
            {
                long sequence = 0;
                foreach (TestMultiplayerLobbyModel lobby in lobbies) lobby.Emit(new AdventurePacket
                {
                    Type = AdventurePacketType.Command, Command = AdventureCommand.Node,
                    RoomId = "room", PlayerId = "0", Sequence = 1, Payload = "0|0"
                });
                sequence = 1;
                for (int step = 0; step < 100; step++)
                {
                    sequence++;
                    foreach (TestMultiplayerLobbyModel lobby in lobbies) lobby.Emit(new AdventurePacket
                    {
                        Type = AdventurePacketType.Command, Command = AdventureCommand.Tick,
                        RoomId = "room", PlayerId = "0", Sequence = sequence, Turn = 1, Payload = "4|0"
                    });
                }
                Assert.That(sessions.All(session => session.LocalModel.Phase == AdventurePhase.SkillReward), Is.True);
                foreach (MultiplayerAdventureSession session in sessions)
                    Assert.That(session.Party.Players.Select(player => player.Health),
                        Is.EqualTo(sessions[0].Party.Players.Select(player => player.Health)));
                for (int player = 0; player < count; player++)
                {
                    sequence++;
                    foreach (TestMultiplayerLobbyModel lobby in lobbies) lobby.Emit(new AdventurePacket
                    {
                        Type = AdventurePacketType.Command, Command = AdventureCommand.Reward,
                        RoomId = "room", PlayerId = player.ToString(), Sequence = sequence, Turn = 1, Payload = "0|0"
                    });
                    Assert.That(sessions.All(session => session.CanVote), Is.EqualTo(player == count - 1));
                }
            }
            finally { foreach (MultiplayerAdventureSession session in sessions) session.Dispose(); }
        }

        [TestCase("room", true)]
        [TestCase("other", false)]
        public void RequestErrorReachesOnlyMatchingSession(string roomId, bool fails)
        {
            TestMultiplayerLobbyModel lobby = new TestMultiplayerLobbyModel
            {
                PlayerId = "self",
                Room = new LobbyRoomSnapshot
                {
                    Id = "room", LeaderId = "self",
                    Members = new[] { new LobbyMemberSnapshot { PlayerId = "self" } }
                }
            };
            using (MultiplayerAdventureSession session = new MultiplayerAdventureSession(lobby, 1, AdventureModelScenarioHelper.WinningConfiguration()))
            {
                lobby.Emit(new AdventurePacket
                {
                    Type = AdventurePacketType.Error, RequestId = "adventure", RoomId = roomId, Error = "rejected"
                });
                Assert.That(session.LocalModel.Phase == AdventurePhase.Failed, Is.EqualTo(fails));
                if (!fails)
                {
                    lobby.Emit(new AdventurePacket
                    {
                        Type = AdventurePacketType.Command, Command = AdventureCommand.Node,
                        RoomId = "room", PlayerId = "self", Sequence = 1, Payload = "0|0"
                    });
                    Assert.That(session.Turn, Is.EqualTo(1));
                }
            }
        }

        [Test]
        public void DisconnectOrSequenceGapFailsWholeParty()
        {
            AdventureConfiguration configuration = AdventureModelScenarioHelper.WinningConfiguration();
            TestMultiplayerLobbyModel lobby = new TestMultiplayerLobbyModel
            {
                PlayerId = "self",
                Room = new LobbyRoomSnapshot
                {
                    Id = "room", LeaderId = "self",
                    Members = new[] { new LobbyMemberSnapshot { PlayerId = "self" } }
                }
            };
            using (MultiplayerAdventureSession session = new MultiplayerAdventureSession(lobby, 1, configuration))
            {
                lobby.Emit(new AdventurePacket
                {
                    Type = AdventurePacketType.Command, Command = AdventureCommand.Tick,
                    RoomId = "room", PlayerId = "self", Sequence = 2, Payload = "1|0"
                });
                Assert.That(session.LocalModel.Phase, Is.EqualTo(AdventurePhase.Failed));
            }
            using (MultiplayerAdventureSession session = new MultiplayerAdventureSession(lobby, 1, configuration))
            {
                lobby.IsConnected = false;
                lobby.Notify();
                Assert.That(session.LocalModel.Phase, Is.EqualTo(AdventurePhase.Failed));
            }
        }
    }
}
