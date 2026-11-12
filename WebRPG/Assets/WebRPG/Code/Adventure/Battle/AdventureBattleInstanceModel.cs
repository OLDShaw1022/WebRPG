using System;
using System.Collections.Generic;
using System.Linq;

namespace WebRPG
{
    // 統一持有單人與多人戰鬥的敵人、投射物及時間，依固定順序推進動作與結算。
    internal sealed class AdventureBattleInstanceModel
    {
        private const string SentenceTerminator = "。";
        private const string DamageJournalSeparator = "，傷害 ";
        private const string SkillAttackJournalSeparator = " → ";
        private const string EnemyAttackJournalSeparator = " 攻擊，受到 ";
        private const string EnemyDamageMessageSuffix = " 傷害。";
        private const string BasicAttackName = "普攻";
        private const string HealingJournalSeparator = " 回復生命 ";

        private readonly AdventureConfiguration _configuration;
        private readonly AdventureBattleModel[] _players;
        private readonly Random _targetRandom;
        private float _accumulatedTime;
        private float _battleTime;

        public AdventureBattleInstanceModel(AdventureConfiguration configuration, AdventureBattleModel[] players,
            IEnumerable<EnemyDefinition> enemies, long projectileSequence, Random targetRandom = null)
        {
            _configuration = configuration;
            _players = (AdventureBattleModel[])players.Clone();
            _targetRandom = targetRandom;
            Enemies = Array.AsReadOnly(enemies.Select(definition => new AdventureEnemyState(definition)).ToArray());
            ProjectileSequence = projectileSequence;
            foreach (AdventureEnemyState enemy in Enemies) StartEnemyAction(enemy);
        }

        public AdventureBattleOutcome Outcome { get; private set; }
        public IReadOnlyList<AdventureEnemyState> Enemies { get; }
        public AdventureProjectileState Projectile { get; private set; }
        public long ProjectileSequence { get; private set; }
        public int SourcePlayerIndex { get; private set; } = -1;
        public int TargetPlayerIndex { get; private set; } = -1;

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
            if (CompleteIfResolved()) return;
            if (Projectile != null)
            {
                Projectile.RemainingTime = Math.Max(0, Projectile.RemainingTime - seconds);
                if (Projectile.RemainingTime > 0) return;
                ResolveProjectile();
                if (!CompleteIfResolved()) LaunchReadyAction();
                return;
            }

            _battleTime += seconds;
            foreach (AdventureBattleModel player in _players) player.AdvanceAction(seconds);
            foreach (AdventureEnemyState enemy in Enemies)
            {
                if (enemy.Health == 0 || enemy.Action == null) continue;
                enemy.RemainingSpecialCooldown = Math.Max(0, enemy.RemainingSpecialCooldown - seconds);
                enemy.Action.RemainingCastTime = Math.Max(0, enemy.Action.RemainingCastTime - seconds);
            }
            if (_battleTime >= _configuration.BattleTimeLimit)
            {
                Stop(AdventureBattleOutcome.TimedOut);
                return;
            }
            LaunchReadyAction();
        }

        private void LaunchReadyAction()
        {
            for (int index = 0; index < _players.Length; index++)
            {
                AdventureBattleModel player = _players[index];
                if (player.Health == 0 || player.PlayerAction == null || player.PlayerAction.RemainingCastTime > 0) continue;
                SourcePlayerIndex = index;
                TargetPlayerIndex = index;
                Projectile = player.LaunchPlayerAction(++ProjectileSequence);
                return;
            }
            for (int index = 0; index < Enemies.Count; index++)
            {
                AdventureEnemyState enemy = Enemies[index];
                if (enemy.Health == 0 || enemy.Action == null || enemy.Action.RemainingCastTime > 0) continue;
                int[] living = Enumerable.Range(0, _players.Length).Where(item => _players[item].Health > 0).ToArray();
                SourcePlayerIndex = -1;
                TargetPlayerIndex = _targetRandom == null ? living[0] : living[_targetRandom.Next(living.Length)];
                LaunchEnemyAction(index);
                return;
            }
        }

