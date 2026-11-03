using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace WebRPG
{
    // 顯示冒險戰鬥、地圖與詳細資訊，並轉送使用者操作。
    [DisallowMultipleComponent]
    public sealed class AdventureView : MonoBehaviour, IAdventureView
    {
        private const string MissingPrefabReferencesMessage = "AdventureView requires complete prefab references.";
        private const string InspectedSkillsTitleSuffix = " · 技能狀態";
        private const string PlayerDefeatedText = "已倒下";
        private const string EnemyDefeatedText = "已擊倒";
        private const string LocalPlayerBadgeText = "本機";
        private const string TeammateBadgeText = "隊友";
        private const string EnemyBadgeText = "敵方";
        private const string LockedBadgeText = "鎖定";
        private const string PauseSpeedText = "暫停";
        private const string NormalSpeedText = "1×";
        private const string DoubleSpeedText = "2×";
        private const string SelectedSpeedFormat = "[{0}]";
        private const string MapNodeNamePrefix = "Node";
        private const string MapNodeIndexSeparator = "_";
        private const string NewLine = "\n";
        private const string JournalHeading = "戰鬥紀錄";
        private const string RelicNamePrefix = "Relic";

        [SerializeField]
        private RectTransform _root;


        [SerializeField]
        private AdventureSurfaceViewReferences _header;
        [SerializeField]
        private TextMeshProUGUI _noRelics;
        [SerializeField]
        private AdventureContentViewReferences _relicArea;
        [SerializeField]
        private AdventureIconItemView _relicTemplate;
        [SerializeField]
        private AdventureTooltipView _tooltip;
        [SerializeField]
        private AdventureMenuView _menu;
        [SerializeField]
        private TextMeshProUGUI _summary;
        [SerializeField]
        private AdventureButtonViewReferences[] _toolbar;
        [SerializeField]
        private RectTransform _arena;
        [SerializeField]
        private AdventureContentViewReferences _actorArea;
        [SerializeField]
        private TextMeshProUGUI _versus;
        [SerializeField]
        private AdventureActorViewReferences[] _players;
        [SerializeField]
        private AdventureActorViewReferences[] _enemies;
        [SerializeField]
        private AdventureSurfaceViewReferences _actionPanel;
        [SerializeField]
        private TextMeshProUGUI _situation;
        [SerializeField]
        private AdventureButtonViewReferences[] _choices;
        [SerializeField]
        private AdventureButtonViewReferences _resultRestart;
        [SerializeField]
        private AdventureButtonViewReferences _resultReturn;
        [SerializeField]
        private AdventureSurfaceViewReferences _skillPanel;
        [SerializeField]
        private TextMeshProUGUI _skillHeading;
        [SerializeField]
        private AdventureContentViewReferences _skillArea;
        [SerializeField]
        private AdventureSkillViewReferences[] _skills;
        [SerializeField]
        private AdventureSurfaceViewReferences _mapPanel;
        [SerializeField]
        private TextMeshProUGUI _mapNotice;
        [SerializeField]
        private AdventureScrollViewReferences _mapScroll;
        [SerializeField]
        private RectTransform _mapLines;
        [SerializeField]
        private RectTransform _mapNodes;
        private readonly List<List<AdventureMapNodeViewState>> _mapRows = new List<List<AdventureMapNodeViewState>>();
        private readonly List<AdventureSurfaceViewReferences> _connections = new List<AdventureSurfaceViewReferences>();
        [SerializeField]
        private AdventureSurfaceViewReferences _details;
        [SerializeField]
        private TextMeshProUGUI _detailsHeading;
        [SerializeField]
        private TextMeshProUGUI _detailsText;
        [SerializeField]
        private AdventureScrollViewReferences _detailsScroll;
        [SerializeField]
        private AdventureButtonTemplateView _mapNodeTemplate;

        [SerializeField]
        private AdventureSurfaceTemplateView _mapConnectionTemplate;

        [SerializeField]
        private AdventureDynamicLayoutSettings _layoutSettings = new AdventureDynamicLayoutSettings();

        [SerializeField]
        private Color _foreground = new Color32(255, 255, 255, 255);

        [SerializeField]
        private Color _secondary = new Color32(179, 184, 194, 255);

        [SerializeField]
        private Color _border = new Color32(115, 115, 115, 255);

        [SerializeField]
        private Color _controlHighlight = new Color32(61, 69, 82, 255);

        [SerializeField]
        private Color _insetBackground = new Color32(18, 20, 26, 255);

        [SerializeField] private AdventureButtonViewReferences[] _battleSpeedControls;
        [SerializeField] private RectTransform _projectile;
        [SerializeField] private TextMeshProUGUI _projectileLabel;
        [SerializeField] private Color _targetOutlineColor = new Color32(224, 91, 101, 255);
        private bool _bound;
        private AdventureDynamicLayoutHelper _layout;

        private AdventureScreenModel _screen;
        private Vector2 _lastSize = new Vector2(-1, -1);
        private readonly List<AdventureIconItemView> _relicIcons = new List<AdventureIconItemView>();
        private int _lastMapRow = -1;
        private bool _layoutDirty = true;

        public AdventureMenuView Menu => _menu;
        public event Action MenuRequested;
        public event Action StartRequested;
        public event Action AbandonRequested;
        public event Action<int> ChoiceRequested;
        public event Action<string> CharacterFocusRequested;
        public event Action MapToggleRequested;
        public event Action<int> SkillRequested;
        public event Action<float> BattleSpeedRequested;

        private void Awake()
        {
            if (_bound)
            {
                return;
            }
            if (!_root || !_mapNodeTemplate || !_mapConnectionTemplate ||
                _toolbar.Length != 2 || _players.Length != 4 || _enemies.Length != 4 ||
                _choices.Length != 3 || _skills.Length != 7 || _battleSpeedControls.Length != 3 ||
                !_projectile || !_projectileLabel || !_resultRestart.Button || !_resultReturn.Button)
            {
                throw new InvalidOperationException(MissingPrefabReferencesMessage);
            }

            _bound = true;
            for (int index = 1; index < _skills.Length; index++)
            {
                int slot = index - 1;
                _skills[index].Button.onClick.AddListener(() => SkillRequested?.Invoke(slot));
            }
            for (int index = 0; index < _battleSpeedControls.Length; index++)
            {
                int speed = index;
                AdventureUiHelper.BindClick(_battleSpeedControls[index], () => BattleSpeedRequested?.Invoke(speed));
            }
            AdventureUiHelper.BindClick(_resultRestart, RequestStart);
            AdventureUiHelper.BindClick(_resultReturn, _menu.RequestReturn);
            AdventureUiHelper.BindClick(_toolbar[0], ToggleMenu);
            AdventureUiHelper.BindClick(_toolbar[1], () => MapToggleRequested?.Invoke());
            for (int index = 0; index < _choices.Length; index++)
            {
                int choice = index;
                AdventureUiHelper.BindClick(_choices[index], () => ChoiceRequested?.Invoke(choice));
            }
            foreach (AdventureActorViewReferences actor in _players)
            {
                AdventureUiHelper.BindActor(actor, id => CharacterFocusRequested?.Invoke(id));
            }
            foreach (AdventureActorViewReferences actor in _enemies)
            {
                AdventureUiHelper.BindActor(actor, id => CharacterFocusRequested?.Invoke(id));
            }

            _layout = new AdventureDynamicLayoutHelper(_root, _actorArea, _players, _enemies,
                _versus, _skillArea, _skills, _relicArea, _relicIcons, _mapScroll, _mapLines, _mapRows, _connections,
                _mapConnectionTemplate, _layoutSettings, _foreground, _controlHighlight);
        }

        private void Update()
        {
            UpdateLayout();
            RenderProjectile();
        }

        public void Render(AdventureScreenModel screen)
        {
            bool modeChanged = _screen == null || _screen.ShowMap != screen.ShowMap ||
                _screen.ShowBattle != screen.ShowBattle;
            _layoutDirty |= modeChanged || _screen == null ||
                _screen.Players.Length != screen.Players.Length || _screen.Enemies.Length != screen.Enemies.Length ||
                _screen.InspectedSkills.Length != screen.InspectedSkills.Length ||
                _screen.Choices.Length != screen.Choices.Length || _screen.Relics.Length != screen.Relics.Length;
            AdventureScreenModel previous = _screen;
            _screen = screen;
            if (modeChanged)
            {
                _tooltip.HideAll();
            }
            _summary.text = screen.Summary;
            if (previous == null || !ReferenceEquals(previous.Relics, screen.Relics))
            {
                RenderRelics(screen.Relics);
            }
            if (previous == null || previous.CanAbandon != screen.CanAbandon || previous.CanStart != screen.CanStart)
            {
                _menu.SetRunState(screen.CanAbandon, screen.CanStart);
            }
            AdventureUiHelper.SetEnabled(_toolbar[1], screen.CanToggleMap, _foreground, _secondary);
            _toolbar[1].Label.text = screen.MapButtonText;
            _arena.gameObject.SetActive(!screen.ShowMap && screen.ShowBattle);
            _actionPanel.Rect.gameObject.SetActive(!screen.ShowMap && !screen.ShowBattle);
            _skillPanel.Rect.gameObject.SetActive(!screen.ShowMap);
            _mapPanel.Rect.gameObject.SetActive(screen.ShowMap);
            _situation.text = screen.Situation;
            _resultRestart.Rect.gameObject.SetActive(screen.ShowResultActions);
            _resultRestart.Button.interactable = screen.CanStart;
            _resultReturn.Rect.gameObject.SetActive(screen.ShowResultActions);
            _skillHeading.text = screen.InspectedCharacterName + InspectedSkillsTitleSuffix;
            for (int index = 0; index < _players.Length; index++)
            {
                RenderActor(_players[index], index < screen.Players.Length ? screen.Players[index] : null);
                RenderActor(_enemies[index], index < screen.Enemies.Length ? screen.Enemies[index] : null);
            }

            AdventureUiHelper.RenderSkills(_skills, screen.InspectedSkills);
            AdventureUiHelper.RenderChoices(_choices, screen.Choices, screen.CanChoose);

            if (screen.ShowMap && (previous == null || !ReferenceEquals(previous.MapRows, screen.MapRows)))
            {
                _layoutDirty = true;
                RenderMap(screen.MapRows);
            }
            UpdateLayout();
            if (screen.ShowMap && (modeChanged || _lastMapRow != screen.MapCurrentRow))
            {
                _mapScroll.Scroll.verticalNormalizedPosition = screen.MapRows.Length > 1
                    ? Mathf.Clamp01((float)screen.MapCurrentRow / (screen.MapRows.Length - 1)) : 0;
                _lastMapRow = screen.MapCurrentRow;
            }

            RenderTimeControls();
            RenderProjectile();
            RenderJournal();
        }

        private void RenderActor(AdventureActorViewReferences control, AdventureCharacterScreenModel character)
        {
            control.Hit.Rect.gameObject.SetActive(character != null);
            if (character == null)
            {
                return;
            }
            control.Id = character.Id;
            control.Name.text = character.Name;
            control.Badge.text = character.IsDefeated ? (control.IsPlayer ? PlayerDefeatedText : EnemyDefeatedText)
                : control.IsPlayer ? (character.IsLocalPlayer ? LocalPlayerBadgeText : TeammateBadgeText) : EnemyBadgeText;
            if (character.IsLocked)
            {
                control.Badge.text = LockedBadgeText;
            }
            control.Hit.Outline.effectColor = character.IsLocked ? _targetOutlineColor : _border;
            control.Hit.Button.interactable = !character.IsDefeated;
            AdventureUiHelper.SetGauge(control.Cast, character.Action, false);
            control.Tooltip.SetContent(character.Name, character.ActionDescription, character.SpecialIntent);
            AdventureUiHelper.SetGauge(control.Health, character.Health, false);
        }

        private void RenderTimeControls()
        {
            string[] labels = { PauseSpeedText, NormalSpeedText, DoubleSpeedText };
            for (int index = 0; index < _battleSpeedControls.Length; index++)
            {
                bool selected = _screen.BattleSpeed == index;
                _battleSpeedControls[index].Label.text = selected
                    ? string.Format(SelectedSpeedFormat, labels[index]) : labels[index];
                _battleSpeedControls[index].Outline.effectColor = selected ? _foreground : _border;
                _battleSpeedControls[index].Button.interactable = _screen.CanChangeBattleSpeed;
            }
        }

        private void RenderProjectile()
        {
            if (_screen == null)
            {
                return;
            }
            AdventureProjectileScreenModel projectile = _screen.Projectile;
            bool visible = _screen.ShowBattle && !_screen.ShowMap && projectile != null;
            _projectile.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }
            AdventureActorViewReferences source = AdventureUiHelper.FindActor(_players, _enemies, projectile.SourceCharacterId);
            AdventureActorViewReferences target = AdventureUiHelper.FindActor(_players, _enemies, projectile.TargetCharacterId);
            if (source == null || target == null)
            {
                _projectile.gameObject.SetActive(false);
                return;
            }
            _projectile.position = Vector3.Lerp(source.Badge.rectTransform.position, target.Badge.rectTransform.position,
                Mathf.Clamp01(projectile.Progress));
            _projectileLabel.text = projectile.Name;
        }

        private void RenderMap(AdventureMapRowScreenModel[] rows)
        {
            while (_mapRows.Count < rows.Length)
            {
                _mapRows.Add(new List<AdventureMapNodeViewState>());
            }
            for (int row = 0; row < _mapRows.Count; row++)
            {
                int count = row < rows.Length ? rows[row].Nodes.Length : 0;
                _layoutDirty |= _mapRows[row].Count < count;
                while (_mapRows[row].Count < count)
                {
                    AdventureMapNodeViewState node = null;
                    AdventureButtonTemplateView instance = Instantiate(_mapNodeTemplate, _mapNodes);
                    instance.name = MapNodeNamePrefix + row + MapNodeIndexSeparator + _mapRows[row].Count;
                    AdventureButtonViewReferences button = instance.Control;
                    AdventureUiHelper.BindClick(button, () => ChoiceRequested?.Invoke(node.ChoiceIndex));
                    node = new AdventureMapNodeViewState(button);
                    _mapRows[row].Add(node);
                }

                for (int index = 0; index < _mapRows[row].Count; index++)
                {
                    AdventureMapNodeViewState node = _mapRows[row][index];
                    node.Control.Rect.gameObject.SetActive(index < count);
                    if (index >= count)
                    {
                        continue;
                    }
                    AdventureMapNodeScreenModel data = rows[row].Nodes[index];
                    node.ChoiceIndex = data.ChoiceIndex;
                    node.Control.Label.text = data.Title + NewLine + data.StateText;
                    if (!node.HasState || node.CanSelect != data.CanSelect || node.IsChosen != data.IsChosen)
                    {
                        node.HasState = true;
                        node.CanSelect = data.CanSelect;
                        node.IsChosen = data.IsChosen;
                        AdventureUiHelper.SetEnabled(node.Control, data.CanSelect, _foreground, _secondary);
                        node.Control.Label.color = data.CanSelect || data.IsChosen ? _foreground : _secondary;
                        node.Control.Outline.effectColor = data.CanSelect || data.IsChosen ? _foreground : _border;
                        UnityEngine.UI.ColorBlock colors = node.Control.Button.colors;
                        colors.disabledColor = data.IsChosen ? _controlHighlight : _insetBackground;
                        node.Control.Button.colors = colors;
                    }
                }
            }

            _mapNotice.text = _screen.MapNotice;
            _layout.UpdateMap(_screen);
        }

        private void UpdateLayout()
        {
            if (_screen == null)
            {
                return;
            }
            Vector2 size = _root.rect.size;
            if (!_layoutDirty && size == _lastSize)
            {
                return;
            }
            _lastSize = size;
            _layoutDirty = false;
            _layout.Update(_screen);
        }

        private void ToggleMenu()
        {
            _tooltip.HideAll();
            MenuRequested?.Invoke();
        }

        public void RequestAbandon()
        {
            _tooltip.HideAll();
            AbandonRequested?.Invoke();
        }

        public void RequestStart()
        {
            _tooltip.HideAll();
            StartRequested?.Invoke();
        }

        private void RenderRelics(AdventureIconScreenModel[] relics)
        {
            _noRelics.enabled = relics.Length == 0;
            while (_relicIcons.Count < relics.Length)
            {
                AdventureIconItemView item = Instantiate(_relicTemplate, _relicArea.Content);
                item.name = RelicNamePrefix + _relicIcons.Count;
                _relicIcons.Add(item);
            }
            for (int index = 0; index < _relicIcons.Count; index++)
            {
                bool visible = index < relics.Length;
                _relicIcons[index].gameObject.SetActive(visible);
                if (visible)
                {
                    _relicIcons[index].Render(relics[index]);
                }
            }
        }

        private void RenderJournal()
        {
            _details.Rect.gameObject.SetActive(true);
            _detailsHeading.text = JournalHeading;
            if (_detailsText.text != _screen.Journal)
            {
                bool followLatest = _detailsScroll.Scroll.verticalNormalizedPosition <= 0.01f ||
                    _detailsText.text.Length == 0;
                _detailsText.text = _screen.Journal;
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_detailsScroll.Content);
                if (followLatest)
                {
                    _detailsScroll.Scroll.verticalNormalizedPosition = 0;
                }
            }
        }

    }
}
