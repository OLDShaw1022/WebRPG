using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using WebRPGShared;

namespace WebRPG
{
    // 重播伺服器排序的全隊操作，管理連線失敗、投票及固定模擬時間。
    public sealed class MultiplayerAdventureSession : IDisposable
    {
        private const float MaximumAccumulatedTimeSeconds = 0.2f;
        private const int MaximumStepsPerPacket = 4;
        private const string PayloadFormat = "{0}|0";
        private const string VoteStatusFormat = "節點投票：{0}/{1}；全員投票後前進，平票優先採隊長選擇。";
        private const string WaitingMessage = "等待隊友完成節點或領取獎勵。";
        private const string FailureMessage = "本輪多人冒險已結束；隊員離開或連線中斷時全隊失敗。";
        private const float NetworkStep = 0.05f;
        internal const float PacketInterval = 0.1f;
        private readonly ILobbyModel _lobby;
        private readonly string _roomId;
        private readonly LobbyMemberSnapshot[] _members;
        private readonly int _localIndex;
        private readonly Dictionary<string, int> _votes = new Dictionary<string, int>();
        private readonly float _timeout;
        private long _lastSequence;
        private float _accumulatedTime;
        private float _silenceTime;
        private bool _disposed;
        private bool _failed;

        public MultiplayerAdventureSession(ILobbyModel lobby, int seed, AdventureConfiguration configuration)
        {
            _lobby = lobby ?? throw new ArgumentNullException(nameof(lobby));
            if (lobby.Room == null) throw new ArgumentException(nameof(lobby));
            _roomId = lobby.Room.Id;
            _members = lobby.Room.Members.Select(member => new LobbyMemberSnapshot
            {
                PlayerId = member.PlayerId, Username = member.Username
            }).ToArray();
            _localIndex = Array.FindIndex(_members, member => member.PlayerId == lobby.PlayerId);
            if (_localIndex < 0) throw new ArgumentException(nameof(lobby));
            Party = new MultiplayerAdventureModel(configuration, seed, _members.Select(member => member.PlayerId).ToArray());
            _timeout = configuration.Multiplayer.NetworkTimeoutSeconds;
            _lobby.AdventureReceived += HandleAdventure;
            _lobby.Changed += HandleLobbyChanged;
        }

        public event Action Changed;
        public MultiplayerAdventureModel Party { get; }
        public AdventureModel LocalModel => Party.Players[_localIndex];
        public string PlayerId => _members[_localIndex].PlayerId;
        public string LocalName => _members[_localIndex].Username;
        public bool IsLeader => _lobby.Room != null && _lobby.Room.LeaderId == PlayerId;
        public int Turn { get; private set; }
        public int BattleSpeed { get; private set; } = 1;
        public bool CanVote => !_failed && Party.CanVote;
        public string Status => _failed ? FailureMessage : CanVote
            ? string.Format(VoteStatusFormat, _votes.Count, _members.Length) : WaitingMessage;
        public string ProjectileSourceId => Party.Battle == null || Party.Battle.SourcePlayerIndex < 0
            ? string.Empty : _members[Party.Battle.SourcePlayerIndex].PlayerId;
        public string ProjectileTargetId => Party.Battle == null || Party.Battle.TargetPlayerIndex < 0
            ? string.Empty : _members[Party.Battle.TargetPlayerIndex].PlayerId;
        public bool IsComplete => Party.Players.All(player => player.Phase == AdventurePhase.Complete);

        public void Tick(float seconds)
        {
            if (_disposed || _failed || IsComplete || float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0) return;
            if (!_lobby.IsConnected || _lobby.Room == null) { Fail(); return; }
            _silenceTime += seconds;
            if (_silenceTime >= _timeout)
            {
                SendCommand(AdventureCommand.Abandon);
                Fail();
                return;
            }
            if (!IsLeader) return;
            _accumulatedTime = Math.Min(MaximumAccumulatedTimeSeconds, _accumulatedTime + seconds);
            if (_accumulatedTime < PacketInterval) return;
            int steps = Math.Min(MaximumStepsPerPacket, (int)(_accumulatedTime / NetworkStep));
            _accumulatedTime -= steps * NetworkStep;
            SendCommand(AdventureCommand.Tick, steps);
        }

