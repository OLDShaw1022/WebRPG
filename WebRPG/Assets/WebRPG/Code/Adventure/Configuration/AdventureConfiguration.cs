using System;
using System.Collections.Generic;
using System.Linq;

namespace WebRPG
{
    // 定義並驗證冒險的玩家、技能、敵人、獎勵與地圖設定。
    public sealed class AdventureConfiguration
    {
        private const string InvalidPlayerOrRewardSettingsMessage = "Invalid player or reward settings.";
        private const string InvalidSkillWeightsMessage = "Invalid skill weights.";
        private const string InvalidBattleTimingMessage = "Invalid battle timing.";
        private const string InvalidCastTimeMessage = "Invalid action cast time.";
        private const string InvalidEnemySkillMessage = "Invalid enemy special skill.";
        private const string InvalidBasicAttackMessage = "Invalid basic attack.";
        private const string InvalidMapChoiceLimitMessage = "Invalid map choice limit.";
        private const string MissingSkillsMessage = "Missing skills.";
        private const string MissingRelicsMessage = "Missing relics.";
        private const string MissingEnemiesMessage = "Missing enemies.";
        private const string MissingEnemyGroupsMessage = "Missing enemy groups.";
        private const string RequiredFloorCountMessage = "Three floors are required.";
        private const string InvalidSkillMessagePrefix = "Invalid skill: ";
        private const string MissingRelicNameMessage = "Missing relic name.";
        private const string InvalidStartingSkillsMessage = "Invalid starting skills.";
        private const string InvalidEnemyMessagePrefix = "Invalid enemy: ";
        private const string InvalidEnemyGroupMessagePrefix = "Invalid enemy group: ";
        private const string InvalidFloorMessage = "Invalid floor.";
        private const string InvalidMapRowMessage = "Invalid map row.";
        private const string InvalidNodeMessage = "Invalid node.";
        private const string InvalidEnemyGroupPoolMessage = "Invalid enemy group pool.";
        private const string BossFloorPositionMessage = "Boss must end each floor.";
        private const string MissingEffectsMessage = "Missing effects.";
        private const string InvalidEffectModuleMessage = "Invalid effect module.";
        private const string InvalidIdentifiersMessage = "Identifiers must be nonempty and unique.";

        public MultiplayerAdventureDefinition Multiplayer { get; set; } = new MultiplayerAdventureDefinition();
        public int StartingHealth { get; set; }
        public BasicAttackDefinition BasicAttack { get; set; }
        public int MaximumMapChoices { get; set; }
        public string[] StartingSkills { get; set; }
        public int SkillSlots { get; set; }
        public int RewardChoices { get; set; }
        public int OwnedSkillWeight { get; set; }
        public int NewSkillWeight { get; set; }
        public float SimulationStep { get; set; }
        public float BattleTimeLimit { get; set; }
        public float ProjectileTravelTime { get; set; } = 0.5f;
        public SkillDefinition[] Skills { get; set; }
        public RelicDefinition[] Relics { get; set; }
        public EnemyDefinition[] Enemies { get; set; }
        public EnemyGroupDefinition[] EnemyGroups { get; set; }
        public FloorDefinition[] Floors { get; set; }

