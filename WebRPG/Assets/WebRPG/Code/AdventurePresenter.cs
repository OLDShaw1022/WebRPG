using System;
using System.Linq;
using System.Text;

namespace WebRPG
{
    public interface IAdventureView
    {
        public event Action StartRequested;
        public event Action AbandonRequested;
        public event Action<int> ChoiceRequested;
        public void Render(AdventureScreenModel screen);
    }

    public sealed class AdventureScreenModel
    {
        public string Heading { get; set; }
        public string Summary { get; set; }
        public string Route { get; set; }
        public string Situation { get; set; }
        public string Journal { get; set; }
        public string[] Choices { get; set; }
        public string[] Skills { get; set; }
        public string Relics { get; set; }
        public bool CanStart { get; set; }
        public bool CanAbandon { get; set; }
    }

    public sealed class AdventurePresenter : IDisposable
    {
        private readonly AdventureModel _model;
        private readonly IAdventureView _view;
        private bool _disposed;

        public AdventurePresenter(AdventureModel model, IAdventureView view)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _view.StartRequested += HandleStart;
            _view.AbandonRequested += HandleAbandon;
            _view.ChoiceRequested += HandleChoice;
            Refresh();
        }

        public void Tick(float seconds)
        {
            if (_disposed || _model.Phase != AdventurePhase.Battle)
            {
                return;
            }

            _model.Tick(seconds);
            Refresh();
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
        }

        private void HandleStart()
        {
            _model.StartRun();
            Refresh();
        }

        private void HandleAbandon()
        {
            _model.AbandonRun();
            Refresh();
        }

