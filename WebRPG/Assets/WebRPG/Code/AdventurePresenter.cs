using System;
using System.Linq;

namespace WebRPG
{
    public interface IAdventureView
    {
        public event Action StartRequested;
        public event Action AbandonRequested;
        public event Action<int> ChoiceRequested;
        public event Action<string> CharacterFocusRequested;
        public event Action MapToggleRequested;
        public void Render(AdventureScreenModel screen);
    }

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
        public string Relics { get; set; }
        public bool CanStart { get; set; }
        public bool CanAbandon { get; set; }
    }

    public sealed class AdventureGaugeScreenModel
    {
        public string Title { get; set; }
        public string Text { get; set; }
        public float FillAmount { get; set; }
    }

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

    public sealed class AdventureMapRowScreenModel
    {
        public string Status { get; set; }
        public AdventureMapNodeScreenModel[] Nodes { get; set; }
    }

    public sealed class AdventureMapNodeScreenModel
    {
        public string Title { get; set; }
        public string StateText { get; set; }
        public bool CanSelect { get; set; }
        public bool IsChosen { get; set; }
        public int ChoiceIndex { get; set; }
    }

    public sealed class AdventurePresenter : IDisposable
    {
        private readonly AdventureModel _model;
        private readonly IAdventureView _view;
        private const string LocalPlayerId = "local-player";
        private bool _disposed;
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
                        Title = "技能槽 " + (index + 1),
                        Text = "尚未裝備",
                        FillAmount = 0
                    };
                }

                AdventureSkillState skill = _model.Skills[index];
                return CooldownGauge(skill.Definition.Name + " Lv." + skill.Level,
                    skill.RemainingCooldown, skill.Definition.Cooldown, fighting);
            }).ToArray();
            AdventureGaugeScreenModel localHealth = HealthGauge(_model.Health, _model.MaximumHealth);
            AdventureGaugeScreenModel localAttack = CooldownGauge("普攻 · 傷害 " + _model.BasicAttackDamage,
                _model.RemainingBasicAttackCooldown, _model.Configuration.BasicAttack.Cooldown, fighting);
            AdventureCharacterScreenModel localPlayer = new AdventureCharacterScreenModel
            {
                Id = LocalPlayerId,
                Name = "本機玩家",
                IsLocalPlayer = true,
                IsDefeated = _model.Health == 0,
                Health = localHealth,
                BasicAttack = localAttack,
                Skills = slots
            };
            AdventureCharacterScreenModel[] enemies = fighting
                ? _model.Enemies.Select((enemy, index) => new AdventureCharacterScreenModel
                {
                    Id = "enemy:" + index,
                    Name = enemy.Definition.Name,
                    IsDefeated = enemy.Health == 0,
                    Health = HealthGauge(enemy.Health, enemy.Definition.Health),
                    BasicAttack = CooldownGauge("普攻 · 傷害 " + enemy.Definition.Damage,
                        enemy.RemainingCooldown, enemy.Definition.Cooldown, enemy.Health > 0, enemy.Health == 0),
                    Skills = Array.Empty<AdventureGaugeScreenModel>()
                }).ToArray() : Array.Empty<AdventureCharacterScreenModel>();
            bool showMap = _mapOpen || _model.Phase == AdventurePhase.Map;
            AdventureCharacterScreenModel inspected = fighting && !showMap
                ? enemies.FirstOrDefault(enemy => enemy.Id == _inspectedCharacterId) ?? localPlayer : localPlayer;
            _inspectedCharacterId = inspected.Id;
            _view.Render(new AdventureScreenModel
            {
                Heading = showMap ? "挑戰地圖" : Heading(),
                Summary = "本機玩家 · 第 " + Math.Min(_model.FloorIndex + 1, _model.Configuration.Floors.Length) +
                    " / " + _model.Configuration.Floors.Length + " 層 · 金幣 " + _model.Gold +
                    " · 攻擊 " + _model.BasicAttackDamage + " · 技能 " + _model.Skills.Count +
                    "/6 · 遺物 " + _model.Relics.Count,
                Situation = Situation(),
                Journal = string.Join("\n", _model.Journal.TakeLast(6)),
                Choices = Choices(),
                PlayerHealth = localHealth,
                BasicAttack = localAttack,
                Skills = slots,
                Players = new[] { localPlayer },
                Enemies = enemies,
                ShowBattle = fighting,
                ShowMap = showMap,
                CanToggleMap = _model.Phase != AdventurePhase.Ready && _model.Phase != AdventurePhase.Map,
                MapButtonText = showMap && _model.Phase != AdventurePhase.Map ? "返回挑戰" : "地圖",
                MapRows = showMap ? MapRows() : Array.Empty<AdventureMapRowScreenModel>(),
                MapCurrentRow = _model.Phase == AdventurePhase.Complete
                    ? _model.GetFloorRows(_model.Configuration.Floors.Length - 1).Count - 1 : _model.RowIndex,
                MapNotice = _model.Phase == AdventurePhase.Map ? "選擇亮框節點前往；路線由下往上推進。"
                    : fighting ? "檢視地圖時戰鬥仍持續進行，節點僅供查看。"
                    : "地圖檢視中；返回挑戰繼續領獎或查看結算。",
                InspectedCharacterId = inspected.Id,
                InspectedCharacterName = inspected.Name,
                InspectedSkills = new[] { inspected.BasicAttack }.Concat(inspected.Skills).ToArray(),
                Relics = _model.Relics.Count == 0 ? "尚未取得" :
                    string.Join("\n\n", _model.Relics.Select(relic => relic.Name + "\n" +
                        string.Join("、", relic.Effects.Select(effect =>
                            (effect.Type == AdventureEffectType.MaximumHealth ? "生命上限+" : "傷害+") + effect.Amount)))),
                CanStart = ended,
                CanAbandon = !ended
            });
        }

        private static AdventureGaugeScreenModel HealthGauge(int current, int maximum)
        {
            return new AdventureGaugeScreenModel
            {
                Title = "生命",
                Text = "生命 " + current + " / " + maximum,
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
                Text = defeated ? "已擊倒" : active ? "倒數 " + seconds.ToString("0.0") + " 秒"
                    : "待戰 · " + duration.ToString("0.0") + " 秒",
                FillAmount = active && duration > 0
                    ? Math.Max(0, Math.Min(1, 1 - seconds / duration)) : 0
            };
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

        private AdventureMapRowScreenModel[] MapRows()
        {
            int floor = Math.Min(_model.FloorIndex, _model.Configuration.Floors.Length - 1);
            System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<NodeDefinition>> rows =
                _model.GetFloorRows(floor);
            System.Collections.Generic.IReadOnlyList<int> path = _model.GetFloorPath(floor);
            return rows.Select((nodes, row) => new AdventureMapRowScreenModel
            {
                Status = _model.Phase == AdventurePhase.Complete || row < _model.RowIndex ? "已通過"
                    : row == _model.RowIndex ? "目前所在" : "未到達",
                Nodes = nodes.Select((node, index) => new AdventureMapNodeScreenModel
                {
                    Title = node.Name + "\n" + NodeLabel(node.Type),
                    CanSelect = _model.Phase == AdventurePhase.Map && row == _model.RowIndex,
                    IsChosen = row < path.Count && path[row] == index,
                    StateText = row < path.Count ? (path[row] == index ? "已選擇" : "已略過")
                        : row == _model.RowIndex && _model.Phase == AdventurePhase.Map ? "可前往" : "未到達",
                    ChoiceIndex = index
                }).ToArray()
            }).ToArray();
        }

        private string Situation()
        {
            if (_model.Phase == AdventurePhase.Battle)
            {
                return "普攻與技能自動施放，依序攻擊第一個存活敵人。";
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
