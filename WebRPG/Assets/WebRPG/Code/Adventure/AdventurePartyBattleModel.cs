using System;
using System.Linq;

namespace WebRPG
{
    // 以單一固定步長排程全隊動作、敵方目標與共享投射物。
    public sealed class AdventurePartyBattleModel
    {
        private readonly AdventureConfiguration _configuration;
        private readonly AdventureBattleModel[] _players;
        private readonly Random _targetRandom;
        private float _accumulatedTime;
        private float _battleTime;
        private AdventureBattleModel _projectileOwner;
        public AdventureBattleOutcome Outcome { get; private set; }
        public int SourcePlayerIndex { get; private set; } = -1;
        public int TargetPlayerIndex { get; private set; } = -1;
        public AdventureProjectileState Projectile => _projectileOwner?.Projectile;

        public AdventurePartyBattleModel(AdventureConfiguration configuration, AdventureBattleModel[] players, Random targetRandom)
        {
            _configuration = configuration;
            _targetRandom = targetRandom ?? throw new ArgumentNullException(nameof(targetRandom));
            _players = players;
            players[0].ScalePartyEnemies(configuration.Multiplayer, players.Length);
            foreach (AdventureBattleModel player in players) player.ConfigureParty(players[0]);
        }

        public void Tick(float seconds)
        {
            if (Outcome != AdventureBattleOutcome.Running || float.IsNaN(seconds) ||
                float.IsInfinity(seconds) || seconds <= 0) return;
            _accumulatedTime += seconds;
            while (Outcome == AdventureBattleOutcome.Running && _accumulatedTime >= _configuration.SimulationStep)
            {
                _accumulatedTime -= _configuration.SimulationStep;
                Step(_configuration.SimulationStep);
            }
        }

        private void Step(float seconds)
        {
            if (_players.All(player => player.Health == 0))
            {
                Finish(AdventureBattleOutcome.Defeated);
                return;
            }
            if (_players[0].Enemies.All(enemy => enemy.Health == 0))
            {
                Finish(AdventureBattleOutcome.Won);
                return;
            }
            if (_projectileOwner != null)
            {
                AdventureProjectileState projectile = _projectileOwner.Projectile;
                projectile.RemainingTime = Math.Max(0, projectile.RemainingTime - seconds);
                if (projectile.RemainingTime > 0) return;
                _projectileOwner.ResolvePartyProjectile();
                foreach (AdventureBattleModel player in _players) player.SharedProjectile = null;
                _projectileOwner = null;
                if (_players.All(player => player.Health == 0)) Finish(AdventureBattleOutcome.Defeated);
                else if (_players[0].Enemies.All(enemy => enemy.Health == 0)) Finish(AdventureBattleOutcome.Won);
                else LaunchReadyAction();
                return;
            }
            _battleTime += seconds;
            if (_battleTime >= _configuration.BattleTimeLimit)
            {
                Finish(AdventureBattleOutcome.TimedOut);
                return;
            }
            foreach (AdventureBattleModel player in _players) player.AdvancePartyPlayer(seconds);
            _players[0].AdvancePartyEnemies(seconds);
            LaunchReadyAction();
        }

        private void LaunchReadyAction()
        {
            for (int index = 0; index < _players.Length; index++)
            {
                AdventureBattleModel player = _players[index];
                if (player.Health > 0 && player.PlayerAction != null && player.PlayerAction.RemainingCastTime == 0)
                {
                    SourcePlayerIndex = index;
                    TargetPlayerIndex = index;
                    player.LaunchPartyPlayerAction();
                    BeginProjectile(player);
                    return;
                }
            }
            for (int index = 0; index < _players[0].Enemies.Count; index++)
            {
                AdventureEnemyState enemy = _players[0].Enemies[index];
                if (enemy.Health == 0 || enemy.Action == null || enemy.Action.RemainingCastTime > 0) continue;
                int[] living = Enumerable.Range(0, _players.Length).Where(item => _players[item].Health > 0).ToArray();
                int target = living[_targetRandom.Next(living.Length)];
                SourcePlayerIndex = -1;
                TargetPlayerIndex = target;
                _players[target].LaunchPartyEnemyAction(index);
                BeginProjectile(_players[target]);
                return;
            }
        }

        private void BeginProjectile(AdventureBattleModel owner)
        {
            _projectileOwner = owner;
            foreach (AdventureBattleModel player in _players) player.SharedProjectile = owner.Projectile;
        }

        private void Finish(AdventureBattleOutcome outcome)
        {
            Outcome = outcome;
            _projectileOwner = null;
            foreach (AdventureBattleModel player in _players) player.CompletePartyBattle(outcome);
        }
    }
}
