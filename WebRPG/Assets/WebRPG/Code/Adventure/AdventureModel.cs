using System;
using System.Collections.Generic;
using System.Linq;

namespace WebRPG
{
    // 管理冒險進度、戰鬥模擬、路線選擇與獎勵結算。
    public sealed class AdventureModel
    {
        private const string RunStartedMessage = "挑戰開始。血量跨戰鬥保留，請選擇路線。";
        private const string NodeEntryMessagePrefix = "前往「";
        private const string QuotedNameSentenceSuffix = "」。";
        private const string SkillAcquiredMessagePrefix = "取得技能：";
        private const string SentenceTerminator = "。";
        private const string SkillUpgradeMessageSeparator = " 升至 Lv.";
        private const string RelicAcquiredMessagePrefix = "取得遺物：";
        private const string AllRelicsCollectedMessage = "此輪已收集所有遺物，寶箱沒有新遺物。";
        private const string CampRestMessage = "在營火休息，恢復血量。";
        private const string EventHealthCostFailureMessage = "事件代價耗盡生命，挑戰失敗。";
        private const string EventGoldRewardMessagePrefix = "支付生命，取得 ";
        private const string GoldRewardMessageSuffix = " 金幣。";
        private const string EventRecoveryMessage = "選擇休養，恢復血量。";
        private const string AbandonedRunMessage = "已放棄本輪挑戰。";
        private const string BasicAttackJournalPrefix = "普攻 → ";
        private const string DamageJournalSeparator = "，傷害 ";
        private const string SkillAttackJournalSeparator = " → ";
        private const string EnemyAttackJournalSeparator = " 攻擊，受到 ";
        private const string EnemyDamageMessageSuffix = " 傷害。";
        private const string PlayerDefeatedMessage = "生命耗盡，挑戰失敗。";
        private const string BattleTimeoutMessage = "戰鬥超過時限，挑戰失敗。";
        private const string BattleVictoryMessagePrefix = "戰鬥勝利，取得 ";
        private const string RunCompletedMessagePrefix = "三層 Boss 已全部擊敗，挑戰成功！本輪金幣：";
        private const string FloorEntryMessagePrefix = "進入「";

        private const string BasicAttackName = "普攻";
        private const string HealingJournalSeparator = " 回復生命 ";

        private readonly AdventureConfiguration _configuration;
        private readonly Random _random;
        private readonly List<AdventureSkillState> _skills = new List<AdventureSkillState>();
        private readonly List<AdventureEnemyState> _enemies = new List<AdventureEnemyState>();
        private readonly List<SkillDefinition> _rewards = new List<SkillDefinition>();
        private readonly List<RelicDefinition> _relics = new List<RelicDefinition>();
        private readonly Queue<string> _journal = new Queue<string>();
        private readonly List<IReadOnlyList<IReadOnlyList<NodeDefinition>>> _map =
            new List<IReadOnlyList<IReadOnlyList<NodeDefinition>>>();
        private readonly List<List<int>> _chosenPaths = new List<List<int>>();
        private float _accumulatedTime;
        private float _battleTime;
        private int _damageBonus;
        private long _projectileSequence;

        public AdventureModel(AdventureConfiguration configuration, int seed)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            configuration.Validate();
            MaximumHealth = configuration.StartingHealth;
            Health = MaximumHealth;
            _random = new Random(seed);
        }

        public AdventureConfiguration Configuration => _configuration;
        public AdventurePhase Phase { get; private set; } = AdventurePhase.Ready;
        public int Health { get; private set; }
        public int MaximumHealth { get; private set; }
        public int Gold { get; private set; }
        public int BasicAttackDamage => _configuration.BasicAttack.Damage + _damageBonus;
        public int FloorIndex { get; private set; }
        public int RowIndex { get; private set; }
        public NodeDefinition CurrentNode { get; private set; }
        public EnemyGroupDefinition CurrentEnemyGroup { get; private set; }
        public AdventureActionState PlayerAction { get; private set; }
        public AdventureProjectileState Projectile { get; private set; }
        public int LockedEnemyIndex { get; private set; } = -1;
        public float BattleSpeed { get; private set; } = 1;
        public IReadOnlyList<IReadOnlyList<NodeDefinition>> CurrentFloorRows => FloorIndex < _map.Count
            ? _map[FloorIndex] : Array.Empty<IReadOnlyList<NodeDefinition>>();
        public IReadOnlyList<AdventureSkillState> Skills => _skills.AsReadOnly();
        public IReadOnlyList<AdventureEnemyState> Enemies => _enemies.AsReadOnly();
        public IReadOnlyList<SkillDefinition> Rewards => _rewards.AsReadOnly();
        public IReadOnlyList<RelicDefinition> Relics => _relics.AsReadOnly();
        public IReadOnlyCollection<string> Journal => _journal.ToArray();
        public IReadOnlyList<NodeDefinition> AvailableNodes => Phase == AdventurePhase.Map
            ? CurrentFloorRows[RowIndex]
            : Array.Empty<NodeDefinition>();

