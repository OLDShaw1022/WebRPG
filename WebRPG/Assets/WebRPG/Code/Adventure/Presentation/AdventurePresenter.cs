using System;

namespace WebRPG
{
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
