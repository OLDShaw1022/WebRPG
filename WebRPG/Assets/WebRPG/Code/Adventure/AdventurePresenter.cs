using System;
using System.Linq;

namespace WebRPG
{
    // 定義冒險畫面的操作事件與資料呈現介面。
    public interface IAdventureView
    {
        public event Action StartRequested;
        public event Action AbandonRequested;
        public event Action<int> ChoiceRequested;
        public event Action<string> CharacterFocusRequested;
        public event Action MapToggleRequested;
        public void Render(AdventureScreenModel screen);
    }

    // 承載冒險畫面的文字、角色、地圖與操作狀態。
    public sealed class AdventureScreenModel
    {
        public string Heading { get; set; }
        public string Summary { get; set; }
        public string Situation { get; set; }
        public string Journal { get; set; }
        public string[] Choices { get; set; }
        public AdventureGaugeScreenModel PlayerHealth { get; set; }
        public AdventureGaugeScreenModel BasicAttack { get; set; }
        public AdventureGaugeScreenModel[] Skills { get; set; }
        public AdventureCharacterScreenModel[] Players { get; set; }
        public AdventureCharacterScreenModel[] Enemies { get; set; }
        public AdventureMapRowScreenModel[] MapRows { get; set; }
        public int MapCurrentRow { get; set; }
        public string MapNotice { get; set; }
        public bool ShowMap { get; set; }
        public bool CanToggleMap { get; set; }
        public string MapButtonText { get; set; }
        public string InspectedCharacterId { get; set; }
        public string InspectedCharacterName { get; set; }
        public AdventureGaugeScreenModel[] InspectedSkills { get; set; }
        public bool ShowBattle { get; set; }
        public AdventureIconScreenModel[] Relics { get; set; } = Array.Empty<AdventureIconScreenModel>();
        public bool CanStart { get; set; }
        public bool CanAbandon { get; set; }
    }

