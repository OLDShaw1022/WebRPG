using System;
using System.Linq;

namespace WebRPG
{
    // 快取僅在挑戰內容變更時更新的文字、選項、遺物與地圖畫面資料。
    internal sealed class AdventureScreenContentHelper
    {
        private const string SkillLevelSeparator = " Lv.";
        private const string LocalPlayerName = "本機玩家";
        private const string CountSeparator = " / ";
        private const string SummaryGoldPrefix = " · 金幣 ";
        private const string SummaryFloorPrefix = " · 第 ";
        private const string SummaryFloorSuffix = " 層";
        private const string NewLine = "\n";
        private const string EffectListSeparator = "、";
        private const string MaximumHealthBonusPrefix = "生命上限+";
        private const string DamageBonusPrefix = "傷害+";
        private const string HealthValuePrefix = "生命 ";
        private const string SecondsFormat = "0.0";
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
        private const string BattleInstructions = "點擊敵人鎖定；點擊技能施放，完成後自動恢復普攻。";
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
        private int _runVersion = -1;
        private int _journalVersion = -1;
        private AdventurePhase _phase;
        private NodeDefinition _node;
        private int _floorIndex = -1;
        private int _rowIndex = -1;
        private int _health = -1;
        private int _maximumHealth = -1;
        private int _gold = -1;
        private int _relicCount = -1;
        private int _mapRunVersion = -1;
        private int _mapFloorIndex = -1;
        private int _mapRowIndex = -1;
        private AdventurePhase _mapPhase;

        public AdventureScreenContentHelper(AdventureModel model)
        {
            _model = model;
        }

        public string HeadingText { get; private set; }
        public string SummaryText { get; private set; }
        public string SituationText { get; private set; }
        public string JournalText { get; private set; }
        public string[] ChoiceLabels { get; private set; }
        public AdventureMapRowScreenModel[] Rows { get; private set; } = Array.Empty<AdventureMapRowScreenModel>();
        public AdventureIconScreenModel[] RelicIcons { get; private set; }

        public void Refresh(bool showMap)
        {
            bool runChanged = _runVersion != _model.RunVersion;
            bool phaseChanged = runChanged || _phase != _model.Phase || _node != _model.CurrentNode ||
                _floorIndex != _model.FloorIndex || _rowIndex != _model.RowIndex;
            if (phaseChanged)
            {
                HeadingText = Heading();
                SituationText = Situation();
                ChoiceLabels = Choices();
            }
            if (phaseChanged || _health != _model.Health || _maximumHealth != _model.MaximumHealth
                || _gold != _model.Gold)
            {
                SummaryText = LocalPlayerName + TitleSeparator + HealthValuePrefix + _model.Health +
                    CountSeparator + _model.MaximumHealth + SummaryGoldPrefix + _model.Gold +
                    SummaryFloorPrefix + Math.Min(_model.FloorIndex + 1, _model.Configuration.Floors.Length) +
                    CountSeparator + _model.Configuration.Floors.Length + SummaryFloorSuffix;
            }
            if (_journalVersion != _model.JournalVersion)
            {
                JournalText = string.Join(NewLine, _model.Journal);
                _journalVersion = _model.JournalVersion;
            }
            if (runChanged || _relicCount != _model.Relics.Count)
            {
                RelicIcons = _model.Relics.Select(relic => new AdventureIconScreenModel
                {
                    Id = relic.Id,
                    Title = relic.Name,
                    Description = string.Join(EffectListSeparator, relic.Effects.Select(effect =>
                        (effect.Type == AdventureEffectType.MaximumHealth ? MaximumHealthBonusPrefix
                            : DamageBonusPrefix) + effect.Amount))
                }).ToArray();
                _relicCount = _model.Relics.Count;
            }
            if (showMap && (_mapRunVersion != _model.RunVersion || _mapFloorIndex != _model.FloorIndex ||
                _mapRowIndex != _model.RowIndex || _mapPhase != _model.Phase))
            {
                Rows = MapRows();
                _mapRunVersion = _model.RunVersion;
                _mapFloorIndex = _model.FloorIndex;
                _mapRowIndex = _model.RowIndex;
                _mapPhase = _model.Phase;
            }
            _runVersion = _model.RunVersion;
            _phase = _model.Phase;
            _node = _model.CurrentNode;
            _floorIndex = _model.FloorIndex;
            _rowIndex = _model.RowIndex;
            _health = _model.Health;
            _maximumHealth = _model.MaximumHealth;
            _gold = _model.Gold;
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
                        : row == _model.RowIndex && _model.Phase == AdventurePhase.Map ? SelectableNodeText
                            : UnreachedNodeText,
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
                    ResultSkillsPrefix + _model.Skills.Count + ResultRelicsSeparator + _model.Relics.Count +
                        ResultCountSuffix +
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
                    return _model.AvailableNodes.Select(node => node.Name + TitleSeparator + NodeLabel(node.Type))
                        .ToArray();
                case AdventurePhase.SkillReward:
                    return _model.Rewards.Select(definition =>
                    {
                        AdventureSkillState owned = _model.Skills.FirstOrDefault(
                            skill => skill.Definition.Id == definition.Id);
                        int level = owned == null ? 1 : owned.Level + 1;
                        return (
                            owned == null ? AcquireSkillPrefix
                            : UpgradeSkillPrefix) + definition.Name + SkillLevelSeparator + level + TitleSeparator +
                            definition.Cooldown.ToString(SecondsFormat) + RewardCooldownSuffix + string.Join(
                                EffectListSeparator, definition.Effects.Select(effect =>
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
                        : new[] {
                            EventSacrificeChoicePrefix + node.HealthCost + EventGoldRewardSeparator + node.Gold +
                            GoldSuffix,
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
