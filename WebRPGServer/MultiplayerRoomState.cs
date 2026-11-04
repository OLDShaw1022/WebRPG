using System.Globalization;
using WebRPGShared;

namespace WebRPGServer;

// 管理單輪冒險的投票、操作序列、回合隔離與模擬時間上限。
public sealed class MultiplayerRoomState
{
    private const int MaximumStepsPerPacket = 4;
    private const int SimulationStepsPerSecond = 20;
    private const int AllowedSimulationLeadSteps = 20;
    private const int MaximumSkillSlotIndex = 5;
    private const int MaximumEnemyTargetIndex = 3;
    private const int MaximumChoiceIndex = 2;
    private const int MaximumResolveOptionIndex = 1;
    private readonly Dictionary<string, int> _votes = new();
    private readonly DateTime _startedAt = DateTime.UtcNow;
    private int _ticks;
    public long Sequence { get; private set; }
    public int Turn { get; private set; }
    public bool Failed { get; private set; }

    public AdventurePacket Execute(string playerId, LobbyRoomSnapshot room, AdventurePacket request)
    {
        if (Failed) throw new ArgumentException("本輪冒險已結束。");
        if (request.Type != AdventurePacketType.Command || (request.Turn != Turn && request.Command != AdventureCommand.Tick))
            throw new ArgumentException("操作類型或冒險回合已失效。");
        if (!AdventureCommandPayloadHelper.TryRead(request.Payload, out int index, out int amount) || amount != 0)
            throw new ArgumentException("冒險操作格式錯誤。");
        string command = request.Command;
        switch (command)
        {
            case AdventureCommand.Vote:
                if (index < 0 || index > MaximumChoiceIndex) throw new ArgumentException("無效的地圖選項。");
                _votes[playerId] = index;
                if (_votes.Count == room.Members.Length)
                {
                    int maximum = _votes.Values.GroupBy(value => value).Max(group => group.Count());
                    int[] tied = _votes.Values.GroupBy(value => value).Where(group => group.Count() == maximum)
                        .Select(group => group.Key).Order().ToArray();
                    index = tied.Contains(_votes[room.LeaderId]) ? _votes[room.LeaderId] : tied[0];
                    command = AdventureCommand.Node;
                    _votes.Clear();
                }
                break;
            case AdventureCommand.Tick:
                if (room.LeaderId != playerId || index < 1 || index > MaximumStepsPerPacket)
                    throw new ArgumentException("只有隊長可推進模擬時間。");
                if (_ticks + index > (DateTime.UtcNow - _startedAt).TotalSeconds * SimulationStepsPerSecond + AllowedSimulationLeadSteps)
                    throw new ArgumentException("模擬時間推進過快。");
                _ticks += index;
                break;
            case AdventureCommand.Skill:
                if (index < 0 || index > MaximumSkillSlotIndex) throw new ArgumentException("無效的技能槽。");
                break;
            case AdventureCommand.Target:
                if (index < 0 || index > MaximumEnemyTargetIndex) throw new ArgumentException("無效的敵方目標。");
                break;
            case AdventureCommand.Reward:
                if (index < 0 || index > MaximumChoiceIndex) throw new ArgumentException("無效的獎勵選項。");
                break;
            case AdventureCommand.Resolve:
                if (index < 0 || index > MaximumResolveOptionIndex) throw new ArgumentException("無效的節點選項。");
                break;
            case AdventureCommand.Abandon:
                Failed = true;
                break;
            default:
                throw new ArgumentException("不支援的冒險操作。");
        }
        AdventurePacket result = new()
        {
            Type = AdventurePacketType.Command, PlayerId = playerId, RoomId = room.Id,
            Command = command, Sequence = ++Sequence, Turn = Turn,
            Payload = index.ToString(CultureInfo.InvariantCulture) + "|0"
        };
        if (command == AdventureCommand.Node) Turn++;
        return result;
    }

    public AdventurePacket? Fail(string roomId, string playerId)
    {
        if (Failed) return null;
        Failed = true;
        return new AdventurePacket
        {
            Type = AdventurePacketType.Failed, RoomId = roomId, PlayerId = playerId,
            Sequence = ++Sequence, Turn = Turn
        };
    }
}