        public void SendCommand(string action, int index = 0)
        {
            if (_disposed || _failed) return;
            if (action == AdventureCommand.Vote && (!CanVote || index < 0 || index >= LocalModel.AvailableNodes.Count)) return;
            _lobby.SendAdventureAsync(new AdventurePacket
            {
                Type = AdventurePacketType.Command, Command = action, Turn = Turn,
                Payload = string.Format(CultureInfo.InvariantCulture, PayloadFormat, index)
            }).Forget();
        }

        public LobbyMemberSnapshot[] GetMembers()
        {
            return _members.Select(member => new LobbyMemberSnapshot
            {
                PlayerId = member.PlayerId, Username = member.Username
            }).ToArray();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _lobby.AdventureReceived -= HandleAdventure;
            _lobby.Changed -= HandleLobbyChanged;
        }

        private void HandleLobbyChanged()
        {
            if (_disposed || _failed || IsComplete) return;
            if (!_lobby.IsConnected || _lobby.Room == null || _lobby.Room.Id != _roomId ||
                !_members.Select(member => member.PlayerId).SequenceEqual(_lobby.Room.Members.Select(member => member.PlayerId)))
                Fail();
        }

        private void Fail()
        {
            if (_failed) return;
            _failed = true;
            Party.Fail();
            Changed?.Invoke();
        }

        private void HandleAdventure(AdventurePacket packet)
        {
            if (_disposed || _failed || packet == null || packet.RoomId != _roomId) return;
            if (packet.Type == AdventurePacketType.Error)
            {
                if (!IsComplete) Fail();
                return;
            }
            if (packet.Sequence <= _lastSequence) return;
            if (packet.Sequence != _lastSequence + 1)
            {
                Fail();
                return;
            }
            _lastSequence = packet.Sequence;
            _silenceTime = 0;
            if (packet.Type == AdventurePacketType.Failed && IsComplete) return;
            if (packet.Type == AdventurePacketType.Failed)
            {
                Fail();
                return;
            }
            if (packet.Type != AdventurePacketType.Command || packet.Turn != Turn ||
                !AdventureCommandPayloadHelper.TryRead(packet.Payload, out int index, out int amount) || amount != 0)
            {
                Fail();
                return;
            }
            int player = Array.FindIndex(_members, member => member.PlayerId == packet.PlayerId);
            if (player < 0) { Fail(); return; }
            switch (packet.Command)
            {
                case AdventureCommand.Vote:
                    if (CanVote) _votes[packet.PlayerId] = index;
                    break;
                case AdventureCommand.Node:
                    if (!CanVote || index < 0 || index >= LocalModel.AvailableNodes.Count) { Fail(); return; }
                    Party.SelectNode(index);
                    Turn++;
                    _votes.Clear();
                    break;
                case AdventureCommand.Resolve:
                    Party.ResolveNode(player, index);
                    break;
                case AdventureCommand.Reward:
                    Party.SelectReward(player, index);
                    break;
                case AdventureCommand.Skill:
                    Party.UseSkill(player, index);
                    break;
                case AdventureCommand.Target:
                    Party.LockEnemy(player, index);
                    break;
                case AdventureCommand.Speed:
                    if (packet.PlayerId != _lobby.Room?.LeaderId ||
                        index < AdventureCommand.MinimumBattleSpeed || index > AdventureCommand.MaximumBattleSpeed)
                    {
                        Fail();
                        return;
                    }
                    BattleSpeed = index;
                    break;
                case AdventureCommand.Tick:
                    if (packet.PlayerId != _lobby.Room?.LeaderId || index < 1 || index > MaximumStepsPerPacket) { Fail(); return; }
                    for (int step = 0; step < index * BattleSpeed; step++) Party.Tick(NetworkStep);
                    break;
                case AdventureCommand.Abandon:
                    Fail();
                    return;
                default:
                    Fail();
                    return;
            }
            Changed?.Invoke();
        }
    }
}