        private void LaunchEnemyAction(int index)
        {
            AdventureEnemyState enemy = Enemies[index];
            int damage = enemy.Action.IsSpecial ? 0 : enemy.Definition.Damage;
            int healing = 0;
            if (enemy.Action.IsSpecial)
            {
                EnemySkillDefinition special = enemy.Definition.SpecialSkill;
                foreach (EffectDefinition effect in special.Effects)
                {
                    if (effect.Type == AdventureEffectType.Damage) damage += effect.Amount;
                    else if (effect.Type == AdventureEffectType.Heal) healing += effect.Amount;
                }
                enemy.RemainingSpecialCooldown = special.Cooldown;
            }
            Projectile = new AdventureProjectileState(++ProjectileSequence, index,
                damage > 0 ? -1 : index, enemy.Action.Name, damage, healing,
                _configuration.ProjectileTravelTime, !enemy.Action.IsSpecial, enemy.Action.IsSpecial);
        }

        private void ResolveProjectile()
        {
            AdventureProjectileState projectile = Projectile;
            Projectile = null;
            AdventureBattleModel player = _players[TargetPlayerIndex];
            if (projectile.SourceEnemyIndex < 0)
            {
                if (projectile.Damage > 0 && projectile.TargetEnemyIndex >= 0)
                {
                    AdventureEnemyState target = Enemies[projectile.TargetEnemyIndex];
                    target.Health = Math.Max(0, target.Health - projectile.Damage);
                    if (target.Health == 0) target.Action = null;
                    player.RecordMessage(projectile.Name + SkillAttackJournalSeparator + target.Definition.Name +
                        DamageJournalSeparator + projectile.Damage + SentenceTerminator);
                }
                player.Heal(projectile.Healing);
            }
            else
            {
                AdventureEnemyState source = Enemies[projectile.SourceEnemyIndex];
                player.ReceiveDamage(projectile.Damage);
                source.Health = Math.Min(source.Definition.Health, source.Health + projectile.Healing);
                if (projectile.IsSpecial)
                    source.BasicAttacksUntilSpecial = source.Definition.SpecialSkill.BasicAttacksBeforeUse;
                else if (source.Definition.SpecialSkill != null)
                    source.BasicAttacksUntilSpecial = Math.Max(0, source.BasicAttacksUntilSpecial - 1);
                if (projectile.Damage > 0)
                    player.RecordMessage(source.Definition.Name + EnemyAttackJournalSeparator +
                        projectile.Damage + EnemyDamageMessageSuffix);
                if (projectile.Healing > 0)
                    player.RecordMessage(source.Definition.Name + HealingJournalSeparator +
                        projectile.Healing + SentenceTerminator);
                StartEnemyAction(source);
            }
            foreach (AdventureBattleModel participant in _players) participant.SelectLivingTarget();
        }

        private void StartEnemyAction(AdventureEnemyState enemy)
        {
            EnemySkillDefinition special = enemy.Definition.SpecialSkill;
            bool useSpecial = special != null && enemy.BasicAttacksUntilSpecial == 0 &&
                enemy.RemainingSpecialCooldown == 0;
            enemy.Action = useSpecial
                ? new AdventureActionState(special.Name, special.CastTime, -1, true)
                : new AdventureActionState(BasicAttackName, enemy.Definition.CastTime);
        }

        private bool CompleteIfResolved()
        {
            if (_players.All(player => player.Health == 0)) Stop(AdventureBattleOutcome.Defeated);
            else if (Enemies.All(enemy => enemy.Health == 0)) Stop(AdventureBattleOutcome.Won);
            return Outcome != AdventureBattleOutcome.Running;
        }

        public void Stop(AdventureBattleOutcome outcome)
        {
            if (Outcome == AdventureBattleOutcome.Running) Outcome = outcome;
            Projectile = null;
            foreach (AdventureBattleModel player in _players) player.ResetCooldowns();
        }
    }
}
