using System;
using System.Collections.Generic;

namespace WebRPG
{
    // 管理玩家生命、技能、施放動作與鎖定目標，戰鬥狀態由共同實例持有。
    public sealed class AdventureBattleModel
    {
        private const string BasicAttackName = "普攻";

        private readonly AdventureConfiguration _configuration;
        private readonly IReadOnlyList<AdventureSkillState> _skills;
        private AdventureBattleInstanceModel _instance;
        private bool _partyMode;
        private readonly int _damageBonus;

        public AdventureBattleModel(AdventureConfiguration configuration,
            IReadOnlyList<AdventureSkillState> skills, IEnumerable<EnemyDefinition> enemies,
            int health, int maximumHealth, int damageBonus = 0, long projectileSequence = 0)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _skills = skills ?? throw new ArgumentNullException(nameof(skills));
            Health = health;
            MaximumHealth = maximumHealth;
            _damageBonus = damageBonus;
            _instance = new AdventureBattleInstanceModel(configuration, new[] { this }, enemies,
                projectileSequence);
            ResetCooldowns();
            SelectLivingTarget();
            if (health > 0) StartPlayerBasicAttack();
            else _instance.Stop(AdventureBattleOutcome.Defeated);
        }

        public event Action<string> MessageRecorded;
        public AdventureBattleOutcome Outcome => _instance.Outcome;
        public int Health { get; private set; }
        public int MaximumHealth { get; }
        public int BasicAttackDamage => _configuration.BasicAttack.Damage + _damageBonus;
        public long ProjectileSequence => _instance.ProjectileSequence;
        public IReadOnlyList<AdventureEnemyState> Enemies => _instance.Enemies;
        public AdventureActionState PlayerAction { get; private set; }
        public AdventureProjectileState Projectile => _instance.Projectile;
        public int LockedEnemyIndex { get; private set; } = -1;

        public void Tick(float seconds)
        {
            if (!_partyMode) _instance.Tick(seconds);
        }

        public void Stop() => _instance.Stop(AdventureBattleOutcome.Stopped);

        public void LockEnemy(int index)
        {
            if (Outcome == AdventureBattleOutcome.Running && index >= 0 && index < Enemies.Count &&
                Enemies[index].Health > 0)
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

        internal void JoinParty(AdventureBattleInstanceModel instance)
        {
            _partyMode = true;
            _instance = instance;
            ResetCooldowns();
            SelectLivingTarget();
            if (Health > 0) StartPlayerBasicAttack();
        }

        public void ReceivePartyDamage(int damage)
        {
            if (_partyMode && Outcome == AdventureBattleOutcome.Running) ReceiveDamage(damage);
        }

        internal void ReceiveDamage(int damage)
        {
            if (damage <= 0) return;
            Health = Math.Max(0, Health - damage);
            if (Health == 0) PlayerAction = null;
        }

        internal void AdvanceAction(float seconds)
        {
            if (Health == 0 || PlayerAction == null) return;
            foreach (AdventureSkillState skill in _skills)
                skill.RemainingCooldown = Math.Max(0, skill.RemainingCooldown - seconds);
            PlayerAction.RemainingCastTime = Math.Max(0, PlayerAction.RemainingCastTime - seconds);
        }

        internal void RecordMessage(string message) => MessageRecorded?.Invoke(message);

        internal AdventureProjectileState LaunchPlayerAction(long sequence)
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
            AdventureProjectileState projectile = new AdventureProjectileState(sequence, -1,
                damage > 0 ? LockedEnemyIndex : -1, name, damage, healing,
                _configuration.ProjectileTravelTime, basicAttack, false);
            StartPlayerBasicAttack();
            return projectile;
        }

        internal void Heal(int amount)
        {
            Health = Math.Min(MaximumHealth, Health + amount);
        }

        internal void SelectLivingTarget()
        {
            if (LockedEnemyIndex >= 0 && LockedEnemyIndex < Enemies.Count &&
                Enemies[LockedEnemyIndex].Health > 0)
            {
                return;
            }
            LockedEnemyIndex = -1;
            for (int index = 0; index < Enemies.Count; index++)
            {
                if (Enemies[index].Health > 0 && (LockedEnemyIndex < 0 ||
                    Enemies[index].Health < Enemies[LockedEnemyIndex].Health))
                {
                    LockedEnemyIndex = index;
                }
            }
        }

        private void StartPlayerBasicAttack()
        {
            PlayerAction = new AdventureActionState(BasicAttackName, _configuration.BasicAttack.CastTime);
        }

        internal void ResetCooldowns()
        {
            PlayerAction = null;
            LockedEnemyIndex = -1;
            foreach (AdventureSkillState skill in _skills)
            {
                skill.RemainingCooldown = 0;
            }
        }

    }
}
