using System;
using System.Collections.Generic;
using System.Linq;

namespace WebRPG
{
    public enum AdventureBattleOutcome { Running, Won, Defeated, TimedOut, Stopped }

    // 管理單場戰鬥的固定步長、角色動作、鎖定目標與投射物結算。
    public sealed class AdventureBattleModel
    {
        private const string SentenceTerminator = "。";
        private const string DamageJournalSeparator = "，傷害 ";
        private const string SkillAttackJournalSeparator = " → ";
        private const string EnemyAttackJournalSeparator = " 攻擊，受到 ";
        private const string EnemyDamageMessageSuffix = " 傷害。";
        private const string BasicAttackName = "普攻";
        private const string HealingJournalSeparator = " 回復生命 ";

        private readonly AdventureConfiguration _configuration;
        private readonly IReadOnlyList<AdventureSkillState> _skills;
        private List<AdventureEnemyState> _enemies;
        private bool _partyMode;
        private AdventureProjectileState _projectile;
        internal AdventureProjectileState SharedProjectile { get; set; }
        private readonly int _damageBonus;
        private float _accumulatedTime;
        private float _battleTime;
        private long _projectileSequence;

        public AdventureBattleModel(AdventureConfiguration configuration,
            IReadOnlyList<AdventureSkillState> skills, IEnumerable<EnemyDefinition> enemies,
            int health, int maximumHealth, int damageBonus = 0, long projectileSequence = 0)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _skills = skills ?? throw new ArgumentNullException(nameof(skills));
            _enemies = enemies.Select(definition => new AdventureEnemyState(definition)).ToList();
            Health = health;
            MaximumHealth = maximumHealth;
            _damageBonus = damageBonus;
            _projectileSequence = projectileSequence;
            ResetCooldowns();
            SelectLivingTarget();
            if (health > 0) StartPlayerBasicAttack();
            else Outcome = AdventureBattleOutcome.Defeated;
            foreach (AdventureEnemyState enemy in _enemies)
            {
                StartEnemyAction(enemy);
            }
        }

        public event Action<string> MessageRecorded;
        public AdventureBattleOutcome Outcome { get; private set; }
        public int Health { get; private set; }
        public int MaximumHealth { get; }
        public int BasicAttackDamage => _configuration.BasicAttack.Damage + _damageBonus;
        public long ProjectileSequence => _projectileSequence;
        public IReadOnlyList<AdventureEnemyState> Enemies => _enemies.AsReadOnly();
        public AdventureActionState PlayerAction { get; private set; }
        public AdventureProjectileState Projectile { get => _projectile ?? SharedProjectile; private set => _projectile = value; }
        public int LockedEnemyIndex { get; private set; } = -1;

        public void Tick(float seconds)
        {
            if (Outcome != AdventureBattleOutcome.Running || float.IsNaN(seconds) ||
                float.IsInfinity(seconds) || seconds <= 0)
            {
                return;
            }
            _accumulatedTime += seconds;
            while (Outcome == AdventureBattleOutcome.Running && _accumulatedTime >= _configuration.SimulationStep)
            {
                _accumulatedTime -= _configuration.SimulationStep;
                SimulateStep(_configuration.SimulationStep);
            }
        }

        public void Stop()
        {
            if (Outcome == AdventureBattleOutcome.Running)
            {
                Outcome = AdventureBattleOutcome.Stopped;
            }
            ResetCooldowns();
        }

        private void Finish(AdventureBattleOutcome outcome)
        {
            Outcome = outcome;
            ResetCooldowns();
        }

        public void LockEnemy(int index)
        {
            if (Outcome == AdventureBattleOutcome.Running && index >= 0 && index < _enemies.Count &&
                _enemies[index].Health > 0)
            {
                LockedEnemyIndex = index;
            }
        }

        public bool TryUseSkill(int index)
        {
            if (Outcome != AdventureBattleOutcome.Running || index < 0 || index >= _skills.Count ||
                PlayerAction == null || PlayerAction.SkillIndex >= 0 || _skills[index].RemainingCooldown > 0)
            {
                return false;
            }

            AdventureSkillState skill = _skills[index];
            PlayerAction = new AdventureActionState(skill.Definition.Name, skill.Definition.CastTime, index);
            return true;
        }

        internal void ScalePartyEnemies(MultiplayerAdventureDefinition settings, int count)
        {
            _enemies = _enemies.Select(enemy => new AdventureEnemyState(
                MultiplayerEnemyScalingHelper.Scale(enemy.Definition, settings, count))).ToList();
            foreach (AdventureEnemyState enemy in _enemies) StartEnemyAction(enemy);
        }