        public IReadOnlyList<IReadOnlyList<NodeDefinition>> GetFloorRows(int floor)
        {
            return floor >= 0 && floor < _map.Count ? _map[floor] : Array.Empty<IReadOnlyList<NodeDefinition>>();
        }

        public IReadOnlyList<int> GetFloorPath(int floor)
        {
            return floor >= 0 && floor < _chosenPaths.Count ? _chosenPaths[floor].AsReadOnly() : Array.Empty<int>();
        }

        public void StartRun()
        {
            if (Phase != AdventurePhase.Ready && Phase != AdventurePhase.Complete && Phase != AdventurePhase.Failed)
            {
                return;
            }

            _skills.Clear();
            _enemies.Clear();
            _rewards.Clear();
            _relics.Clear();
            _journal.Clear();
            _map.Clear();
            _chosenPaths.Clear();
            foreach (FloorDefinition floor in _configuration.Floors)
            {
                _map.Add(Array.AsReadOnly(floor.Rows.Select(DrawNodes).ToArray()));
                _chosenPaths.Add(new List<int>());
            }

            _damageBonus = 0;
            _projectileSequence = 0;
            BattleSpeed = 1;
            _accumulatedTime = 0;
            _battleTime = 0;
            MaximumHealth = _configuration.StartingHealth;
            Health = MaximumHealth;
            Gold = 0;
            FloorIndex = 0;
            RowIndex = 0;
            CurrentNode = null;
            CurrentEnemyGroup = null;
            foreach (string identifier in _configuration.StartingSkills)
            {
                _skills.Add(new AdventureSkillState(_configuration.Skills.First(skill => skill.Id == identifier)));
            }

            ResetCooldowns();
            Phase = AdventurePhase.Map;
            Record(RunStartedMessage);
        }

        public void SelectNode(int index)
        {
            if (Phase != AdventurePhase.Map || index < 0 || index >= AvailableNodes.Count)
            {
                return;
            }

            CurrentNode = AvailableNodes[index];
            _chosenPaths[FloorIndex].Add(index);
            Record(NodeEntryMessagePrefix + CurrentNode.Name + QuotedNameSentenceSuffix);
            if (!CurrentNode.IsBattle)
            {
                Phase = AdventurePhase.NodeChoice;
                return;
            }

            _enemies.Clear();
            string groupId = CurrentNode.EnemyGroupPool[_random.Next(CurrentNode.EnemyGroupPool.Length)];
            CurrentEnemyGroup = _configuration.EnemyGroups.First(group => group.Id == groupId);
            foreach (string identifier in CurrentEnemyGroup.EnemyIds)
            {
                _enemies.Add(new AdventureEnemyState(_configuration.Enemies.First(enemy => enemy.Id == identifier)));
            }

            ResetCooldowns();
            _accumulatedTime = 0;
            _battleTime = 0;
            Phase = AdventurePhase.Battle;
            SelectLivingTarget();
            StartPlayerBasicAttack();
            foreach (AdventureEnemyState enemy in _enemies)
            {
                StartEnemyAction(enemy);
            }
        }

        public void Tick(float seconds)
        {
            if (Phase != AdventurePhase.Battle || float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0)
            {
                return;
            }

            _accumulatedTime += seconds * BattleSpeed;
            while (Phase == AdventurePhase.Battle && _accumulatedTime >= _configuration.SimulationStep)
            {
                _accumulatedTime -= _configuration.SimulationStep;
                SimulateStep(_configuration.SimulationStep);
            }
        }

