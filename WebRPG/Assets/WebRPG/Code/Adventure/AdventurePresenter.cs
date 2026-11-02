using System;
using System.Linq;
using WebRPGShared;

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
        public event Action<int> SkillRequested;
        public event Action<float> BattleSpeedRequested;
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
        public float BattleSpeed { get; set; } = 1;
        public AdventureProjectileScreenModel Projectile { get; set; }
        public AdventureIconScreenModel[] Relics { get; set; } = Array.Empty<AdventureIconScreenModel>();
        public bool ShowResultActions { get; set; }
        public bool CanStart { get; set; }
        public bool CanAbandon { get; set; }
        public bool CanChoose { get; set; } = true;
        public bool CanChangeBattleSpeed { get; set; } = true;
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
        public bool CanUse { get; set; }
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
        public bool IsLocked { get; set; }
        public AdventureGaugeScreenModel Action { get; set; } = new AdventureGaugeScreenModel();
        public string ActionDescription { get; set; } = string.Empty;
        public string SpecialIntent { get; set; } = string.Empty;
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
        private const string LocalPlayerName = "本機玩家";
        private const string EnemyIdPrefix = "enemy:";
        private const string MapHeading = "挑戰地圖";
        private const string CountSeparator = " / ";
        private const string ReturnToAdventureText = "返回挑戰";
        private const string MapButtonText = "地圖";
        private const string MapSelectionNotice = "選擇亮框節點前往；路線由下往上推進。";
        private const string BattleMapNotice = "檢視地圖時戰鬥仍持續進行，節點僅供查看。";
        private const string ResultMapNotice = "地圖檢視中；返回挑戰繼續領獎或查看結算。";
        private const string EmptyIconId = "empty";
        private const string EmptySlotName = "空槽";
        private const string HealthTitle = "生命";
        private const string HealthValuePrefix = "生命 ";
        private const string BattleNodeLabel = "普通戰鬥";
        private const string EliteNodeLabel = "精英戰鬥";
        private const string BossNodeLabel = "Boss 戰";
        private const string ChestNodeLabel = "遺物";
        private const string CampNodeLabel = "營火";
        private const string EventNodeLabel = "事件";

        private readonly AdventureModel _model;
        private readonly AdventureScreenContentHelper _content;
        private readonly IAdventureView _view;
        private readonly MultiplayerAdventureSession _network;
        private const string LocalPlayerId = "local-player";
        private bool _disposed;
        private bool _paused;
        private bool _mapOpen;


        public AdventurePresenter(AdventureModel model, IAdventureView view, MultiplayerAdventureSession network = null)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _network = network;
            _content = new AdventureScreenContentHelper(_model);
            _view.StartRequested += HandleStart;
            _view.AbandonRequested += HandleAbandon;
            _view.ChoiceRequested += HandleChoice;
            _view.CharacterFocusRequested += HandleCharacterFocus;
            _view.MapToggleRequested += HandleMapToggle;
            _view.SkillRequested += HandleSkill;
            _view.BattleSpeedRequested += HandleBattleSpeed;
            if (_network != null)
            {
                _network.Changed += Refresh;
            }
            Refresh();
        }

        public void Tick(float seconds)
        {
            if (_disposed) return;
            if (_network != null) _network.Tick(seconds);
            else if (!_paused && _model.Phase == AdventurePhase.Battle) _model.Tick(seconds);
            Refresh();
        }

        public void SetPaused(bool paused)
        {
            _paused = paused;
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
            _view.SkillRequested -= HandleSkill;
            _view.BattleSpeedRequested -= HandleBattleSpeed;
            if (_network != null)
            {
                _network.Changed -= Refresh;
            }
        }

        private void HandleStart()
        {
            if (_network != null) return;
            _mapOpen = false;
            _model.StartRun();
            Refresh();
        }

        private void HandleAbandon()
        {
            if (_network != null)
            {
                _network.SendCommand(AdventureCommand.Abandon);
                return;
            }
            _mapOpen = false;
            _model.AbandonRun();
            Refresh();
        }

        private void HandleChoice(int index)
        {
            if (_network != null)
            {
                switch (_model.Phase)
                {
                    case AdventurePhase.Map: _network.SendCommand(AdventureCommand.Vote, index); break;
                    case AdventurePhase.NodeChoice: _network.SendCommand(AdventureCommand.Resolve, index); break;
                    case AdventurePhase.SkillReward: _network.SendCommand(AdventureCommand.Reward, index); break;
                }
                return;
            }
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
            if (characterId != null && characterId.StartsWith(EnemyIdPrefix, StringComparison.Ordinal) &&
                int.TryParse(characterId.Substring(EnemyIdPrefix.Length), out int index))
            {
                if (_network == null) _model.LockEnemy(index);
                else _network.SendCommand(AdventureCommand.Target, index);
            }
            Refresh();
        }

        private void HandleSkill(int index)
        {
            if (!_paused)
            {
                if (_network == null) _model.TryUseSkill(index);
                else _network.SendCommand(AdventureCommand.Skill, index);
            }
            Refresh();
        }

        private void HandleBattleSpeed(float speed)
        {
            if (_network != null) return;
            _model.SetBattleSpeed(speed);
            Refresh();
        }

        private void HandleMapToggle()
        {
            if (_model.Phase == AdventurePhase.Ready || _model.Phase == AdventurePhase.Map)
            {
                return;
            }

            _mapOpen = !_mapOpen;
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

                bool canUse = fighting && _model.Health > 0 && !_paused && _model.PlayerAction.SkillIndex < 0 &&
                    _model.Skills[index].RemainingCooldown <= 0;
                return AdventureBattleScreenHelper.SkillGauge(_model.Skills[index], fighting,
                    _model.PlayerAction, index, canUse);
            }).ToArray();
            AdventureGaugeScreenModel localHealth = HealthGauge(_model.Health, _model.MaximumHealth);
            AdventureGaugeScreenModel localAttack = AdventureBattleScreenHelper.BasicAttackGauge(_model.BasicAttackDamage,
                _model.Configuration.BasicAttack.CastTime, _model.PlayerAction, fighting);
            AdventureCharacterScreenModel localPlayer = new AdventureCharacterScreenModel
            {
                Id = _network?.PlayerId ?? LocalPlayerId,
                Name = string.IsNullOrEmpty(_network?.LocalName) ? LocalPlayerName : _network.LocalName,
                IsLocalPlayer = true,
                IsDefeated = _model.Health == 0 || _model.Phase == AdventurePhase.Failed,
                Health = localHealth,
                Action = AdventureBattleScreenHelper.CastGauge(_model.PlayerAction, _model.Health == 0),
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
                    IsLocked = index == _model.LockedEnemyIndex,
                    Action = AdventureBattleScreenHelper.CastGauge(enemy.Action, enemy.Health == 0),
                    ActionDescription = AdventureBattleScreenHelper.EnemyAction(enemy, _model.Projectile, index),
                    SpecialIntent = AdventureBattleScreenHelper.EnemyIntent(enemy),
                    BasicAttack = AdventureBattleScreenHelper.BasicAttackGauge(enemy.Definition.Damage,
                        enemy.Definition.CastTime, enemy.Action, enemy.Health > 0, enemy.Health == 0),
                    Skills = Array.Empty<AdventureGaugeScreenModel>()
                }).ToArray() : Array.Empty<AdventureCharacterScreenModel>();
            bool showMap = _mapOpen || _model.Phase == AdventurePhase.Map;
            _content.Refresh(showMap);
            AdventureMapRowScreenModel[] mapRows = _network == null ? _content.Rows : showMap ? _content.Rows.Select(row => new AdventureMapRowScreenModel
            {
                Status = row.Status,
                Nodes = row.Nodes.Select(node => new AdventureMapNodeScreenModel
                {
                    Title = node.Title,
                    StateText = node.StateText,
                    CanSelect = node.CanSelect && (_network == null ||
                        _network.CanVote),
                    IsChosen = node.IsChosen,
                    ChoiceIndex = node.ChoiceIndex
                }).ToArray()
            }).ToArray() : Array.Empty<AdventureMapRowScreenModel>();
            _view.Render(new AdventureScreenModel
            {
                Heading = showMap ? MapHeading : _content.HeadingText,
                Summary = _content.SummaryText,
                Situation = _network == null ? _content.SituationText : _network.Status,
                Journal = _content.JournalText,
                Choices = _content.ChoiceLabels,
                PlayerHealth = localHealth,
                BasicAttack = localAttack,
                Skills = slots,
                Players = _network?.GetPartyCharacters(localPlayer) ?? new[] { localPlayer },
                Enemies = enemies,
                ShowBattle = fighting,
                BattleSpeed = _model.BattleSpeed,
                Projectile = fighting && _model.Projectile != null ? new AdventureProjectileScreenModel
                {
                    SourceCharacterId = _model.Projectile.SourceEnemyIndex < 0 ? (_network?.ProjectileSourceId ?? localPlayer.Id)
                        : EnemyIdPrefix + _model.Projectile.SourceEnemyIndex,
                    TargetCharacterId = _model.Projectile.TargetEnemyIndex < 0 ? (_network?.ProjectileTargetId ?? localPlayer.Id)
                        : EnemyIdPrefix + _model.Projectile.TargetEnemyIndex,
                    Name = _model.Projectile.Name,
                    Progress = 1 - _model.Projectile.RemainingTime / _model.Projectile.Duration
                } : null,
                ShowMap = showMap,
                CanToggleMap = _model.Phase != AdventurePhase.Ready && _model.Phase != AdventurePhase.Map,
                MapButtonText = showMap && _model.Phase != AdventurePhase.Map ? ReturnToAdventureText : MapButtonText,
                MapRows = mapRows,
                MapCurrentRow = _model.Phase == AdventurePhase.Complete
                    ? _model.GetFloorRows(_model.Configuration.Floors.Length - 1).Count - 1 : _model.RowIndex,
                MapNotice = _network != null && _model.Phase == AdventurePhase.Map ? _network.Status
                    : _model.Phase == AdventurePhase.Map ? MapSelectionNotice
                    : fighting ? BattleMapNotice
                    : ResultMapNotice,
                InspectedCharacterId = localPlayer.Id,
                InspectedCharacterName = localPlayer.Name,
                InspectedSkills = new[] { localPlayer.BasicAttack }.Concat(slots).ToArray(),
                Relics = _content.RelicIcons,
                ShowResultActions = (_model.Phase == AdventurePhase.Complete && (_network == null || _network.IsComplete)) ||
                    _model.Phase == AdventurePhase.Failed,
                CanStart = ended && _network == null,
                CanAbandon = !ended,
                CanChoose = _network == null || _model.Phase == AdventurePhase.SkillReward ||
                    _model.Phase == AdventurePhase.NodeChoice || _network.CanVote,
                CanChangeBattleSpeed = _network == null
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
