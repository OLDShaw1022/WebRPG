using System;
using System.Collections.Generic;
using System.Linq;

namespace WebRPG
{
    // 管理冒險進度、路線選擇與獎勵結算，並協調單場戰鬥。
    public sealed class AdventureModel
    {
        private const int RelicRandomSeedSalt = 0x3F6A2D17;
        private const int SkillRandomSeedSalt = 0x6D718293;
        private const int PersonalRandomSeedMultiplier = 7919;
        private const int MaximumJournalEntries = 8;
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
        private const string PlayerDefeatedMessage = "生命耗盡，挑戰失敗。";
        private const string BattleTimeoutMessage = "戰鬥超過時限，挑戰失敗。";
        private const string BattleVictoryMessagePrefix = "戰鬥勝利，取得 ";
        private const string PartyFailureMessage = "多人冒險已終止，本輪挑戰失敗。";
        private const string RunCompletedMessagePrefix = "三層 Boss 已全部擊敗，挑戰成功！本輪金幣：";
        private const string FloorEntryMessagePrefix = "進入「";


        private readonly AdventureConfiguration _configuration;
        private readonly Random _mapRandom;
        private readonly Random _encounterRandom;
        private readonly Random _relicRandom;
        private readonly Random _skillRandom;
        private readonly bool _partyMode;
        private readonly List<AdventureSkillState> _skills = new List<AdventureSkillState>();
        private AdventureBattleModel _battle;
        private readonly IReadOnlyList<AdventureSkillState> _readOnlySkills;
        private readonly IReadOnlyList<SkillDefinition> _readOnlyRewards;
        private readonly IReadOnlyList<RelicDefinition> _readOnlyRelics;
        private IReadOnlyCollection<string> _journalSnapshot = Array.Empty<string>();
        private readonly List<IReadOnlyList<int>> _readOnlyChosenPaths = new List<IReadOnlyList<int>>();
        private readonly List<SkillDefinition> _rewards = new List<SkillDefinition>();
        private readonly List<RelicDefinition> _relics = new List<RelicDefinition>();
        private readonly Queue<string> _journal = new Queue<string>();
        private readonly List<IReadOnlyList<IReadOnlyList<NodeDefinition>>> _map =
            new List<IReadOnlyList<IReadOnlyList<NodeDefinition>>>();
        private readonly List<List<int>> _chosenPaths = new List<List<int>>();
        private int _damageBonus;
        private long _projectileSequence;

        public AdventureModel(AdventureConfiguration configuration, int seed) : this(configuration, seed, false)
        {
        }

        public AdventureModel(AdventureConfiguration configuration, int seed, bool isolateRandomStreams, int personalSeed = 0)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            configuration.Validate();
            _partyMode = isolateRandomStreams;
            MaximumHealth = configuration.StartingHealth;
            Health = MaximumHealth;
            Random sharedRandom = new Random(seed);
            _mapRandom = isolateRandomStreams ? new Random(seed) : sharedRandom;
            _encounterRandom = isolateRandomStreams ? new Random(unchecked(seed ^ 0x2A53B4C1)) : sharedRandom;
            _relicRandom = isolateRandomStreams ? new Random(unchecked(seed ^ RelicRandomSeedSalt ^ personalSeed * PersonalRandomSeedMultiplier)) : sharedRandom;
            _skillRandom = isolateRandomStreams ? new Random(unchecked(seed ^ SkillRandomSeedSalt ^ personalSeed * PersonalRandomSeedMultiplier)) : sharedRandom;
            _readOnlySkills = _skills.AsReadOnly();
            _readOnlyRewards = _rewards.AsReadOnly();
            _readOnlyRelics = _relics.AsReadOnly();
        }

        public AdventureBattleModel Battle => _battle;
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
        public AdventureActionState PlayerAction => _battle?.PlayerAction;
        public AdventureProjectileState Projectile => _battle?.Projectile;
        public int LockedEnemyIndex => _battle?.LockedEnemyIndex ?? -1;
        public float BattleSpeed { get; private set; } = 1;
        public IReadOnlyList<IReadOnlyList<NodeDefinition>> CurrentFloorRows => FloorIndex < _map.Count
            ? _map[FloorIndex] : Array.Empty<IReadOnlyList<NodeDefinition>>();
        public IReadOnlyList<AdventureSkillState> Skills => _readOnlySkills;
        public IReadOnlyList<AdventureEnemyState> Enemies => _battle?.Enemies ?? Array.Empty<AdventureEnemyState>();
        public IReadOnlyList<SkillDefinition> Rewards => _readOnlyRewards;
        public IReadOnlyList<RelicDefinition> Relics => _readOnlyRelics;
        public IReadOnlyCollection<string> Journal => _journalSnapshot;
        public int RunVersion { get; private set; }
        public event Action<string> JournalEntryAdded;