        public void Validate()
        {
            Require(Multiplayer != null && Multiplayer.EnemyHealthPerAdditionalPlayer >= 0 &&
                Multiplayer.EnemyHealthPerAdditionalPlayer <= 10 && Multiplayer.EnemyDamagePerAdditionalPlayer >= 0 &&
                Multiplayer.EnemyDamagePerAdditionalPlayer <= 10 && Multiplayer.RevivalHealthFraction > 0 &&
                Multiplayer.RevivalHealthFraction <= 1 && Multiplayer.NetworkTimeoutSeconds >= 3 &&
                Multiplayer.NetworkTimeoutSeconds <= 120, nameof(Multiplayer));
            Require(StartingHealth > 0 && SkillSlots == 6 && RewardChoices >= 1 && RewardChoices <= 3,
                InvalidPlayerOrRewardSettingsMessage);
            Require(OwnedSkillWeight > 0 && NewSkillWeight > 0 &&
                (long)OwnedSkillWeight + NewSkillWeight <= int.MaxValue, InvalidSkillWeightsMessage);
            Require(IsPositive(SimulationStep) && IsPositive(BattleTimeLimit) &&
                SimulationStep <= BattleTimeLimit, InvalidBattleTimingMessage);
            Require(BasicAttack != null && BasicAttack.Damage > 0, InvalidBasicAttackMessage);
            Require(IsPositive(BasicAttack.CastTime) && BasicAttack.CastTime >= SimulationStep &&
                IsPositive(ProjectileTravelTime) && ProjectileTravelTime >= SimulationStep, InvalidCastTimeMessage);
            Require(MaximumMapChoices >= 1 && MaximumMapChoices <= 3, InvalidMapChoiceLimitMessage);
            Require(Skills != null && Skills.Length > 0 && Skills.All(skill => skill != null), MissingSkillsMessage);
            Require(Relics != null && Relics.Length > 0 && Relics.All(relic => relic != null), MissingRelicsMessage);
            Require(Enemies != null && Enemies.Length > 0 && Enemies.All(enemy => enemy != null), MissingEnemiesMessage);
            Require(EnemyGroups != null && EnemyGroups.Length > 0 && EnemyGroups.All(group => group != null),
                MissingEnemyGroupsMessage);
            Require(Floors != null && Floors.Length == 3 && Floors.All(floor => floor != null), RequiredFloorCountMessage);
            RequireUnique(Skills.Select(skill => skill.Id));
            RequireUnique(Relics.Select(relic => relic.Id));
            RequireUnique(Enemies.Select(enemy => enemy.Id));
            RequireUnique(EnemyGroups.Select(group => group.Id));
            foreach (SkillDefinition skill in Skills)
            {
                Require(!string.IsNullOrWhiteSpace(skill.Name) && IsPositive(skill.Cooldown) && skill.Cooldown >= SimulationStep && skill.MaximumLevel > 0,
                    InvalidSkillMessagePrefix + skill.Id);
                Require(IsPositive(skill.CastTime) && skill.CastTime >= SimulationStep, InvalidCastTimeMessage);
                ValidateEffects(skill.Effects, false);
            }

            foreach (RelicDefinition relic in Relics)
            {
                Require(!string.IsNullOrWhiteSpace(relic.Name), MissingRelicNameMessage);
                ValidateEffects(relic.Effects, true);
            }

            Require(StartingSkills != null && StartingSkills.Length > 0 && StartingSkills.Length <= SkillSlots &&
                StartingSkills.Distinct().Count() == StartingSkills.Length &&
                StartingSkills.All(id => Skills.Any(skill => skill.Id == id)), InvalidStartingSkillsMessage);
            foreach (EnemyDefinition enemy in Enemies)
            {
                Require(!string.IsNullOrWhiteSpace(enemy.Name) && enemy.Health > 0 && enemy.Damage > 0 &&
                    IsPositive(enemy.CastTime) && enemy.CastTime >= SimulationStep, InvalidEnemyMessagePrefix + enemy.Id);
                if (enemy.SpecialSkill != null)
                {
                    EnemySkillDefinition special = enemy.SpecialSkill;
                    Require(!string.IsNullOrWhiteSpace(special.Id) && !string.IsNullOrWhiteSpace(special.Name) &&
                        IsPositive(special.CastTime) && special.CastTime >= SimulationStep &&
                        IsPositive(special.Cooldown) && special.Cooldown >= SimulationStep &&
                        special.BasicAttacksBeforeUse > 0, InvalidEnemySkillMessage);
                    ValidateEffects(special.Effects, false);
                }
            }

            foreach (EnemyGroupDefinition group in EnemyGroups)
            {
                Require(group.EnemyIds != null && group.EnemyIds.Length >= 1 && group.EnemyIds.Length <= 4 &&
                    group.EnemyIds.All(id => Enemies.Any(enemy => enemy.Id == id)), InvalidEnemyGroupMessagePrefix + group.Id);
            }

            foreach (FloorDefinition floor in Floors)
            {
                Require(!string.IsNullOrWhiteSpace(floor.Name) && floor.Rows != null && floor.Rows.Length > 0,
                    InvalidFloorMessage);
                foreach (MapRowDefinition row in floor.Rows)
                {
                    Require(row != null && row.NodePool != null && row.NodePool.Length > 0 &&
                        row.NodeCount >= 1 && row.NodeCount <= MaximumMapChoices &&
                        row.NodeCount <= row.NodePool.Length, InvalidMapRowMessage);
                    foreach (NodeDefinition node in row.NodePool)
                    {
                        Require(node != null && !string.IsNullOrWhiteSpace(node.Name) &&
                            Enum.IsDefined(typeof(AdventureNodeType), node.Type) && node.Gold >= 0 &&
                            node.Healing >= 0 && node.HealthCost >= 0, InvalidNodeMessage);
                        if (node.IsBattle)
                        {
                            Require(node.EnemyGroupPool != null && node.EnemyGroupPool.Length > 0 &&
                                node.EnemyGroupPool.All(id => EnemyGroups.Any(group => group.Id == id)),
                                InvalidEnemyGroupPoolMessage);
                            RequireUnique(node.EnemyGroupPool);
                        }
                    }

                    RequireUnique(row.NodePool.Select(node => node.Name));
                }

                Require(floor.Rows.Last().NodePool.All(node => node.Type == AdventureNodeType.Boss) &&
                    floor.Rows.Take(floor.Rows.Length - 1).SelectMany(row => row.NodePool)
                        .All(node => node.Type != AdventureNodeType.Boss), BossFloorPositionMessage);
            }
        }

        private static bool IsPositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0;
        }

        private static void ValidateEffects(EffectDefinition[] effects, bool passive)
        {
            Require(effects != null && effects.Length > 0 && effects.All(effect => effect != null), MissingEffectsMessage);
            foreach (EffectDefinition effect in effects)
            {
                Require(effect.Amount > 0 && effect.PerLevel >= 0 &&
                    (passive ? effect.Type == AdventureEffectType.MaximumHealth || effect.Type == AdventureEffectType.DamageBonus
                        : effect.Type == AdventureEffectType.Damage || effect.Type == AdventureEffectType.Heal),
                    InvalidEffectModuleMessage);
            }
        }

        private static void RequireUnique(IEnumerable<string> identifiers)
        {
            string[] values = identifiers.ToArray();
            Require(values.All(value => !string.IsNullOrWhiteSpace(value)) &&
                values.Distinct().Count() == values.Length, InvalidIdentifiersMessage);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new ArgumentException(message);
            }
        }
    }
}
