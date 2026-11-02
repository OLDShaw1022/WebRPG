using System;
using System.Linq;
using WebRPGShared;

namespace WebRPG
{
    // 定義大廳畫面的操作與呈現介面。
    public interface ILobbyView
    {
        public event Action<int, string> ConnectRequested;
        public void ConfigureServers(string[] names, int defaultServerIndex);
        public event Action<string, bool> CreateRequested;
        public event Action<string> JoinRequested;
        public event Action RefreshRequested;
        public event Action LeaveRequested;
        public event Action ReadyRequested;
        public event Action BackRequested;
        public void Render(LobbyScreenModel screen);
    }

    // 保存大廳 View 所需的顯示內容與可操作狀態。
    public sealed class LobbyScreenModel
    {
        public bool Connected;
        public bool Busy;
        public bool InRoom;
        public string Status;
        public string Members;
        public string ReadyLabel;
        public LobbyRoomSummary[] Rooms;
    }

    // 將大廳操作轉成連線命令，並將伺服器快照轉為畫面資料。
    public sealed class LobbyPresenter : IDisposable
    {
        private const string RoomFormat = "房間：{0}\n房間 ID：{1}\n{2}\n{3}";
        private const string PublicLabel = "公開房間";
        private const string PrivateLabel = "私人房間（以房間 ID 加入）";
        private const string MemberFormat = "{0}{1}　{2}";
        private const string LeaderLabel = "［隊長］";
        private const string ReadyLabel = "已準備";
        private const string NotReadyLabel = "未準備";
        private const string CancelReadyLabel = "取消準備";
        private const string SetReadyLabel = "準備";
        private const string NoRoomMessage = "尚未加入房間。\n可從公開列表加入，或輸入房間 ID。";
        private const string NewLine = "\n";
        private readonly ILobbyModel _model;
        private readonly ILobbyView _view;
        private readonly Action _back;
        private bool _disposed;

        public LobbyPresenter(ILobbyModel model, ILobbyView view, Action back)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _back = back ?? throw new ArgumentNullException(nameof(back));
            _model.Changed += Refresh;
            _view.ConnectRequested += Connect;
            _view.CreateRequested += Create;
            _view.JoinRequested += Join;
            _view.RefreshRequested += List;
            _view.LeaveRequested += Leave;
            _view.ReadyRequested += Ready;
            _view.BackRequested += Back;
            _view.ConfigureServers(_model.ServerNames, _model.DefaultServerIndex);
            Refresh();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _model.Changed -= Refresh;
            _view.ConnectRequested -= Connect;
            _view.CreateRequested -= Create;
            _view.JoinRequested -= Join;
            _view.RefreshRequested -= List;
            _view.LeaveRequested -= Leave;
            _view.ReadyRequested -= Ready;
            _view.BackRequested -= Back;
        }

        private void Connect(int serverIndex, string username) => _model.ConnectAsync(serverIndex, username).Forget();
        private void Create(string name, bool isPublic) =>
            _model.SendAsync(new LobbyPacket { Type = LobbyPacketType.CreateRoom, RoomName = name, IsPublic = isPublic }).Forget();
        private void Join(string roomId) =>
            _model.SendAsync(new LobbyPacket { Type = LobbyPacketType.JoinRoom, RoomId = roomId?.Trim() }).Forget();
        private void List() => _model.SendAsync(new LobbyPacket { Type = LobbyPacketType.ListRooms }).Forget();
        private void Leave() => _model.SendAsync(new LobbyPacket { Type = LobbyPacketType.LeaveRoom }).Forget();
        private void Ready()
        {
            bool ready = _model.Room?.Members.Any(member => member.PlayerId == _model.PlayerId && member.IsReady) ?? false;
            _model.SendAsync(new LobbyPacket { Type = LobbyPacketType.SetReady, IsReady = !ready }).Forget();
        }
        private void Back() => _back();

        private void Refresh()
        {
            LobbyRoomSnapshot room = _model.Room;
            bool ready = room?.Members.Any(member => member.PlayerId == _model.PlayerId && member.IsReady) ?? false;
            string members = room == null ? NoRoomMessage : string.Format(RoomFormat, room.Name, room.Id,
                room.IsPublic ? PublicLabel : PrivateLabel,
                string.Join(NewLine, room.Members.Select(member => string.Format(MemberFormat,
                    member.PlayerId == room.LeaderId ? LeaderLabel : string.Empty,
                    member.Username, member.IsReady ? ReadyLabel : NotReadyLabel))));
            _view.Render(new LobbyScreenModel
            {
                Connected = _model.IsConnected, Busy = _model.IsBusy, InRoom = room != null,
                Status = _model.Status, Members = members,
                ReadyLabel = ready ? CancelReadyLabel : SetReadyLabel, Rooms = _model.Rooms
            });
        }
    }
}