        public int JournalVersion { get; private set; }
        public IReadOnlyList<NodeDefinition> AvailableNodes => Phase == AdventurePhase.Map
            ? CurrentFloorRows[RowIndex]
            : Array.Empty<NodeDefinition>();

        public IReadOnlyList<IReadOnlyList<NodeDefinition>> GetFloorRows(int floor)
        {
            return floor >= 0 && floor < _map.Count ? _map[floor] : Array.Empty<IReadOnlyList<NodeDefinition>>();
        }

        public IReadOnlyList<int> GetFloorPath(int floor)
        {
            return floor >= 0 && floor < _chosenPaths.Count ? _readOnlyChosenPaths[floor] : Array.Empty<int>();
        }

        public void StartRun()
        {
            if (Phase != AdventurePhase.Ready && Phase != AdventurePhase.Complete && Phase != AdventurePhase.Failed)
            {
                return;
            }

            _skills.Clear();
            _battle = null;
            _rewards.Clear();
            _relics.Clear();
            _journal.Clear();
            _map.Clear();
            _chosenPaths.Clear();
            _readOnlyChosenPaths.Clear();
            RunVersion++;
            foreach (FloorDefinition floor in _configuration.Floors)
            {
                _map.Add(Array.AsReadOnly(floor.Rows.Select(DrawNodes).ToArray()));
                List<int> path = new List<int>();
                _chosenPaths.Add(path);
                _readOnlyChosenPaths.Add(path.AsReadOnly());
            }

            _damageBonus = 0;
            _projectileSequence = 0;
            BattleSpeed = 1;
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

            string groupId = CurrentNode.EnemyGroupPool[_encounterRandom.Next(CurrentNode.EnemyGroupPool.Length)];
            CurrentEnemyGroup = _configuration.EnemyGroups.First(group => group.Id == groupId);
            _battle = new AdventureBattleModel(_configuration, Skills,
                CurrentEnemyGroup.EnemyIds.Select(identifier => _configuration.Enemies.First(enemy => enemy.Id == identifier)),
                Health, MaximumHealth, _damageBonus, _projectileSequence);
            _battle.MessageRecorded += Record;
            Phase = AdventurePhase.Battle;
        }

        public void Tick(float seconds)
        {
            if (Phase != AdventurePhase.Battle || float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0)
            {
                return;
            }
            _battle.Tick(seconds * BattleSpeed);
            Health = _battle.Health;
            _projectileSequence = _battle.ProjectileSequence;
            switch (_battle.Outcome)
            {
                case AdventureBattleOutcome.Won:
                    WinBattle();
                    break;
                case AdventureBattleOutcome.Defeated:
                    if (!_partyMode) Fail(PlayerDefeatedMessage);
                    break;
                case AdventureBattleOutcome.TimedOut:
                    if (!_partyMode) Fail(BattleTimeoutMessage);
                    break;
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
                    RelicDefinition relic = pool[_relicRandom.Next(pool.Length)];
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
                        if (_partyMode)
                        {
                            Record(PlayerDefeatedMessage);
                            AdvanceNode();
                        }
                        else
                        {
                            Fail(EventHealthCostFailureMessage);
                        }
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
            if (Phase == AdventurePhase.Battle)
            {
                _battle.LockEnemy(index);
            }
        }

        public bool TryUseSkill(int index)
        {
            return Phase == AdventurePhase.Battle && _battle.TryUseSkill(index);
        }

        public void SetBattleSpeed(float speed)
        {
            if (speed == 0 || speed == 1 || speed == 2)
            {
                BattleSpeed = speed;
            }
        }

        public void SynchronizePartyBattle()
        {
            if (!_partyMode || Phase != AdventurePhase.Battle || _battle == null) return;
            Health = _battle.Health;
            _projectileSequence = _battle.ProjectileSequence;
            if (_battle.Outcome == AdventureBattleOutcome.Won) WinBattle();
        }

        public void FailPartyRun()
        {
            if (Phase != AdventurePhase.Failed) Fail(PartyFailureMessage);
        }

        private void WinBattle()
        {
            if (_partyMode && Health == 0)
                Health = Math.Max(1, (int)Math.Ceiling(MaximumHealth * _configuration.Multiplayer.RevivalHealthFraction));
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
                double draw = _skillRandom.NextDouble() * totalWeight;
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
                int draw = _mapRandom.Next(candidates.Count);
                selected.Add(candidates[draw]);
                candidates.RemoveAt(draw);
            }

            return Array.AsReadOnly(row.NodePool.Where((node, index) => selected.Contains(index)).ToArray());
        }

        private void ResetCooldowns()
        {
            _battle?.Stop();
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
            while (_journal.Count > MaximumJournalEntries)
            {
                _journal.Dequeue();
            }
            _journalSnapshot = Array.AsReadOnly(_journal.ToArray());
            JournalVersion++;
            JournalEntryAdded?.Invoke(message);
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