        private void HandleChoice(int index)
        {
            switch (_model.Phase)
            {
                case AdventurePhase.Map:
                    _model.SelectNode(index);
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

        private void Refresh()
        {
            bool ended = _model.Phase == AdventurePhase.Ready || _model.Phase == AdventurePhase.Complete ||
                _model.Phase == AdventurePhase.Failed;
            string[] slots = Enumerable.Range(0, _model.Configuration.SkillSlots).Select(index =>
            {
                if (index >= _model.Skills.Count)
                {
                    return (index + 1) + " · 空技能槽";
                }

                AdventureSkillState skill = _model.Skills[index];
                return skill.Definition.Name + " Lv." + skill.Level + "\n" +
                    (_model.Phase == AdventurePhase.Battle
                        ? Math.Max(0, skill.RemainingCooldown).ToString("0.0") + " 秒"
                        : "倒數 " + skill.Definition.Cooldown.ToString("0.0") + " 秒");
            }).ToArray();
            _view.Render(new AdventureScreenModel
            {
                Heading = Heading(),
                Summary = "生命 " + _model.Health + " / " + _model.MaximumHealth + "    本輪金幣 " + _model.Gold,
                Route = Route(),
                Situation = Situation(),
                Journal = string.Join("\n", _model.Journal.TakeLast(6)),
                Choices = Choices(),
                Skills = slots,
                Relics = "遺物：" + (_model.Relics.Count == 0 ? "尚未取得" :
                    string.Join("、", _model.Relics.Select(relic => relic.Name + "（" +
                        string.Join("／", relic.Effects.Select(effect =>
                            (effect.Type == AdventureEffectType.MaximumHealth ? "生命上限+" : "傷害+") + effect.Amount)) + "）"))),
                CanStart = ended,
                CanAbandon = !ended
            });
        }

        private string Heading()
        {
            switch (_model.Phase)
            {
                case AdventurePhase.Ready: return "灰燼之塔 · 單人試玩";
                case AdventurePhase.Map: return "選擇下一個節點";
                case AdventurePhase.Battle: return "自動戰鬥中";
                case AdventurePhase.SkillReward: return "戰利品 · 選擇一個技能";
                case AdventurePhase.NodeChoice: return _model.CurrentNode.Name;
                case AdventurePhase.Complete: return "挑戰成功";
                default: return "挑戰結束";
            }
        }

        private string Route()
        {
            if (_model.Phase == AdventurePhase.Ready)
            {
                return "三層分支路線，每層末尾為 Boss。\n\n戰鬥自動進行，勝利後選擇技能。\n血量跨戰鬥保留，營火可恢復。\n每次挑戰最多裝備六個技能。";
            }

            if (_model.Phase == AdventurePhase.Complete)
            {
                return "三層塔樓已全部完成。\n\n可開始新一輪挑戰。";
            }

            FloorDefinition floor = _model.Configuration.Floors[_model.FloorIndex];
            StringBuilder route = new StringBuilder();
            route.AppendLine("路線 · 可上下捲動");
            route.AppendLine("第 " + (_model.FloorIndex + 1) + " / 3 層 · " + floor.Name);
            for (int row = 0; row < _model.CurrentFloorRows.Count; row++)
            {
                string marker = row < _model.RowIndex ? "✓ " : row == _model.RowIndex ? "→ " : "· ";
                route.AppendLine(marker + string.Join(" ／ ", _model.CurrentFloorRows[row].Select(node => node.Name)));
                if (row < _model.CurrentFloorRows.Count - 1)
                {
                    route.AppendLine("    ↓ 任選下一列一路");
                }
            }

            return route.ToString();
        }

        private string Situation()
        {
            if (_model.Phase == AdventurePhase.Battle)
            {
                return "普攻倒數 " + Math.Max(0, _model.RemainingBasicAttackCooldown).ToString("0.0") +
                    " 秒 · 傷害 " + _model.Configuration.BasicAttack.Damage + "（另加遺物加成）\n" + string.Join("\n", _model.Enemies.Select(enemy =>
                    enemy.Definition.Name + "  生命 " + enemy.Health + " / " + enemy.Definition.Health +
                    (enemy.Health == 0 ? "  已擊倒" : "  攻擊倒數 " + Math.Max(0, enemy.RemainingCooldown).ToString("0.0") + " 秒")));
            }

            if (_model.Phase == AdventurePhase.Complete || _model.Phase == AdventurePhase.Failed)
            {
                return "本輪結算：" + _model.Gold + " 金幣\n" +
                    "技能 " + _model.Skills.Count + " 個 · 遺物 " + _model.Relics.Count + " 個\n" +
                    "單人原型：結算只供本輪檢視，不會保存至帳號。";
            }

            if (_model.Phase == AdventurePhase.SkillReward)
            {
                return "新技能自動裝入空槽；重複技能直接升級。\n六槽裝滿後，只會出現已裝備且未滿級的技能。";
            }

            return _model.Phase == AdventurePhase.Ready
                ? "按「開始挑戰」進入塔樓。\n可在任何進行中的階段放棄，該輪會以失敗結算。"
                : "選擇下方行動。寶箱提供被動遺物，事件可交換生命與金幣。";
        }

        private string[] Choices()
        {
            switch (_model.Phase)
            {
                case AdventurePhase.Map:
                    return _model.AvailableNodes.Select(node => node.Name + " · " + NodeLabel(node.Type)).ToArray();
                case AdventurePhase.SkillReward:
                    return _model.Rewards.Select(definition =>
                    {
                        AdventureSkillState owned = _model.Skills.FirstOrDefault(skill => skill.Definition.Id == definition.Id);
                        int level = owned == null ? 1 : owned.Level + 1;
                        return (owned == null ? "取得 " : "升級 ") + definition.Name + " Lv." + level + " · " +
                            definition.Cooldown.ToString("0.0") + " 秒\n" + string.Join("、", definition.Effects.Select(effect =>
                                (effect.Type == AdventureEffectType.Damage ? "傷害 " : "治療 ") +
                                (effect.Amount + effect.PerLevel * (level - 1))));
                    }).ToArray();
                case AdventurePhase.NodeChoice:
                    NodeDefinition node = _model.CurrentNode;
                    if (node.Type == AdventureNodeType.Chest)
                    {
                        return new[] { "開啟寶箱 · 隨機取得一個未持有的遺物" };
                    }

                    return node.Type == AdventureNodeType.Camp
                        ? new[] { "休息 · 恢復 " + node.Healing + " 生命", "直接離開" }
                        : new[] { "獻祭 " + node.HealthCost + " 生命 · 取得 " + node.Gold + " 金幣",
                            "休養 · 恢復 " + node.Healing + " 生命" };
                default:
                    return Array.Empty<string>();
            }
        }

        private static string NodeLabel(AdventureNodeType type)
        {
            switch (type)
            {
                case AdventureNodeType.Battle: return "普通戰鬥";
                case AdventureNodeType.Elite: return "精英戰鬥";
                case AdventureNodeType.Boss: return "Boss 戰";
                case AdventureNodeType.Chest: return "遺物";
                case AdventureNodeType.Camp: return "營火";
                default: return "事件";
            }
        }
    }
}
