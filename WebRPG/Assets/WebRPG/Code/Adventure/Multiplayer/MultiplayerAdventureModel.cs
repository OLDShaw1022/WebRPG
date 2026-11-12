using System;
using System.Collections.Generic;
using System.Linq;

namespace WebRPG
{
    // 依伺服器排序的操作推進全隊冒險，維持個人獎勵與共同進度屏障。
    public sealed class MultiplayerAdventureModel
    {
        private readonly AdventureConfiguration _configuration;
        private AdventurePartyBattleModel _battle;
        private readonly Random _targetRandom;
        public IReadOnlyList<AdventureModel> Players { get; }
        public bool CanVote => Players.All(player => player.Phase == AdventurePhase.Map) &&
            Players.All(player => player.FloorIndex == Players[0].FloorIndex && player.RowIndex == Players[0].RowIndex);
        public AdventurePartyBattleModel Battle => _battle;

        public MultiplayerAdventureModel(AdventureConfiguration configuration, int seed, string[] playerIds)
        {
            if (playerIds == null || playerIds.Length < 1
                || playerIds.Length > WebRPGShared.LobbyPacketLimits.MaximumPlayers ||
                playerIds.Distinct().Count() != playerIds.Length)
                throw new ArgumentException(nameof(playerIds));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _targetRandom = new Random(unchecked(seed ^ 0x1372B4C9));
            Players = Array.AsReadOnly(playerIds.Select((identifier, index) =>
            {
                AdventureModel player = new AdventureModel(configuration, seed, true, index + 1);
                player.StartRun();
                return player;
            }).ToArray());
        }

        public void SelectNode(int index)
        {
            if (!CanVote || index < 0 || index >= Players[0].AvailableNodes.Count) return;
            foreach (AdventureModel player in Players) player.SelectNode(index);
            if (Players.All(player => player.Phase == AdventurePhase.Battle))
                _battle = new AdventurePartyBattleModel(
                    _configuration, Players.Select(player => player.Battle).ToArray(), _targetRandom);
        }

        public void Tick(float seconds)
        {
            if (_battle == null || !Players.Any(player => player.Phase == AdventurePhase.Battle)) return;
            _battle.Tick(seconds);
            foreach (AdventureModel player in Players) player.SynchronizePartyBattle();
            if (_battle.Outcome == AdventureBattleOutcome.Defeated
                || _battle.Outcome == AdventureBattleOutcome.TimedOut)
                Fail();
        }

        public void SelectReward(int playerIndex, int choice)
        {
            if (playerIndex >= 0 && playerIndex < Players.Count) Players[playerIndex].SelectReward(choice);
        }

        public void ResolveNode(int playerIndex, int choice)
        {
            if (playerIndex >= 0 && playerIndex < Players.Count) Players[playerIndex].ResolveNode(choice);
            if (Players.All(player => player.Health == 0)) Fail();
        }

        public void UseSkill(int playerIndex, int skillIndex)
        {
            if (playerIndex >= 0 && playerIndex < Players.Count) Players[playerIndex].TryUseSkill(skillIndex);
        }

        public void LockEnemy(int playerIndex, int enemyIndex)
        {
            if (playerIndex >= 0 && playerIndex < Players.Count) Players[playerIndex].LockEnemy(enemyIndex);
        }

        public void Fail()
        {
            foreach (AdventureModel player in Players) player.FailPartyRun();
        }
    }
}
