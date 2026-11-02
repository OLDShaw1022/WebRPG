using WebRPGShared;

namespace WebRPGServer;

// 管理單一伺服器程序內的房間、成員與準備狀態，由 Hub 序列化存取。
public sealed class LobbyRegistry
{
    private readonly LobbyOptions _options;
    private readonly Dictionary<string, LobbyMemberSnapshot> _players = new();
    private readonly Dictionary<string, LobbyRoomSnapshot> _rooms = new();
    private readonly Dictionary<string, string> _memberships = new();

    public LobbyRegistry(LobbyOptions options)
    {
        options.Validate();
        _options = options;
    }

    public void Connect(string playerId, string username)
    {
        string name = username?.Trim() ?? string.Empty;
        if (name.Length == 0 || name.Length > LobbyPacketLimits.UsernameLength || name.Any(char.IsControl))
            throw new ArgumentException("玩家名稱須為 1～24 個字元，且不可含控制字元。");
        _players.Add(playerId, new LobbyMemberSnapshot { PlayerId = playerId, Username = name });
    }

    public bool Execute(string playerId, LobbyPacket request)
    {
        if (!_players.ContainsKey(playerId)) throw new ArgumentException("請先連線。");
        switch (request.Type)
        {
            case LobbyPacketType.ListRooms:
                return false;
            case LobbyPacketType.CreateRoom:
                if (_memberships.ContainsKey(playerId)) throw new ArgumentException("請先離開目前房間。");
                if (_rooms.Count >= _options.MaximumRooms) throw new ArgumentException("房間數已達上限。");
                string name = request.RoomName?.Trim() ?? string.Empty;
                if (name.Length == 0 || name.Length > LobbyPacketLimits.RoomNameLength || name.Any(char.IsControl))
                    throw new ArgumentException("房間名稱須為 1～40 個字元，且不可含控制字元。");
                LobbyRoomSnapshot created = new()
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = name,
                    LeaderId = playerId,
                    IsPublic = request.IsPublic,
                    Capacity = _options.RoomCapacity,
                    Members = new[] { _players[playerId] }
                };
                _players[playerId].IsReady = false;
                _rooms.Add(created.Id, created);
                _memberships.Add(playerId, created.Id);
                return true;
            case LobbyPacketType.JoinRoom:
                if (_memberships.ContainsKey(playerId)) throw new ArgumentException("已在房間中。");
                if (!_rooms.TryGetValue(request.RoomId ?? string.Empty, out LobbyRoomSnapshot? room))
                    throw new ArgumentException("找不到房間，請重新整理列表。");
                if (room.Members.Length >= room.Capacity) throw new ArgumentException("房間已滿。");
                _players[playerId].IsReady = false;
                room.Members = room.Members.Append(_players[playerId]).ToArray();
                _memberships.Add(playerId, room.Id);
                return true;
            case LobbyPacketType.LeaveRoom:
                Leave(playerId);
                return true;
            case LobbyPacketType.SetReady:
                if (!_memberships.ContainsKey(playerId)) throw new ArgumentException("尚未加入房間。");
                _players[playerId].IsReady = request.IsReady;
                return true;
            default:
                throw new ArgumentException("不支援的大廳操作。");
        }
    }

    public LobbyPacket Snapshot(string playerId, string requestId = "")
    {
        return new LobbyPacket
        {
            Type = LobbyPacketType.State,
            RequestId = requestId,
            PlayerId = playerId,
            Rooms = _rooms.Values.Where(room => room.IsPublic)
                .OrderBy(room => room.Id, StringComparer.Ordinal)
                .Select(room => new LobbyRoomSummary
                {
                    Id = room.Id, Name = room.Name, MemberCount = room.Members.Length, Capacity = room.Capacity
                }).ToArray(),
            Room = _memberships.TryGetValue(playerId, out string? roomId) ? _rooms[roomId] : null!
        };
    }

    public void Disconnect(string playerId)
    {
        Leave(playerId);
        _players.Remove(playerId);
    }

    private void Leave(string playerId)
    {
        if (!_memberships.Remove(playerId, out string? roomId)) return;
        LobbyRoomSnapshot room = _rooms[roomId];
        room.Members = room.Members.Where(member => member.PlayerId != playerId).ToArray();
        _players[playerId].IsReady = false;
        if (room.Members.Length == 0)
        {
            _rooms.Remove(roomId);
            return;
        }
        if (room.LeaderId == playerId) room.LeaderId = room.Members[0].PlayerId;
    }
}
