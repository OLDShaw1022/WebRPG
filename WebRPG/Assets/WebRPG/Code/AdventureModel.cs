using System;
using System.Collections.Generic;
using System.Linq;

namespace WebRPG
{
    public sealed class AdventureModel
    {
        private readonly AdventureConfiguration _configuration;
        private readonly Random _random;
        private readonly List<AdventureSkillState> _skills = new List<AdventureSkillState>();
        private readonly List<AdventureEnemyState> _enemies = new List<AdventureEnemyState>();
        private readonly List<SkillDefinition> _rewards = new List<SkillDefinition>();
        private readonly List<RelicDefinition> _relics = new List<RelicDefinition>();
        private readonly Queue<string> _journal = new Queue<string>();
        private readonly List<IReadOnlyList<IReadOnlyList<NodeDefinition>>> _map =
            new List<IReadOnlyList<IReadOnlyList<NodeDefinition>>>();
        private float _accumulatedTime;
        private float _battleTime;
        private int _damageBonus;

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
        public int FloorIndex { get; private set; }
        public int RowIndex { get; private set; }
        public NodeDefinition CurrentNode { get; private set; }
        public EnemyGroupDefinition CurrentEnemyGroup { get; private set; }
        public float RemainingBasicAttackCooldown { get; private set; }
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
            foreach (FloorDefinition floor in _configuration.Floors)
            {
                _map.Add(Array.AsReadOnly(floor.Rows.Select(DrawNodes).ToArray()));
            }

            _damageBonus = 0;
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
            Record("挑戰開始。血量跨戰鬥保留，請選擇路線。");
        }

        public void SelectNode(int index)
        {
            if (Phase != AdventurePhase.Map || index < 0 || index >= AvailableNodes.Count)
            {
                return;
            }

            CurrentNode = AvailableNodes[index];
            Record("前往「" + CurrentNode.Name + "」。");
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
        }

        public void Tick(float seconds)
        {
            if (Phase != AdventurePhase.Battle || float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0)
            {
                return;
            }

            _accumulatedTime += seconds;
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
                Record("取得技能：" + definition.Name + "。");
            }
            else
            {
                owned.Level++;
                Record(definition.Name + " 升至 Lv." + owned.Level + "。");
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

                    Record("取得遺物：" + relic.Name + "。");
                }
                else
                {
                    Record("此輪已收集所有遺物，寶箱沒有新遺物。");
                }
            }
            else if (CurrentNode.Type == AdventureNodeType.Camp)
            {
                if (choice == 0)
                {
                    Heal(CurrentNode.Healing);
                    Record("在營火休息，恢復血量。");
                }
            }
            else if (CurrentNode.Type == AdventureNodeType.Event)
            {
                if (choice == 0)
                {
                    Health = Math.Max(0, Health - CurrentNode.HealthCost);
                    if (Health == 0)
                    {
                        Fail("事件代價耗盡生命，挑戰失敗。");
                        return;
                    }

                    Gold += CurrentNode.Gold;
                    Record("支付生命，取得 " + CurrentNode.Gold + " 金幣。");
                }
                else
                {
                    Heal(CurrentNode.Healing);
                    Record("選擇休養，恢復血量。");
                }
            }

            AdvanceNode();
        }

        public void AbandonRun()
        {
            if (Phase == AdventurePhase.Map || Phase == AdventurePhase.Battle ||
                Phase == AdventurePhase.SkillReward || Phase == AdventurePhase.NodeChoice)
            {
                Fail("已放棄本輪挑戰。");
            }
        }

        private void SimulateStep(float step)
        {
            _battleTime += step;
            RemainingBasicAttackCooldown -= step;
            if (RemainingBasicAttackCooldown <= 0)
            {
                RemainingBasicAttackCooldown += _configuration.BasicAttack.Cooldown;
                AdventureEnemyState target = _enemies.FirstOrDefault(enemy => enemy.Health > 0);
                if (target != null)
                {
                    int damage = _configuration.BasicAttack.Damage + _damageBonus;
                    target.Health = Math.Max(0, target.Health - damage);
                    Record("普攻 → " + target.Definition.Name + "，傷害 " + damage + "。");
                }

                if (_enemies.All(enemy => enemy.Health == 0))
                {
                    WinBattle();
                    return;
                }
            }

            foreach (AdventureSkillState skill in _skills)
            {
                skill.RemainingCooldown -= step;
                if (skill.RemainingCooldown > 0)
                {
                    continue;
                }

                skill.RemainingCooldown += skill.Definition.Cooldown;
                foreach (EffectDefinition effect in skill.Definition.Effects)
                {
                    int amount = effect.Amount + effect.PerLevel * (skill.Level - 1);
                    if (effect.Type == AdventureEffectType.Heal)
                    {
                        Heal(amount);
                    }
                    else if (effect.Type == AdventureEffectType.Damage)
                    {
                        AdventureEnemyState target = _enemies.FirstOrDefault(enemy => enemy.Health > 0);
                        if (target != null)
                        {
                            amount += _damageBonus;
                            target.Health = Math.Max(0, target.Health - amount);
                            Record(skill.Definition.Name + " → " + target.Definition.Name + "，傷害 " + amount + "。");
                        }
                    }
                }

                if (_enemies.All(enemy => enemy.Health == 0))
                {
                    WinBattle();
                    return;
                }
            }

            foreach (AdventureEnemyState enemy in _enemies)
            {
                if (enemy.Health == 0)
                {
                    continue;
                }

                enemy.RemainingCooldown -= step;
                if (enemy.RemainingCooldown > 0)
                {
                    continue;
                }

                enemy.RemainingCooldown += enemy.Definition.Cooldown;
                Health = Math.Max(0, Health - enemy.Definition.Damage);
                Record(enemy.Definition.Name + " 攻擊，受到 " + enemy.Definition.Damage + " 傷害。");
                if (Health == 0)
                {
                    Fail("生命耗盡，挑戰失敗。");
                    return;
                }
            }

            if (_battleTime >= _configuration.BattleTimeLimit)
            {
                Fail("戰鬥超過時限，挑戰失敗。");
            }
        }

        private void WinBattle()
        {
            ResetCooldowns();
            Gold += CurrentNode.Gold;
            Record("戰鬥勝利，取得 " + CurrentNode.Gold + " 金幣。");
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
                    Record("三層 Boss 已全部擊敗，挑戰成功！本輪金幣：" + Gold + "。");
                    return;
                }

                Record("進入「" + _configuration.Floors[FloorIndex].Name + "」。");
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
            RemainingBasicAttackCooldown = _configuration.BasicAttack.Cooldown;
            foreach (AdventureSkillState skill in _skills)
            {
                skill.RemainingCooldown = skill.Definition.Cooldown;
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

    public sealed class AdventureSkillState
    {
        public AdventureSkillState(SkillDefinition definition)
        {
            Definition = definition;
            RemainingCooldown = definition.Cooldown;
        }

        public SkillDefinition Definition { get; }
        public int Level { get; internal set; } = 1;
        public float RemainingCooldown { get; internal set; }
    }

    public sealed class AdventureEnemyState
    {
        public AdventureEnemyState(EnemyDefinition definition)
        {
            Definition = definition;
            Health = definition.Health;
            RemainingCooldown = definition.Cooldown;
        }

        public EnemyDefinition Definition { get; }
        public int Health { get; internal set; }
        public float RemainingCooldown { get; internal set; }
    }
}
