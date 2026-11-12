using System;
using System.Linq;

namespace WebRPG
{
    // 組裝冒險畫面資料，保留內容快取並隔離呈現資料轉換。
    public sealed class AdventureScreenAssembler
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

        private const string LocalPlayerId = "local-player";
        private readonly AdventureModel _model;
        private readonly MultiplayerAdventureSession _network;
        private readonly AdventureScreenContentHelper _content;

        public AdventureScreenAssembler(AdventureModel model, MultiplayerAdventureSession network = null)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _network = network;
            _content = new AdventureScreenContentHelper(model);
        }

        public AdventureScreenModel Build(bool paused, bool mapOpen)
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

                bool canUse = fighting && _model.Health > 0 && !paused && _model.PlayerAction.SkillIndex < 0 &&
                    _model.Skills[index].RemainingCooldown <= 0;
                return AdventureBattleScreenHelper.SkillGauge(_model.Skills[index], fighting,
                    _model.PlayerAction, index, canUse);
            }).ToArray();
            AdventureGaugeScreenModel localHealth = HealthGauge(_model.Health, _model.MaximumHealth);
            AdventureGaugeScreenModel localAttack = AdventureBattleScreenHelper.BasicAttackGauge(
                _model.BasicAttackDamage,
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
            bool showMap = mapOpen || _model.Phase == AdventurePhase.Map;
            _content.Refresh(showMap);
            AdventureMapRowScreenModel[] mapRows = _network == null ? _content.Rows : showMap
                ? _content.Rows.Select(row => new AdventureMapRowScreenModel
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
            return new AdventureScreenModel
            {
                Heading = showMap ? MapHeading : _content.HeadingText,
                Summary = _content.SummaryText,
                Situation = _network == null ? _content.SituationText : _network.Status,
                Journal = _content.JournalText,
                Choices = _content.ChoiceLabels,
                PlayerHealth = localHealth,
                BasicAttack = localAttack,
                Skills = slots,
                Players = _network == null ? new[] { localPlayer }
                    : AdventurePartyScreenHelper.Characters(_network, localPlayer),
                Enemies = enemies,
                ShowBattle = fighting,
                BattleSpeed = _network?.BattleSpeed ?? _model.BattleSpeed,
                Projectile = fighting && _model.Projectile != null ? new AdventureProjectileScreenModel
                {
                    SourceCharacterId = _model.Projectile.SourceEnemyIndex < 0
                        ? (_network?.ProjectileSourceId ?? localPlayer.Id)
                        : EnemyIdPrefix + _model.Projectile.SourceEnemyIndex,
                    TargetCharacterId = _model.Projectile.TargetEnemyIndex < 0
                        ? (_network?.ProjectileTargetId ?? localPlayer.Id)
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
                ShowResultActions = (_model.Phase == AdventurePhase.Complete &&
                                     (_network == null || _network.IsComplete)) ||
                    _model.Phase == AdventurePhase.Failed,
                CanStart = ended && _network == null,
                CanAbandon = !ended,
                CanChoose = _network == null || _model.Phase == AdventurePhase.SkillReward ||
                    _model.Phase == AdventurePhase.NodeChoice || _network.CanVote,
                CanChangeBattleSpeed = _network == null || _network.IsLeader
            };
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

    }
}