    // 承載遺物圖示的識別、名稱與被動效果。
    public sealed class AdventureIconScreenModel
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
    }

    // 承載數值條的標題、顯示文字與填滿比例。
    public sealed class AdventureGaugeScreenModel
    {
        public string Title { get; set; }
        public string Text { get; set; }
        public float FillAmount { get; set; }
        public string IconId { get; set; }
        public string Description { get; set; }
        public string CompactText { get; set; }
        public string DisplayName { get; set; }
    }

    // 承載角色身分、存活狀態、生命值與技能畫面資料。
    public sealed class AdventureCharacterScreenModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool IsLocalPlayer { get; set; }
        public bool IsDefeated { get; set; }
        public AdventureGaugeScreenModel Health { get; set; }
        public AdventureGaugeScreenModel BasicAttack { get; set; }
        public AdventureGaugeScreenModel[] Skills { get; set; }
    }

    // 承載地圖列的狀態文字與節點畫面資料。
    public sealed class AdventureMapRowScreenModel
    {
        public string Status { get; set; }
        public AdventureMapNodeScreenModel[] Nodes { get; set; }
    }

    // 承載地圖節點的標題、狀態與選取資訊。
    public sealed class AdventureMapNodeScreenModel
    {
        public string Title { get; set; }
        public string StateText { get; set; }
        public bool CanSelect { get; set; }
        public bool IsChosen { get; set; }
        public int ChoiceIndex { get; set; }
    }

    // 處理冒險介面操作，並將遊戲狀態轉換為畫面資料。
    public sealed class AdventurePresenter : IDisposable
    {
        private const string SkillSlotTitlePrefix = "技能槽 ";
        private const string UnequippedText = "尚未裝備";
        private const string SkillLevelSeparator = " Lv.";
        private const string BasicAttackTitlePrefix = "普攻 · 傷害 ";
        private const string LocalPlayerName = "本機玩家";
        private const string EnemyIdPrefix = "enemy:";
        private const string MapHeading = "挑戰地圖";
        private const string CountSeparator = " / ";
        private const string SummaryGoldPrefix = " · 金幣 ";
        private const string SummaryFloorPrefix = " · 第 ";
        private const string SummaryFloorSuffix = " 層";
        private const string NewLine = "\n";
        private const string ReturnToAdventureText = "返回挑戰";
        private const string MapButtonText = "地圖";
        private const string MapSelectionNotice = "選擇亮框節點前往；路線由下往上推進。";
        private const string BattleMapNotice = "檢視地圖時戰鬥仍持續進行，節點僅供查看。";
        private const string ResultMapNotice = "地圖檢視中；返回挑戰繼續領獎或查看結算。";
        private const string EmptyIconId = "empty";
        private const string EmptySlotName = "空槽";
        private const string BasicAttackName = "普攻";
        private const string AttackIconId = "attack";
        private const string CompactIdleText = "待戰";
        private const string CompactDefeatedText = "倒下";
        private const string CompactSecondsSuffix = "秒";
        private const string SkillCooldownPrefix = "冷卻 ";
        private const string EffectListSeparator = "、";
        private const string MaximumHealthBonusPrefix = "生命上限+";
        private const string DamageBonusPrefix = "傷害+";
        private const string HealthTitle = "生命";
        private const string HealthValuePrefix = "生命 ";
        private const string DefeatedText = "已擊倒";
        private const string CooldownTextPrefix = "倒數 ";
        private const string SecondsFormat = "0.0";
        private const string SecondsSuffix = " 秒";
        private const string IdleCooldownTextPrefix = "待戰 · ";
        private const string ReadyHeading = "灰燼之塔 · 單人試玩";
        private const string NextNodeHeading = "選擇下一個節點";
        private const string BattleHeading = "自動戰鬥中";
        private const string SkillRewardHeading = "戰利品 · 選擇一個技能";
        private const string CompleteHeading = "挑戰成功";
        private const string FinishedHeading = "挑戰結束";
        private const string PassedRowText = "已通過";
        private const string CurrentRowText = "目前所在";
        private const string UnreachedNodeText = "未到達";
        private const string ChosenNodeText = "已選擇";
        private const string SkippedNodeText = "已略過";
        private const string SelectableNodeText = "可前往";
        private const string BattleInstructions = "普攻與技能自動施放，依序攻擊第一個存活敵人。";
        private const string ResultGoldPrefix = "本輪結算：";
        private const string ResultGoldSuffix = " 金幣\n";
        private const string ResultSkillsPrefix = "技能 ";
        private const string ResultRelicsSeparator = " 個 · 遺物 ";
        private const string ResultCountSuffix = " 個\n";
        private const string ResultPersistenceNotice = "單人原型：結算只供本輪檢視，不會保存至帳號。";
        private const string SkillRewardInstructions = "新技能自動裝入空槽；重複技能直接升級。\n六槽裝滿後，只會出現已裝備且未滿級的技能。";
        private const string ReadyInstructions = "從右上選單開始新一輪挑戰。\n可在選單中放棄進行中的挑戰，該輪會以失敗結算。";
        private const string NodeChoiceInstructions = "選擇下方行動。寶箱提供被動遺物，事件可交換生命與金幣。";
        private const string TitleSeparator = " · ";
        private const string AcquireSkillPrefix = "取得 ";
        private const string UpgradeSkillPrefix = "升級 ";
        private const string RewardCooldownSuffix = " 秒\n";
        private const string DamageEffectPrefix = "傷害 ";
        private const string HealingEffectPrefix = "治療 ";
        private const string ChestChoiceText = "開啟寶箱 · 隨機取得一個未持有的遺物";
        private const string CampRestChoicePrefix = "休息 · 恢復 ";
        private const string HealthSuffix = " 生命";
        private const string LeaveNodeChoiceText = "直接離開";
        private const string EventSacrificeChoicePrefix = "獻祭 ";
        private const string EventGoldRewardSeparator = " 生命 · 取得 ";
        private const string GoldSuffix = " 金幣";
        private const string EventRecoveryChoicePrefix = "休養 · 恢復 ";
        private const string BattleNodeLabel = "普通戰鬥";
        private const string EliteNodeLabel = "精英戰鬥";
        private const string BossNodeLabel = "Boss 戰";
        private const string ChestNodeLabel = "遺物";
        private const string CampNodeLabel = "營火";
        private const string EventNodeLabel = "事件";

        private readonly AdventureModel _model;
        private readonly IAdventureView _view;
        private const string LocalPlayerId = "local-player";
        private bool _disposed;
        private bool _paused;
        private bool _mapOpen;
        private string _inspectedCharacterId = LocalPlayerId;

        public AdventurePresenter(AdventureModel model, IAdventureView view)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _view.StartRequested += HandleStart;
            _view.AbandonRequested += HandleAbandon;
            _view.ChoiceRequested += HandleChoice;
            _view.CharacterFocusRequested += HandleCharacterFocus;
            _view.MapToggleRequested += HandleMapToggle;
            Refresh();
        }

        public void Tick(float seconds)
        {
            if (_disposed || _paused || _model.Phase != AdventurePhase.Battle)
            {
                return;
            }

            _model.Tick(seconds);
            Refresh();
        }

        public void SetPaused(bool paused)
        {
            _paused = paused;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _view.StartRequested -= HandleStart;
            _view.AbandonRequested -= HandleAbandon;
            _view.ChoiceRequested -= HandleChoice;
            _view.CharacterFocusRequested -= HandleCharacterFocus;
            _view.MapToggleRequested -= HandleMapToggle;
        }

        private void HandleStart()
        {
            _mapOpen = false;
            _inspectedCharacterId = LocalPlayerId;
            _model.StartRun();
            Refresh();
        }

        private void HandleAbandon()
        {
            _mapOpen = false;
            _model.AbandonRun();
            Refresh();
        }

        private void HandleChoice(int index)
        {
            switch (_model.Phase)
            {
                case AdventurePhase.Map:
                    _model.SelectNode(index);
                    if (_model.Phase != AdventurePhase.Map)
                    {
                        _mapOpen = false;
                    }
                    break;
                case AdventurePhase.SkillReward:
                    _model.SelectReward(index);
                    break;
                case AdventurePhase.NodeChoice:
                    _model.ResolveNode(index);
                    break;
            }

            Refresh();
        }

        private void HandleCharacterFocus(string characterId)
        {
            _inspectedCharacterId = characterId;
            Refresh();
        }

        private void HandleMapToggle()
        {
            if (_model.Phase == AdventurePhase.Ready || _model.Phase == AdventurePhase.Map)
            {
                return;
            }

            _mapOpen = !_mapOpen;
            _inspectedCharacterId = LocalPlayerId;
            Refresh();
        }

        private void Refresh()
        {
            bool ended = _model.Phase == AdventurePhase.Ready || _model.Phase == AdventurePhase.Complete ||
                _model.Phase == AdventurePhase.Failed;
            bool fighting = _model.Phase == AdventurePhase.Battle;
            AdventureGaugeScreenModel[] slots = Enumerable.Range(0, _model.Configuration.SkillSlots).Select(index =>
            {
                if (index >= _model.Skills.Count)
                {
                    return new AdventureGaugeScreenModel
                    {
                        Title = SkillSlotTitlePrefix + (index + 1),
                        Text = UnequippedText,
                        IconId = EmptyIconId,
                        DisplayName = EmptySlotName,
                        Description = UnequippedText,
                        CompactText = string.Empty,
                        FillAmount = 0
                    };
                }

                AdventureSkillState skill = _model.Skills[index];
                AdventureGaugeScreenModel gauge = CooldownGauge(skill.Definition.Name + SkillLevelSeparator + skill.Level,
                    skill.RemainingCooldown, skill.Definition.Cooldown, fighting);
                gauge.IconId = skill.Definition.Id;
                gauge.DisplayName = skill.Definition.Name;
                gauge.Description = SkillCooldownPrefix + skill.Definition.Cooldown.ToString(SecondsFormat) + SecondsSuffix + NewLine +
                    string.Join(EffectListSeparator, skill.Definition.Effects.Select(effect =>
                        (effect.Type == AdventureEffectType.Damage ? DamageEffectPrefix : HealingEffectPrefix) +
                        (effect.Amount + effect.PerLevel * (skill.Level - 1))));
                return gauge;
            }).ToArray();
            AdventureGaugeScreenModel localHealth = HealthGauge(_model.Health, _model.MaximumHealth);
            AdventureGaugeScreenModel localAttack = CooldownGauge(BasicAttackTitlePrefix + _model.BasicAttackDamage,
                _model.RemainingBasicAttackCooldown, _model.Configuration.BasicAttack.Cooldown, fighting);
            AdventureCharacterScreenModel localPlayer = new AdventureCharacterScreenModel
            {
                Id = LocalPlayerId,
                Name = LocalPlayerName,
                IsLocalPlayer = true,
                IsDefeated = _model.Health == 0,
                Health = localHealth,
                BasicAttack = localAttack,
                Skills = slots
            };
            AdventureCharacterScreenModel[] enemies = fighting
                ? _model.Enemies.Select((enemy, index) => new AdventureCharacterScreenModel
                {
                    Id = EnemyIdPrefix + index,
                    Name = enemy.Definition.Name,
                    IsDefeated = enemy.Health == 0,
                    Health = HealthGauge(enemy.Health, enemy.Definition.Health),
                    BasicAttack = CooldownGauge(BasicAttackTitlePrefix + enemy.Definition.Damage,
                        enemy.RemainingCooldown, enemy.Definition.Cooldown, enemy.Health > 0, enemy.Health == 0),
                    Skills = Array.Empty<AdventureGaugeScreenModel>()
                }).ToArray() : Array.Empty<AdventureCharacterScreenModel>();
            bool showMap = _mapOpen || _model.Phase == AdventurePhase.Map;
            AdventureCharacterScreenModel inspected = fighting && !showMap
                ? enemies.FirstOrDefault(enemy => enemy.Id == _inspectedCharacterId) ?? localPlayer : localPlayer;
            _inspectedCharacterId = inspected.Id;
            _view.Render(new AdventureScreenModel
            {
                Heading = showMap ? MapHeading : Heading(),
                Summary = LocalPlayerName + TitleSeparator + localHealth.Text + SummaryGoldPrefix + _model.Gold +
                    SummaryFloorPrefix + Math.Min(_model.FloorIndex + 1, _model.Configuration.Floors.Length) +
                    CountSeparator + _model.Configuration.Floors.Length + SummaryFloorSuffix,
                Situation = Situation(),
                Journal = string.Join(NewLine, _model.Journal),
                Choices = Choices(),
                PlayerHealth = localHealth,
                BasicAttack = localAttack,
                Skills = slots,
                Players = new[] { localPlayer },
                Enemies = enemies,
                ShowBattle = fighting,
                ShowMap = showMap,
                CanToggleMap = _model.Phase != AdventurePhase.Ready && _model.Phase != AdventurePhase.Map,
                MapButtonText = showMap && _model.Phase != AdventurePhase.Map ? ReturnToAdventureText : MapButtonText,
                MapRows = showMap ? MapRows() : Array.Empty<AdventureMapRowScreenModel>(),
                MapCurrentRow = _model.Phase == AdventurePhase.Complete
                    ? _model.GetFloorRows(_model.Configuration.Floors.Length - 1).Count - 1 : _model.RowIndex,
                MapNotice = _model.Phase == AdventurePhase.Map ? MapSelectionNotice
                    : fighting ? BattleMapNotice
                    : ResultMapNotice,
                InspectedCharacterId = inspected.Id,
                InspectedCharacterName = inspected.Name,
                InspectedSkills = new[] { inspected.BasicAttack }.Concat(inspected.Skills).ToArray(),
                Relics = _model.Relics.Select(relic => new AdventureIconScreenModel
                {
                    Id = relic.Id,
                    Title = relic.Name,
                    Description = string.Join(EffectListSeparator, relic.Effects.Select(effect =>
                        (effect.Type == AdventureEffectType.MaximumHealth ? MaximumHealthBonusPrefix : DamageBonusPrefix) + effect.Amount))
                }).ToArray(),
                CanStart = ended,
                CanAbandon = !ended
            });
        }

        private static AdventureGaugeScreenModel HealthGauge(int current, int maximum)
        {
            return new AdventureGaugeScreenModel
            {
                Title = HealthTitle,
                Text = HealthValuePrefix + current + CountSeparator + maximum,
                FillAmount = maximum > 0 ? Math.Max(0, Math.Min(1, (float)current / maximum)) : 0
            };
        }

        private static AdventureGaugeScreenModel CooldownGauge(string title, float remaining,
            float duration, bool active, bool defeated = false)
        {
            float seconds = Math.Max(0, remaining);
            return new AdventureGaugeScreenModel
            {
                Title = title,
                IconId = AttackIconId,
                DisplayName = BasicAttackName,
                Description = title + NewLine + SkillCooldownPrefix + duration.ToString(SecondsFormat) + SecondsSuffix,
                CompactText = defeated ? CompactDefeatedText : active
                    ? seconds.ToString(SecondsFormat) + CompactSecondsSuffix : CompactIdleText,
                Text = defeated ? DefeatedText : active ? CooldownTextPrefix + seconds.ToString(SecondsFormat) + SecondsSuffix
                    : IdleCooldownTextPrefix + duration.ToString(SecondsFormat) + SecondsSuffix,
                FillAmount = active && duration > 0
                    ? Math.Max(0, Math.Min(1, 1 - seconds / duration)) : 0
            };
        }

        private string Heading()
        {
            switch (_model.Phase)
            {
                case AdventurePhase.Ready: return ReadyHeading;
                case AdventurePhase.Map: return NextNodeHeading;
                case AdventurePhase.Battle: return BattleHeading;
                case AdventurePhase.SkillReward: return SkillRewardHeading;
                case AdventurePhase.NodeChoice: return _model.CurrentNode.Name;
                case AdventurePhase.Complete: return CompleteHeading;
                default: return FinishedHeading;
            }
        }

        private AdventureMapRowScreenModel[] MapRows()
        {
            int floor = Math.Min(_model.FloorIndex, _model.Configuration.Floors.Length - 1);
            System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<NodeDefinition>> rows =
                _model.GetFloorRows(floor);
            System.Collections.Generic.IReadOnlyList<int> path = _model.GetFloorPath(floor);
            return rows.Select((nodes, row) => new AdventureMapRowScreenModel
            {
                Status = _model.Phase == AdventurePhase.Complete || row < _model.RowIndex ? PassedRowText
                    : row == _model.RowIndex ? CurrentRowText : UnreachedNodeText,
                Nodes = nodes.Select((node, index) => new AdventureMapNodeScreenModel
                {
                    Title = node.Name + NewLine + NodeLabel(node.Type),
                    CanSelect = _model.Phase == AdventurePhase.Map && row == _model.RowIndex,
                    IsChosen = row < path.Count && path[row] == index,
                    StateText = row < path.Count ? (path[row] == index ? ChosenNodeText : SkippedNodeText)
                        : row == _model.RowIndex && _model.Phase == AdventurePhase.Map ? SelectableNodeText : UnreachedNodeText,
                    ChoiceIndex = index
                }).ToArray()
            }).ToArray();
        }

        private string Situation()
        {
            if (_model.Phase == AdventurePhase.Battle)
            {
                return BattleInstructions;
            }

            if (_model.Phase == AdventurePhase.Complete || _model.Phase == AdventurePhase.Failed)
            {
                return ResultGoldPrefix + _model.Gold + ResultGoldSuffix +
                    ResultSkillsPrefix + _model.Skills.Count + ResultRelicsSeparator + _model.Relics.Count + ResultCountSuffix +
                    ResultPersistenceNotice;
            }

            if (_model.Phase == AdventurePhase.SkillReward)
            {
                return SkillRewardInstructions;
            }

            return _model.Phase == AdventurePhase.Ready
                ? ReadyInstructions
                : NodeChoiceInstructions;
        }

        private string[] Choices()
        {
            switch (_model.Phase)
            {
                case AdventurePhase.Map:
                    return _model.AvailableNodes.Select(node => node.Name + TitleSeparator + NodeLabel(node.Type)).ToArray();
                case AdventurePhase.SkillReward:
                    return _model.Rewards.Select(definition =>
                    {
                        AdventureSkillState owned = _model.Skills.FirstOrDefault(skill => skill.Definition.Id == definition.Id);
                        int level = owned == null ? 1 : owned.Level + 1;
                        return (owned == null ? AcquireSkillPrefix : UpgradeSkillPrefix) + definition.Name + SkillLevelSeparator + level + TitleSeparator +
                            definition.Cooldown.ToString(SecondsFormat) + RewardCooldownSuffix + string.Join(EffectListSeparator, definition.Effects.Select(effect =>
                                (effect.Type == AdventureEffectType.Damage ? DamageEffectPrefix : HealingEffectPrefix) +
                                (effect.Amount + effect.PerLevel * (level - 1))));
                    }).ToArray();
                case AdventurePhase.NodeChoice:
                    NodeDefinition node = _model.CurrentNode;
                    if (node.Type == AdventureNodeType.Chest)
                    {
                        return new[] { ChestChoiceText };
                    }

                    return node.Type == AdventureNodeType.Camp
                        ? new[] { CampRestChoicePrefix + node.Healing + HealthSuffix, LeaveNodeChoiceText }
                        : new[] { EventSacrificeChoicePrefix + node.HealthCost + EventGoldRewardSeparator + node.Gold + GoldSuffix,
                            EventRecoveryChoicePrefix + node.Healing + HealthSuffix };
                default:
                    return Array.Empty<string>();
            }
        }

        private static string NodeLabel(AdventureNodeType type)
        {
            switch (type)
            {
                case AdventureNodeType.Battle: return BattleNodeLabel;
                case AdventureNodeType.Elite: return EliteNodeLabel;
                case AdventureNodeType.Boss: return BossNodeLabel;
                case AdventureNodeType.Chest: return ChestNodeLabel;
                case AdventureNodeType.Camp: return CampNodeLabel;
                default: return EventNodeLabel;
            }
        }
    }
}