        internal void ConfigureParty(AdventureBattleModel first)
        {
            _partyMode = true;
            _enemies = first._enemies;
            Outcome = AdventureBattleOutcome.Running;
            SelectLivingTarget();
        }

        public void ReceivePartyDamage(int damage)
        {
            if (!_partyMode || damage <= 0) return;
            Health = Math.Max(0, Health - damage);
            if (Health == 0) PlayerAction = null;
        }

        internal void AdvancePartyPlayer(float seconds)
        {
            if (Health == 0 || PlayerAction == null) return;
            foreach (AdventureSkillState skill in _skills)
                skill.RemainingCooldown = Math.Max(0, skill.RemainingCooldown - seconds);
            PlayerAction.RemainingCastTime = Math.Max(0, PlayerAction.RemainingCastTime - seconds);
        }

        internal void AdvancePartyEnemies(float seconds)
        {
            foreach (AdventureEnemyState enemy in _enemies)
            {
                if (enemy.Health == 0 || enemy.Action == null) continue;
                enemy.RemainingSpecialCooldown = Math.Max(0, enemy.RemainingSpecialCooldown - seconds);
                enemy.Action.RemainingCastTime = Math.Max(0, enemy.Action.RemainingCastTime - seconds);
            }
        }

        internal void LaunchPartyPlayerAction() => LaunchPlayerAction();
        internal void LaunchPartyEnemyAction(int index) => LaunchEnemyAction(index);
        internal void ResolvePartyProjectile() => ResolveProjectile();
        internal void CompletePartyBattle(AdventureBattleOutcome outcome)
        {
            SharedProjectile = null;
            Finish(outcome);
        }

        private void SimulateStep(float step)
        {
            if (Projectile != null)
            {
                Projectile.RemainingTime = Math.Max(0, Projectile.RemainingTime - step);
                if (Projectile.RemainingTime == 0)
                {
                    ResolveProjectile();
                    if (Outcome == AdventureBattleOutcome.Running)
                    {
                        LaunchReadyAction();
                    }
                }
                return;
            }

            _battleTime += step;
            foreach (AdventureSkillState skill in _skills)
            {
                skill.RemainingCooldown = Math.Max(0, skill.RemainingCooldown - step);
            }
            PlayerAction.RemainingCastTime = Math.Max(0, PlayerAction.RemainingCastTime - step);
            foreach (AdventureEnemyState enemy in _enemies)
            {
                if (enemy.Health == 0)
                {
                    continue;
                }
                enemy.RemainingSpecialCooldown = Math.Max(0, enemy.RemainingSpecialCooldown - step);
                enemy.Action.RemainingCastTime = Math.Max(0, enemy.Action.RemainingCastTime - step);
            }

            if (_battleTime >= _configuration.BattleTimeLimit)
            {
                Finish(AdventureBattleOutcome.TimedOut);
                return;
            }
            LaunchReadyAction();
        }

        private void LaunchReadyAction()
        {
            if (PlayerAction.RemainingCastTime == 0)
            {
                LaunchPlayerAction();
                return;
            }
            for (int index = 0; index < _enemies.Count; index++)
            {
                AdventureEnemyState enemy = _enemies[index];
                if (enemy.Health > 0 && enemy.Action.RemainingCastTime == 0)
                {
                    LaunchEnemyAction(index);
                    return;
                }
            }
        }

        private void LaunchPlayerAction()
        {
            SelectLivingTarget();
            int damage = 0;
            int healing = 0;
            string name = PlayerAction.Name;
            bool basicAttack = PlayerAction.SkillIndex < 0;
            if (basicAttack)
            {
                damage = BasicAttackDamage;
            }
            else
            {
                AdventureSkillState skill = _skills[PlayerAction.SkillIndex];
                foreach (EffectDefinition effect in skill.Definition.Effects)
                {
                    int amount = effect.Amount + effect.PerLevel * (skill.Level - 1);
                    if (effect.Type == AdventureEffectType.Damage)
                    {
                        damage += amount + _damageBonus;
                    }
                    else if (effect.Type == AdventureEffectType.Heal)
                    {
                        healing += amount;
                    }
                }
                skill.RemainingCooldown = skill.Definition.Cooldown;
            }
            Projectile = new AdventureProjectileState(++_projectileSequence, -1,
                damage > 0 ? LockedEnemyIndex : -1, name, damage, healing,
                _configuration.ProjectileTravelTime, basicAttack, false);
            StartPlayerBasicAttack();
        }