        public void SelectReward(int index)
        {
            if (Phase != AdventurePhase.SkillReward || index < 0 || index >= _rewards.Count)
            {
                return;
            }

            SkillDefinition definition = _rewards[index];
            AdventureSkillState owned = _skills.FirstOrDefault(skill => skill.Definition.Id == definition.Id);
            if (owned == null)
            {
                _skills.Add(new AdventureSkillState(definition));
                Record(SkillAcquiredMessagePrefix + definition.Name + SentenceTerminator);
            }
            else
            {
                owned.Level++;
                Record(definition.Name + SkillUpgradeMessageSeparator + owned.Level + SentenceTerminator);
            }

            _rewards.Clear();
            AdvanceNode();
        }

        public void ResolveNode(int choice)
        {
            if (Phase != AdventurePhase.NodeChoice || choice < 0 || choice > 1)
            {
                return;
            }

            if (CurrentNode.Type == AdventureNodeType.Chest)
            {
                if (choice != 0)
                {
                    return;
                }

                RelicDefinition[] pool = _configuration.Relics.Where(relic =>
                    _relics.All(owned => owned.Id != relic.Id)).ToArray();
                if (pool.Length > 0)
                {
                    RelicDefinition relic = pool[_random.Next(pool.Length)];
                    _relics.Add(relic);
                    foreach (EffectDefinition effect in relic.Effects)
                    {
                        if (effect.Type == AdventureEffectType.MaximumHealth)
                        {
                            MaximumHealth += effect.Amount;
                            Health += effect.Amount;
                        }
                        else if (effect.Type == AdventureEffectType.DamageBonus)
                        {
                            _damageBonus += effect.Amount;
                        }
                    }

                    Record(RelicAcquiredMessagePrefix + relic.Name + SentenceTerminator);
                }
                else
                {
                    Record(AllRelicsCollectedMessage);
                }
            }
            else if (CurrentNode.Type == AdventureNodeType.Camp)
            {
                if (choice == 0)
                {
                    Heal(CurrentNode.Healing);
                    Record(CampRestMessage);
                }
            }
            else if (CurrentNode.Type == AdventureNodeType.Event)
            {
                if (choice == 0)
                {
                    Health = Math.Max(0, Health - CurrentNode.HealthCost);
                    if (Health == 0)
                    {
                        Fail(EventHealthCostFailureMessage);
                        return;
                    }

                    Gold += CurrentNode.Gold;
                    Record(EventGoldRewardMessagePrefix + CurrentNode.Gold + GoldRewardMessageSuffix);
                }
                else
                {
                    Heal(CurrentNode.Healing);
                    Record(EventRecoveryMessage);
                }
            }

            AdvanceNode();
        }

        public void AbandonRun()
        {
            if (Phase == AdventurePhase.Map || Phase == AdventurePhase.Battle ||
                Phase == AdventurePhase.SkillReward || Phase == AdventurePhase.NodeChoice)
            {
                Fail(AbandonedRunMessage);
            }
        }

        public void LockEnemy(int index)
        {
            if (Phase == AdventurePhase.Battle && index >= 0 && index < _enemies.Count &&
                _enemies[index].Health > 0)
            {
                LockedEnemyIndex = index;
            }
        }

        public bool TryUseSkill(int index)
        {
            if (Phase != AdventurePhase.Battle || index < 0 || index >= _skills.Count ||
                PlayerAction == null || PlayerAction.SkillIndex >= 0 || _skills[index].RemainingCooldown > 0)
            {
                return false;
            }

            AdventureSkillState skill = _skills[index];
            PlayerAction = new AdventureActionState(skill.Definition.Name, skill.Definition.CastTime, index);
            return true;
        }

        public void SetBattleSpeed(float speed)
        {
            if (speed == 0 || speed == 1 || speed == 2)
            {
                BattleSpeed = speed;
            }
        }

