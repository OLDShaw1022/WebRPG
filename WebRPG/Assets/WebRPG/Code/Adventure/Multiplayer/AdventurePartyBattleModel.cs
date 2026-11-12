using System;
using System.Linq;

namespace WebRPG
{
    // 組裝多人戰鬥實例，提供共用時間推進與投射物來源及目標。
    public sealed class AdventurePartyBattleModel
    {
        private readonly AdventureBattleInstanceModel _instance;
        public AdventureBattleOutcome Outcome => _instance.Outcome;
        public int SourcePlayerIndex => _instance.SourcePlayerIndex;
        public int TargetPlayerIndex => _instance.TargetPlayerIndex;
        public AdventureProjectileState Projectile => _instance.Projectile;

        public AdventurePartyBattleModel(AdventureConfiguration configuration, AdventureBattleModel[] players,
            Random targetRandom)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (players == null || players.Length == 0 || players.Any(player => player == null) ||
                players.Distinct().Count() != players.Length)
                throw new ArgumentException(nameof(players));
            if (targetRandom == null) throw new ArgumentNullException(nameof(targetRandom));
            EnemyDefinition[] enemies = players[0].Enemies.Select(enemy =>
                MultiplayerEnemyScalingHelper.Scale(enemy.Definition, configuration.Multiplayer, players.Length))
                    .ToArray();
            _instance = new AdventureBattleInstanceModel(configuration, players, enemies,
                players.Max(player => player.ProjectileSequence), targetRandom);
            foreach (AdventureBattleModel player in players) player.JoinParty(_instance);
        }

        public void Tick(float seconds) => _instance.Tick(seconds);
    }
}
