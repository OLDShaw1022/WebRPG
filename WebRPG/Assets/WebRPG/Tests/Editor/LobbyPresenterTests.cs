using System;
using NUnit.Framework;
using UnityEngine;
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
                Assert.That(view.SelectedServerIndex, Is.Zero);
                view.SelectedServerIndex = 1;
                view.Connect();
                Assert.That(model.ServerIndex, Is.EqualTo(1));
                Assert.That(model.Username, Is.EqualTo("Player"));
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
        public void DisconnectedStateClearsRoomAndRetainsReconnectControls()
        {
            TestLobbyModel model = new TestLobbyModel();
            TestLobbyView view = new TestLobbyView();
            using (LobbyPresenter presenter = new LobbyPresenter(model, view, () => { }))
            {
                model.IsConnected = false;
                model.Room = null;
                model.Notify();
                Assert.That(view.Screen.Connected, Is.False);
                Assert.That(view.Screen.InRoom, Is.False);
                Assert.That(view.Screen.Members, Does.Contain("尚未加入"));
            }
        }
    }

    // 模擬伺服器狀態並記錄送出的命令。
    internal sealed class TestLobbyModel : ILobbyModel
    {
        public event Action Changed;
        public event Action<AdventurePacket> AdventureReceived;
        public string[] ServerNames => new[] { "AWS EC2", "本機測試" };
        public int DefaultServerIndex => 0;
        public bool IsConnected { get; set; } = true;
        public bool IsBusy => false;
        public string PlayerId => "self";
        public string Status => "Connected";
        public LobbyRoomSnapshot Room { get; set; }
        public LobbyRoomSummary[] Rooms => Array.Empty<LobbyRoomSummary>();
        public LobbyPacket Last;
        public string Username;
        public int ServerIndex;
        public void Notify() => Changed?.Invoke();
        public Awaitable ConnectAsync(int serverIndex, string username)
        {
            ServerIndex = serverIndex;
            Username = username;
            AwaitableCompletionSource source = new AwaitableCompletionSource();
            source.SetResult();
            return source.Awaitable;
        }
        public Awaitable SendAsync(LobbyPacket packet)
        {
            Last = packet;
            AwaitableCompletionSource source = new AwaitableCompletionSource();
            source.SetResult();
            return source.Awaitable;
        }
        public Awaitable SendAdventureAsync(AdventurePacket packet)
        {
            AdventureReceived?.Invoke(packet);
            AwaitableCompletionSource source = new AwaitableCompletionSource();
            source.SetResult();
            return source.Awaitable;
        }
    }

    // 模擬大廳操作並保存最後一次呈現資料。
    internal sealed class TestLobbyView : ILobbyView
    {
        public event Action<int, string> ConnectRequested;
        public event Action<string, bool> CreateRequested;
        public event Action<string> JoinRequested;
        public event Action RefreshRequested;
        public event Action LeaveRequested;
        public event Action ReadyRequested;
        public event Action BackRequested;
        public LobbyScreenModel Screen;
        public int SelectedServerIndex;
        public void ConfigureServers(string[] names, int defaultServerIndex) => SelectedServerIndex = defaultServerIndex;
        public void Render(LobbyScreenModel screen) => Screen = screen;
        public void Connect() => ConnectRequested?.Invoke(SelectedServerIndex, "Player");
        public void Create() => CreateRequested?.Invoke("Room", true);
        public void Join() => JoinRequested?.Invoke(" room-id ");
        public void Refresh() => RefreshRequested?.Invoke();
        public void Leave() => LeaveRequested?.Invoke();
        public void Ready() => ReadyRequested?.Invoke();
        public void Back() => BackRequested?.Invoke();
    }
}