        private void LaunchEnemyAction(int index)
        {
            AdventureEnemyState enemy = _enemies[index];
            int damage = enemy.Action.IsSpecial ? 0 : enemy.Definition.Damage;
            int healing = 0;
            if (enemy.Action.IsSpecial)
            {
                EnemySkillDefinition special = enemy.Definition.SpecialSkill;
                foreach (EffectDefinition effect in special.Effects)
                {
                    if (effect.Type == AdventureEffectType.Damage)
                    {
                        damage += effect.Amount;
                    }
                    else if (effect.Type == AdventureEffectType.Heal)
                    {
                        healing += effect.Amount;
                    }
                }
                enemy.RemainingSpecialCooldown = special.Cooldown;
            }
            Projectile = new AdventureProjectileState(++_projectileSequence, index,
                damage > 0 ? -1 : index, enemy.Action.Name, damage, healing,
                _configuration.ProjectileTravelTime, !enemy.Action.IsSpecial, enemy.Action.IsSpecial);
        }

        private void ResolveProjectile()
        {
            AdventureProjectileState projectile = Projectile;
            Projectile = null;
            if (projectile.SourceEnemyIndex < 0)
            {
                if (projectile.Damage > 0 && projectile.TargetEnemyIndex >= 0)
                {
                    AdventureEnemyState target = _enemies[projectile.TargetEnemyIndex];
                    target.Health = Math.Max(0, target.Health - projectile.Damage);
                    if (target.Health == 0)
                    {
                        target.Action = null;
                    }
                    MessageRecorded?.Invoke(projectile.Name + SkillAttackJournalSeparator + target.Definition.Name +
                        DamageJournalSeparator + projectile.Damage + SentenceTerminator);
                }
                Heal(projectile.Healing);
            }
            else
            {
                AdventureEnemyState source = _enemies[projectile.SourceEnemyIndex];
                Health = Math.Max(0, Health - projectile.Damage);
                source.Health = Math.Min(source.Definition.Health, source.Health + projectile.Healing);
                if (projectile.IsSpecial)
                {
                    source.BasicAttacksUntilSpecial = source.Definition.SpecialSkill.BasicAttacksBeforeUse;
                }
                else if (source.Definition.SpecialSkill != null)
                {
                    source.BasicAttacksUntilSpecial = Math.Max(0, source.BasicAttacksUntilSpecial - 1);
                }
                if (projectile.Damage > 0)
                {
                    MessageRecorded?.Invoke(source.Definition.Name + EnemyAttackJournalSeparator +
                        projectile.Damage + EnemyDamageMessageSuffix);
                }
                if (projectile.Healing > 0)
                {
                    MessageRecorded?.Invoke(source.Definition.Name + HealingJournalSeparator +
                        projectile.Healing + SentenceTerminator);
                }
                StartEnemyAction(source);
            }
            if (_partyMode)
            {
                if (Health == 0) PlayerAction = null;
                SelectLivingTarget();
                return;
            }
            if (Health == 0)
            {
                Finish(AdventureBattleOutcome.Defeated);
                return;
            }
            if (_enemies.All(enemy => enemy.Health == 0))
            {
                Finish(AdventureBattleOutcome.Won);
                return;
            }
            SelectLivingTarget();
        }

        private void SelectLivingTarget()
        {
            if (LockedEnemyIndex >= 0 && LockedEnemyIndex < _enemies.Count &&
                _enemies[LockedEnemyIndex].Health > 0)
            {
                return;
            }
            LockedEnemyIndex = -1;
            for (int index = 0; index < _enemies.Count; index++)
            {
                if (_enemies[index].Health > 0 && (LockedEnemyIndex < 0 ||
                    _enemies[index].Health < _enemies[LockedEnemyIndex].Health))
                {
                    LockedEnemyIndex = index;
                }
            }
        }

        private void StartPlayerBasicAttack()
        {
            PlayerAction = new AdventureActionState(BasicAttackName, _configuration.BasicAttack.CastTime);
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

        private void ResetCooldowns()
        {
            PlayerAction = null;
            Projectile = null;
            LockedEnemyIndex = -1;
            foreach (AdventureSkillState skill in _skills)
            {
                skill.RemainingCooldown = 0;
            }
        }

        private void Heal(int amount)
        {
            Health = Math.Min(MaximumHealth, Health + amount);
        }
    }
}
