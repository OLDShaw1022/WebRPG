using NUnit.Framework;
using WebRPGShared;

namespace WebRPG.Tests
{
    // 驗證大廳 Presenter 的命令、準備切換與事件解除。
    public sealed class LobbyPresenterTests
    {
        [Test]
        public void CommandsUseOnlyLocalPlayerAndReadyTogglesServerState()
        {
            TestLobbyModel model = new TestLobbyModel();
            TestLobbyView view = new TestLobbyView();
            int backs = 0;
            using (LobbyPresenter presenter = new LobbyPresenter(model, view, () => backs++))
            {
                Assert.That(model.ConnectCalls, Is.EqualTo(1));
                view.Create();
                Assert.That(model.Last.Type, Is.EqualTo(LobbyPacketType.CreateRoom));
                Assert.That(model.Last.RoomName, Is.EqualTo("Room"));
                view.Join();
                Assert.That(model.Last.RoomId, Is.EqualTo("room-id"));
                view.Ready();
                Assert.That(model.Last.IsReady, Is.True);
                model.Room = new LobbyRoomSnapshot { LeaderId = "self", Members = new[]
                {
                    new LobbyMemberSnapshot { PlayerId = "self", Username = "<b>Player", IsReady = true },
                    new LobbyMemberSnapshot { PlayerId = "other", Username = "Other", IsReady = false }
                }};
                model.Notify();
                Assert.That(view.Screen.ReadyLabel, Is.EqualTo("取消準備"));
                Assert.That(view.Screen.InRoom, Is.True);
                Assert.That(view.Screen.Members, Does.Contain("<b>Player"));
                view.Ready();
                Assert.That(model.Last.IsReady, Is.False);
                view.Leave();
                Assert.That(model.Last.Type, Is.EqualTo(LobbyPacketType.LeaveRoom));
                view.Refresh();
                Assert.That(model.Last.Type, Is.EqualTo(LobbyPacketType.ListRooms));
                view.Back();
                Assert.That(backs, Is.EqualTo(1));
            }
            model.Last = null;
            view.Create();
            view.Back();
            Assert.That(model.Last, Is.Null);
            Assert.That(backs, Is.EqualTo(1));
        }

        [Test]
        public void DisconnectedStateClearsRoomWithoutAutomaticallyRetrying()
        {
            TestLobbyModel model = new TestLobbyModel();
            TestLobbyView view = new TestLobbyView();
            using (LobbyPresenter presenter = new LobbyPresenter(model, view, () => { }))
            {
                model.IsConnected = false;
                model.Room = null;
                model.Notify();
                Assert.That(view.Screen.Connected, Is.False);
                Assert.That(model.ConnectCalls, Is.EqualTo(1));
                Assert.That(view.Screen.InRoom, Is.False);
                Assert.That(view.Screen.Members, Does.Contain("尚未加入"));
            }
        }
    }
}
