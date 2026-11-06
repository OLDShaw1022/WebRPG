using System;

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
        private const string EnemyIdPrefix = "enemy:";
        private readonly AdventureModel _model;
        private readonly AdventureScreenAssembler _screenAssembler;
        private readonly AdventureOperationHelper _operations;
        private readonly IAdventureView _view;
        private readonly MultiplayerAdventureSession _network;
        private readonly AdventurePresentationHelper _presentation;
        private bool _disposed;
        private bool _paused;
        private bool _mapOpen;


        public AdventurePresenter(AdventureModel model, IAdventureView view, MultiplayerAdventureSession network = null)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _network = network;
            _presentation = new AdventurePresentationHelper(network == null
                ? model.Configuration.SimulationStep : MultiplayerAdventureSession.PacketInterval);
            _screenAssembler = new AdventureScreenAssembler(_model, network);
            _operations = new AdventureOperationHelper(_model, network);
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
            _operations.Tick(seconds, _paused);
            Render(seconds);
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
            if (!_operations.Start()) return;
            _mapOpen = false;
            Refresh();
        }


        private void HandleAbandon()
        {
            if (!_operations.Abandon()) return;
            _mapOpen = false;
            Refresh();
        }


        private void HandleChoice(int index)
        {
            AdventurePhase previousPhase = _model.Phase;
            _operations.Choose(index);
            if (_operations.IsMultiplayer) return;
            if (previousPhase == AdventurePhase.Map && _model.Phase != AdventurePhase.Map) _mapOpen = false;
            Refresh();
        }


        private void HandleCharacterFocus(string characterId)
        {
            if (characterId != null && characterId.StartsWith(EnemyIdPrefix, StringComparison.Ordinal) &&
                int.TryParse(characterId.Substring(EnemyIdPrefix.Length), out int index))
            {
                _operations.Target(index);
            }
            Refresh();
        }

        private void HandleSkill(int index)
        {
            if (!_paused)
            {
                _operations.UseSkill(index);
            }
            Refresh();
        }

        private void HandleBattleSpeed(float speed)
        {
            _operations.SetBattleSpeed(speed);
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
            Render(0);
        }

        private void Render(float seconds)
        {
            if (_disposed) return;
            AdventureScreenModel screen = _screenAssembler.Build(_paused, _mapOpen);
            float presentationSeconds = _network == null && _paused ? 0 : seconds * screen.BattleSpeed;
            _presentation.Apply(screen, presentationSeconds);
            _view.Render(screen);
        }
    }
}