        private void SimulateStep(float step)
        {
            if (Projectile != null)
            {
                Projectile.RemainingTime = Math.Max(0, Projectile.RemainingTime - step);
                if (Projectile.RemainingTime == 0)
                {
                    ResolveProjectile();
                    if (Phase == AdventurePhase.Battle)
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
                Fail(BattleTimeoutMessage);
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
                    Record(projectile.Name + SkillAttackJournalSeparator + target.Definition.Name +
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
                    Record(source.Definition.Name + EnemyAttackJournalSeparator +
                        projectile.Damage + EnemyDamageMessageSuffix);
                }
                if (projectile.Healing > 0)
                {
                    Record(source.Definition.Name + HealingJournalSeparator +
                        projectile.Healing + SentenceTerminator);
                }
                StartEnemyAction(source);
            }
            if (Health == 0)
            {
                Fail(PlayerDefeatedMessage);
                return;
            }
            if (_enemies.All(enemy => enemy.Health == 0))
            {
                WinBattle();
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

        private void WinBattle()
        {
            ResetCooldowns();
            Gold += CurrentNode.Gold;
            Record(BattleVictoryMessagePrefix + CurrentNode.Gold + GoldRewardMessageSuffix);
            _rewards.Clear();
            List<SkillDefinition> pool = _configuration.Skills.Where(definition =>
            {
                AdventureSkillState owned = _skills.FirstOrDefault(skill => skill.Definition.Id == definition.Id);
                return owned != null ? owned.Level < definition.MaximumLevel : _skills.Count < _configuration.SkillSlots;
            }).ToList();
            while (_rewards.Count < _configuration.RewardChoices && pool.Count > 0)
            {
                double totalWeight = pool.Sum(definition => (double)SkillWeight(definition));
                double draw = _random.NextDouble() * totalWeight;
                SkillDefinition selected = pool[pool.Count - 1];
                foreach (SkillDefinition candidate in pool)
                {
                    draw -= SkillWeight(candidate);
                    if (draw < 0)
                    {
                        selected = candidate;
                        break;
                    }
                }

                _rewards.Add(selected);
                pool.Remove(selected);
            }

            if (_rewards.Count == 0)
            {
                AdvanceNode();
            }
            else
            {
                Phase = AdventurePhase.SkillReward;
            }
        }

        private int SkillWeight(SkillDefinition definition)
        {
            return _skills.Any(skill => skill.Definition.Id == definition.Id)
                ? _configuration.OwnedSkillWeight : _configuration.NewSkillWeight;
        }

        private void AdvanceNode()
        {
            RowIndex++;
            if (RowIndex >= CurrentFloorRows.Count)
            {
                FloorIndex++;
                RowIndex = 0;
                if (FloorIndex >= _configuration.Floors.Length)
                {
                    Phase = AdventurePhase.Complete;
                    Record(RunCompletedMessagePrefix + Gold + SentenceTerminator);
                    return;
                }

                Record(FloorEntryMessagePrefix + _configuration.Floors[FloorIndex].Name + QuotedNameSentenceSuffix);
            }

            Phase = AdventurePhase.Map;
        }

        private IReadOnlyList<NodeDefinition> DrawNodes(MapRowDefinition row)
        {
            if (row.NodeCount == row.NodePool.Length)
            {
                return Array.AsReadOnly(row.NodePool.ToArray());
            }

            List<int> candidates = Enumerable.Range(0, row.NodePool.Length).ToList();
            HashSet<int> selected = new HashSet<int>();
            for (int index = 0; index < row.NodeCount; index++)
            {
                int draw = _random.Next(candidates.Count);
                selected.Add(candidates[draw]);
                candidates.RemoveAt(draw);
            }

            return Array.AsReadOnly(row.NodePool.Where((node, index) => selected.Contains(index)).ToArray());
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

        private void Fail(string message)
        {
            Phase = AdventurePhase.Failed;
            ResetCooldowns();
            _rewards.Clear();
            Record(message);
        }

        private void Record(string message)
        {
            _journal.Enqueue(message);
            while (_journal.Count > 8)
            {
                _journal.Dequeue();
            }
        }
    }

    // 記錄冒險技能的設定、目前等級與剩餘冷卻時間。
    public sealed class AdventureSkillState
    {
        public AdventureSkillState(SkillDefinition definition)
        {
            Definition = definition;
            RemainingCooldown = 0;
        }

        public SkillDefinition Definition { get; }
        public int Level { get; internal set; } = 1;
        public float RemainingCooldown { get; internal set; }
    }

    // 記錄敵人的生命、施放動作與特殊技能循環。
    public sealed class AdventureEnemyState
    {
        public AdventureEnemyState(EnemyDefinition definition)
        {
            Definition = definition;
            Health = definition.Health;
            BasicAttacksUntilSpecial = definition.SpecialSkill?.BasicAttacksBeforeUse ?? 0;
        }

        public EnemyDefinition Definition { get; }
        public int Health { get; internal set; }
        public AdventureActionState Action { get; internal set; }
        public int BasicAttacksUntilSpecial { get; internal set; }
        public float RemainingSpecialCooldown { get; internal set; }
    }
}
