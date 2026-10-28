using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace WebRPG
{
    [DisallowMultipleComponent]
    public sealed class AdventureView : MonoBehaviour, IAdventureView
    {
        [SerializeField]
        private RectTransform _root;

        [SerializeField]
        private TMP_FontAsset _font;

        private static readonly Color Background = new Color32(10, 13, 18, 255);
        private static readonly Color InsetBackground = new Color32(18, 20, 26, 255);
        private static readonly Color Panel = new Color32(26, 31, 38, 255);
        private static readonly Color ControlBackground = new Color32(41, 46, 56, 255);
        private static readonly Color ControlHighlight = new Color32(61, 69, 82, 255);
        private static readonly Color Border = new Color32(115, 115, 115, 255);
        private static readonly Color Foreground = new Color32(255, 255, 255, 255);
        private static readonly Color Secondary = new Color32(179, 184, 194, 255);
        private const int LargeFontSize = 28;
        private const int MediumFontSize = 22;
        private const int SmallFontSize = 18;
        private const float MapRowHeight = 144;
        private const float MapNodeHeight = 104;

        private SurfaceControl _header;
        private TextMeshProUGUI _heading;
        private TextMeshProUGUI _summary;
        private GaugeControl _localHealth;
        private GaugeControl _localAttack;
        private readonly ChoiceControl[] _toolbar = new ChoiceControl[5];
        private RectTransform _arena;
        private ScrollControl _actorScroll;
        private TextMeshProUGUI _versus;
        private readonly ActorControl[] _players = new ActorControl[4];
        private readonly ActorControl[] _enemies = new ActorControl[4];
        private SurfaceControl _actionPanel;
        private TextMeshProUGUI _situation;
        private readonly ChoiceControl[] _choices = new ChoiceControl[3];
        private SurfaceControl _skillPanel;
        private TextMeshProUGUI _skillHeading;
        private ScrollControl _skillScroll;
        private readonly SkillControl[] _skills = new SkillControl[7];
        private SurfaceControl _mapPanel;
        private TextMeshProUGUI _mapNotice;
        private ScrollControl _mapScroll;
        private RectTransform _mapLines;
        private RectTransform _mapNodes;
        private readonly List<List<MapNodeControl>> _mapRows = new List<List<MapNodeControl>>();
        private readonly List<SurfaceControl> _connections = new List<SurfaceControl>();
        private SurfaceControl _details;
        private TextMeshProUGUI _detailsHeading;
        private TextMeshProUGUI _detailsText;
        private ScrollControl _detailsScroll;
        private ChoiceControl _closeDetails;
        private AdventureScreenModel _screen;
        private Vector2 _lastSize = new Vector2(-1, -1);
        private int _detailsKind;
        private int _lastMapRow = -1;
        private bool _layoutDirty = true;

        public event Action StartRequested;
        public event Action AbandonRequested;
        public event Action<int> ChoiceRequested;
        public event Action<string> CharacterFocusRequested;
        public event Action MapToggleRequested;

        private void Awake()
        {
            if (!_root || !_font)
            {
                throw new InvalidOperationException("AdventureView requires root and font references.");
            }

            UnityEngine.UI.Image backdrop = _root.gameObject.AddComponent<UnityEngine.UI.Image>();
            backdrop.color = Background;
            backdrop.raycastTarget = false;
            _header = CreateSurface("LocalPlayerHud", _root, Background);
            _heading = CreateText("Heading", _header.Rect, LargeFontSize);
            _summary = CreateText("LocalSummary", _header.Rect, SmallFontSize);
            _localHealth = CreateGauge("LocalHealth", _header.Rect, Border);
            _localAttack = CreateGauge("LocalAttack", _header.Rect, ControlHighlight);
            _toolbar[0] = CreateButton("Start", _header.Rect, () => StartRequested?.Invoke());
            _toolbar[0].Label.text = "開始挑戰";
            _toolbar[1] = CreateButton("Abandon", _header.Rect, () => AbandonRequested?.Invoke());
            _toolbar[1].Label.text = "放棄本輪";
            _toolbar[2] = CreateButton("MapToggle", _header.Rect, () => MapToggleRequested?.Invoke());
            _toolbar[3] = CreateButton("Relics", _header.Rect, () => OpenDetails(2));
            _toolbar[3].Label.text = "遺物";
            _toolbar[4] = CreateButton("Journal", _header.Rect, () => OpenDetails(1));
            _toolbar[4].Label.text = "紀錄";

            _arena = CreateRect("BattleScreen", _root);
            _actorScroll = CreateScroll("CharacterRow", _arena, true);
            _versus = CreateText("Versus", _actorScroll.Content, MediumFontSize);
            _versus.text = "VS";
            _versus.alignment = TextAlignmentOptions.Center;
            _versus.color = Secondary;
            for (int index = 0; index < _players.Length; index++)
            {
                _players[index] = CreateActor("Player" + index, true);
                _enemies[index] = CreateActor("Enemy" + index, false);
            }

            _actionPanel = CreateSurface("Actions", _root, Panel);
            _situation = CreateText("Situation", _actionPanel.Rect, SmallFontSize);
            _situation.alignment = TextAlignmentOptions.Top;
            for (int index = 0; index < _choices.Length; index++)
            {
                int choice = index;
                _choices[index] = CreateButton("Choice" + index, _actionPanel.Rect,
                    () => ChoiceRequested?.Invoke(choice));
            }

            _skillPanel = CreateSurface("InspectedSkills", _root, Panel);
            _skillHeading = CreateText("InspectedCharacterName", _skillPanel.Rect, MediumFontSize);
            _skillScroll = CreateScroll("SkillRow", _skillPanel.Rect, true);
            for (int index = 0; index < _skills.Length; index++)
            {
                SurfaceControl card = CreateSurface("Skill" + index, _skillScroll.Content, InsetBackground);
                TextMeshProUGUI title = CreateText("SkillName", card.Rect, SmallFontSize);
                title.textWrappingMode = TextWrappingModes.NoWrap;
                Place(title.rectTransform, 0, 1, 1, 1, 8, -34, -8, -4);
                GaugeControl gauge = CreateGauge("Cooldown", card.Rect, ControlHighlight);
                Place(gauge.Rect, 0, 0, 1, 0, 8, 6, -8, 36);
                _skills[index] = new SkillControl(card.Rect, title, gauge);
            }

            _mapPanel = CreateSurface("MapScreen", _root, Panel);
            _mapNotice = CreateText("MapNotice", _mapPanel.Rect, SmallFontSize);
            _mapScroll = CreateScroll("NodeMap", _mapPanel.Rect, false);
            _mapLines = CreateRect("Connections", _mapScroll.Content);
            _mapNodes = CreateRect("Nodes", _mapScroll.Content);
            Place(_mapLines, 0, 0, 1, 1, 0, 0, 0, 0);
            Place(_mapNodes, 0, 0, 1, 1, 0, 0, 0, 0);

            _details = CreateSurface("Details", _root, Panel);
            _details.Image.raycastTarget = true;
            _detailsHeading = CreateText("DetailsHeading", _details.Rect, MediumFontSize);
            _detailsScroll = CreateScroll("DetailsText", _details.Rect, false);
            _detailsText = CreateText("Text", _detailsScroll.Content, SmallFontSize);
            _detailsText.overflowMode = TextOverflowModes.Overflow;
            UnityEngine.UI.VerticalLayoutGroup layout =
                _detailsScroll.Content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            UnityEngine.UI.ContentSizeFitter fitter =
                _detailsScroll.Content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            _closeDetails = CreateButton("CloseDetails", _details.Rect, () => OpenDetails(0));
            _closeDetails.Label.text = "關閉";
            _details.Rect.gameObject.SetActive(false);
        }

        private void Update()
        {
            UpdateLayout();
        }

        public void Render(AdventureScreenModel screen)
        {
            bool modeChanged = _screen == null || _screen.ShowMap != screen.ShowMap ||
                _screen.ShowBattle != screen.ShowBattle;
            bool inspectionChanged = _screen == null || _screen.InspectedCharacterId != screen.InspectedCharacterId;
            _layoutDirty |= modeChanged || _screen == null ||
                _screen.Players.Length != screen.Players.Length || _screen.Enemies.Length != screen.Enemies.Length ||
                _screen.InspectedSkills.Length != screen.InspectedSkills.Length ||
                _screen.Choices.Length != screen.Choices.Length;
            _screen = screen;
            if (modeChanged)
            {
                _detailsKind = 0;
            }
            _heading.text = screen.Heading;
            _summary.text = screen.Summary;
            SetGauge(_localHealth, screen.PlayerHealth, false);
            SetGauge(_localAttack, screen.BasicAttack, true);
            SetEnabled(_toolbar[0], screen.CanStart);
            SetEnabled(_toolbar[1], screen.CanAbandon);
            SetEnabled(_toolbar[2], screen.CanToggleMap);
            _toolbar[2].Label.text = screen.MapButtonText;
            _arena.gameObject.SetActive(!screen.ShowMap && screen.ShowBattle);
            _actionPanel.Rect.gameObject.SetActive(!screen.ShowMap && !screen.ShowBattle);
            _skillPanel.Rect.gameObject.SetActive(!screen.ShowMap);
            _mapPanel.Rect.gameObject.SetActive(screen.ShowMap);
            _situation.text = screen.Situation;
            _skillHeading.text = screen.InspectedCharacterName + " · 技能狀態";
            for (int index = 0; index < _players.Length; index++)
            {
                RenderActor(_players[index], index < screen.Players.Length ? screen.Players[index] : null);
                RenderActor(_enemies[index], index < screen.Enemies.Length ? screen.Enemies[index] : null);
            }

            for (int index = 0; index < _skills.Length; index++)
            {
                bool visible = index < screen.InspectedSkills.Length;
                _skills[index].Rect.gameObject.SetActive(visible);
                if (visible)
                {
                    _skills[index].Title.text = screen.InspectedSkills[index].Title;
                    SetGauge(_skills[index].Gauge, screen.InspectedSkills[index], false);
                }
            }

            for (int index = 0; index < _choices.Length; index++)
            {
                bool visible = index < screen.Choices.Length;
                _choices[index].Rect.gameObject.SetActive(visible);
                if (visible)
                {
                    _choices[index].Label.text = screen.Choices[index];
                }
            }

            if (screen.ShowMap)
            {
                RenderMap(screen.MapRows);
            }
            UpdateLayout();
            if (inspectionChanged)
            {
                _skillScroll.Scroll.horizontalNormalizedPosition = 0;
            }
            if (screen.ShowMap && (modeChanged || _lastMapRow != screen.MapCurrentRow))
            {
                _mapScroll.Scroll.verticalNormalizedPosition = screen.MapRows.Length > 1
                    ? Mathf.Clamp01((float)screen.MapCurrentRow / (screen.MapRows.Length - 1)) : 0;
                _lastMapRow = screen.MapCurrentRow;
            }

            RenderDetails();
        }

        private ActorControl CreateActor(string name, bool isPlayer)
        {
            ActorControl actor = null;
            ChoiceControl hit = CreateButton(name, _actorScroll.Content,
                () => CharacterFocusRequested?.Invoke(actor.Id));
            hit.Label.gameObject.SetActive(false);
            UnityEngine.UI.ColorBlock colors = hit.Button.colors;
            colors.normalColor = Background;
            colors.selectedColor = Background;
            hit.Button.colors = colors;
            SurfaceControl visual = CreateSurface("CharacterMarker", hit.Rect, Panel);
            Place(visual.Rect, 0, 0, 1, 1, 12, 78, -12, -8);
            TextMeshProUGUI badge = CreateText("CharacterState", visual.Rect, MediumFontSize);
            badge.alignment = TextAlignmentOptions.Center;
            Place(badge.rectTransform, 0, 0, 1, 1, 4, 4, -4, -4);
            TextMeshProUGUI label = CreateText("CharacterName", hit.Rect, SmallFontSize);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            Place(label.rectTransform, 0, 0, 1, 0, 6, 40, -6, 70);
            GaugeControl health = CreateGauge("Health", hit.Rect, Border);
            Place(health.Rect, 0, 0, 1, 0, 6, 4, -6, 34);
            actor = new ActorControl(hit, badge, label, health, isPlayer);
            UnityEngine.EventSystems.EventTrigger trigger =
                hit.Rect.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            AddTrigger(trigger, UnityEngine.EventSystems.EventTriggerType.PointerEnter,
                () => CharacterFocusRequested?.Invoke(actor.Id));
            AddTrigger(trigger, UnityEngine.EventSystems.EventTriggerType.PointerExit,
                () => CharacterFocusRequested?.Invoke(null));
            AddTrigger(trigger, UnityEngine.EventSystems.EventTriggerType.Select,
                () => CharacterFocusRequested?.Invoke(actor.Id));
            AddTrigger(trigger, UnityEngine.EventSystems.EventTriggerType.Deselect,
                () => CharacterFocusRequested?.Invoke(null));
            return actor;
        }

        private void RenderActor(ActorControl control, AdventureCharacterScreenModel character)
        {
            control.Hit.Rect.gameObject.SetActive(character != null);
            if (character == null)
            {
                return;
            }
            control.Id = character.Id;
            control.Name.text = character.Name;
            control.Badge.text = character.IsDefeated ? (control.IsPlayer ? "已倒下" : "已擊倒")
                : control.IsPlayer ? (character.IsLocalPlayer ? "本機" : "隊友") : "敵方";
            control.Hit.Outline.effectColor = _screen.InspectedCharacterId == character.Id ? Foreground : Border;
            SetGauge(control.Health, character.Health, false);
        }

        private static void AddTrigger(UnityEngine.EventSystems.EventTrigger trigger,
            UnityEngine.EventSystems.EventTriggerType type, Action action)
        {
            UnityEngine.EventSystems.EventTrigger.Entry entry = new UnityEngine.EventSystems.EventTrigger.Entry
            {
                eventID = type
            };
            entry.callback.AddListener(data => action());
            trigger.triggers.Add(entry);
        }

        private void RenderMap(AdventureMapRowScreenModel[] rows)
        {
            while (_mapRows.Count < rows.Length)
            {
                _mapRows.Add(new List<MapNodeControl>());
            }
            for (int row = 0; row < _mapRows.Count; row++)
            {
                int count = row < rows.Length ? rows[row].Nodes.Length : 0;
                _layoutDirty |= _mapRows[row].Count < count;
                while (_mapRows[row].Count < count)
                {
                    MapNodeControl node = null;
                    ChoiceControl button = CreateButton("Node" + row + "_" + _mapRows[row].Count, _mapNodes,
                        () => ChoiceRequested?.Invoke(node.ChoiceIndex));
                    node = new MapNodeControl(button);
                    _mapRows[row].Add(node);
                }

                for (int index = 0; index < _mapRows[row].Count; index++)
                {
                    MapNodeControl node = _mapRows[row][index];
                    node.Control.Rect.gameObject.SetActive(index < count);
                    if (index >= count)
                    {
                        continue;
                    }
                    AdventureMapNodeScreenModel data = rows[row].Nodes[index];
                    node.ChoiceIndex = data.ChoiceIndex;
                    node.Control.Label.text = data.Title + "\n" + data.StateText;
                    if (!node.HasState || node.CanSelect != data.CanSelect || node.IsChosen != data.IsChosen)
                    {
                        node.HasState = true;
                        node.CanSelect = data.CanSelect;
                        node.IsChosen = data.IsChosen;
                        SetEnabled(node.Control, data.CanSelect);
                        node.Control.Label.color = data.CanSelect || data.IsChosen ? Foreground : Secondary;
                        node.Control.Outline.effectColor = data.CanSelect || data.IsChosen ? Foreground : Border;
                        UnityEngine.UI.ColorBlock colors = node.Control.Button.colors;
                        colors.disabledColor = data.IsChosen ? ControlHighlight : InsetBackground;
                        node.Control.Button.colors = colors;
                    }
                }
            }

            _mapNotice.text = _screen.MapNotice;
            LayoutMap();
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
            bool narrow = size.x < 1100;
            Place(_header.Rect, 0, 1, 1, 1, 12, -192, -12, -12);
            Place(_heading.rectTransform, 0, 1, 1, 1, 16, -54, -16, -8);
            Place(_summary.rectTransform, 0, 1, 1, 1, 16, -86, -16, -56);
            Place(_localHealth.Rect, 0, 1, 0.5f, 1, 16, -124, -6, -94);
            Place(_localAttack.Rect, 0.5f, 1, 1, 1, 6, -124, -16, -94);
            for (int index = 0; index < _toolbar.Length; index++)
            {
                Place(_toolbar[index].Rect, (float)index / _toolbar.Length, 0,
                    (float)(index + 1) / _toolbar.Length, 0, 8, 8, -8, 42);
            }

            Place(_arena, 0, 0, 1, 1, 12, 164, -12, -204);
            Place(_actorScroll.Rect, 0, 0, 1, 1, 0, 0, 0, 0);
            Place(_actionPanel.Rect, 0, 0, 1, 1, 16, 164, -16, -204);
            Place(_situation.rectTransform, 0, 1, 1, 1, 20, -100, -20, -12);
            int count = _screen.Choices.Length;
            float choiceWidth = Mathf.Min(300, (size.x - 64 - Math.Max(0, count - 1) * 16) / Math.Max(1, count));
            float groupWidth = count * choiceWidth + Math.Max(0, count - 1) * 16;
            for (int index = 0; index < count; index++)
            {
                RectTransform rect = _choices[index].Rect;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.45f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(choiceWidth, 160);
                rect.anchoredPosition = new Vector2(-groupWidth / 2 + choiceWidth / 2 + index * (choiceWidth + 16), 0);
            }

            Place(_skillPanel.Rect, 0, 0, 1, 0, 12, 12, -12, 152);
            Place(_skillHeading.rectTransform, 0, 1, 1, 1, 16, -40, -16, -6);
            Place(_skillScroll.Rect, 0, 0, 1, 1, 12, 8, -12, -48);
            LayoutCharacters(narrow ? 144 : 176);
            LayoutSkills(narrow ? 144 : 160);
            Place(_mapPanel.Rect, 0, 0, 1, 1, 12, 12, -12, -204);
            Place(_mapNotice.rectTransform, 0, 1, 1, 1, 16, -44, -16, -8);
            Place(_mapScroll.Rect, 0, 0, 1, 1, 16, 8, -16, -52);
            LayoutMap();
            Place(_details.Rect, 0, 0, 1, 1, 24, _screen.ShowMap ? 24 : 164, -24, -204);
            Place(_detailsHeading.rectTransform, 0, 1, 1, 1, 16, -40, -120, -6);
            Place(_closeDetails.Rect, 1, 1, 1, 1, -112, -42, -12, -8);
            Place(_detailsScroll.Rect, 0, 0, 1, 1, 16, 8, -16, -52);
        }

        private void LayoutCharacters(float width)
        {
            int playerCount = _screen.Players.Length;
            int enemyCount = _screen.Enemies.Length;
            int count = playerCount + enemyCount;
            float extra = playerCount > 0 && enemyCount > 0 ? 40 : 0;
            float group = count * width + Math.Max(0, count - 1) * 16 + extra;
            float contentWidth = Mathf.Max(_actorScroll.Viewport.rect.width, group + 32);
            SetHorizontalContent(_actorScroll.Content, contentWidth);
            float start = (contentWidth - group) / 2;
            for (int index = 0; index < playerCount; index++)
            {
                PlaceCard(_players[index].Hit.Rect, start + width / 2 + index * (width + 16), width, 214);
            }

            for (int index = 0; index < enemyCount; index++)
            {
                float x = start + (playerCount + index) * (width + 16) + extra + width / 2;
                PlaceCard(_enemies[index].Hit.Rect, x, width, 214);
            }

            _versus.gameObject.SetActive(extra > 0);
            float separator = start + playerCount * width + Math.Max(0, playerCount - 1) * 16 + 28;
            PlaceCard(_versus.rectTransform, separator, 44, 36);
        }

        private void LayoutSkills(float width)
        {
            int count = _screen.InspectedSkills.Length;
            float group = count * width + Math.Max(0, count - 1) * 12;
            float contentWidth = Mathf.Max(_skillScroll.Viewport.rect.width, group + 16);
            SetHorizontalContent(_skillScroll.Content, contentWidth);
            float start = (contentWidth - group) / 2;
            for (int index = 0; index < count; index++)
            {
                PlaceCard(_skills[index].Rect, start + width / 2 + index * (width + 12), width, 80);
            }
        }

        private void LayoutMap()
        {
            if (_screen == null || !_screen.ShowMap)
            {
                return;
            }
            AdventureMapRowScreenModel[] rows = _screen.MapRows;
            float width = Mathf.Max(1, _mapScroll.Viewport.rect.width);
            float height = Math.Max(0, rows.Length - 1) * MapRowHeight + MapNodeHeight + 64;
            _mapScroll.Content.anchorMin = new Vector2(0, 1);
            _mapScroll.Content.anchorMax = new Vector2(1, 1);
            _mapScroll.Content.pivot = new Vector2(0, 1);
            _mapScroll.Content.sizeDelta = new Vector2(0, height);
            float nodeWidth = Mathf.Clamp(width / 4 - 16, 128, 200);
            int connection = 0;
            for (int row = 0; row < rows.Length; row++)
            {
                for (int index = 0; index < rows[row].Nodes.Length; index++)
                {
                    RectTransform rect = _mapRows[row][index].Control.Rect;
                    rect.anchorMin = rect.anchorMax = Vector2.zero;
                    rect.pivot = new Vector2(0.5f, 0);
                    rect.sizeDelta = new Vector2(nodeWidth, MapNodeHeight);
                    rect.anchoredPosition = new Vector2(width * (index + 1) / (rows[row].Nodes.Length + 1),
                        32 + row * MapRowHeight);
                    if (row + 1 >= rows.Length)
                    {
                        continue;
                    }
                    for (int next = 0; next < rows[row + 1].Nodes.Length; next++)
                    {
                        if (connection >= _connections.Count)
                        {
                            SurfaceControl line = CreateSurface("Connection" + connection, _mapLines, ControlHighlight);
                            line.Outline.enabled = false;
                            _connections.Add(line);
                        }

                        SurfaceControl edge = _connections[connection++];
                        edge.Rect.gameObject.SetActive(true);
                        edge.Image.color = rows[row].Nodes[index].IsChosen && rows[row + 1].Nodes[next].IsChosen
                            ? Foreground : ControlHighlight;
                        Vector2 from = new Vector2(rect.anchoredPosition.x, 32 + row * MapRowHeight + MapNodeHeight);
                        Vector2 to = new Vector2(width * (next + 1) / (rows[row + 1].Nodes.Length + 1),
                            32 + (row + 1) * MapRowHeight);
                        Vector2 delta = to - from;
                        edge.Rect.anchorMin = edge.Rect.anchorMax = Vector2.zero;
                        edge.Rect.pivot = new Vector2(0, 0.5f);
                        edge.Rect.anchoredPosition = from;
                        edge.Rect.sizeDelta = new Vector2(delta.magnitude, 2);
                        edge.Rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                    }
                }
            }

            for (int index = connection; index < _connections.Count; index++)
            {
                _connections[index].Rect.gameObject.SetActive(false);
            }
        }

        private void OpenDetails(int kind)
        {
            _detailsKind = _detailsKind == kind ? 0 : kind;
            RenderDetails();
        }

        private void RenderDetails()
        {
            if (_screen == null)
            {
                return;
            }
            _details.Rect.gameObject.SetActive(_detailsKind != 0);
            if (_detailsKind == 0)
            {
                return;
            }
            _detailsHeading.text = _detailsKind == 1 ? "戰鬥紀錄" : "本機玩家 · 遺物";
            string text = _detailsKind == 1 ? _screen.Journal : _screen.Relics;
            if (_detailsText.text != text)
            {
                _detailsText.text = text;
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_detailsScroll.Content);
                _detailsScroll.Scroll.verticalNormalizedPosition = _detailsKind == 1 ? 0 : 1;
            }

            _details.Rect.SetAsLastSibling();
        }

        private GaugeControl CreateGauge(string name, RectTransform parent, Color color)
        {
            SurfaceControl surface = CreateSurface(name, parent, InsetBackground);
            RectTransform fill = CreateRect("Fill", surface.Rect);
            Place(fill, 0, 0, 1, 1, 1, 1, -1, -1);
            UnityEngine.UI.Image image = fill.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            TextMeshProUGUI value = CreateText("Value", surface.Rect, SmallFontSize);
            value.alignment = TextAlignmentOptions.Center;
            value.textWrappingMode = TextWrappingModes.NoWrap;
            Place(value.rectTransform, 0, 0, 1, 1, 6, 0, -6, 0);
            return new GaugeControl(surface.Rect, fill, value);
        }

        private static void SetGauge(GaugeControl control, AdventureGaugeScreenModel model, bool includeTitle)
        {
            float amount = float.IsNaN(model.FillAmount) ? 0 : Mathf.Clamp01(model.FillAmount);
            control.Fill.gameObject.SetActive(amount > 0);
            control.Fill.anchorMax = new Vector2(amount, 1);
            control.Value.text = includeTitle ? model.Title + " · " + model.Text : model.Text;
        }

        private SurfaceControl CreateSurface(string name, RectTransform parent, Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            UnityEngine.UI.Image image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            UnityEngine.UI.Outline outline = rect.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = Border;
            outline.effectDistance = new Vector2(1, -1);
            return new SurfaceControl(rect, image, outline);
        }

        private ChoiceControl CreateButton(string name, RectTransform parent, Action clicked)
        {
            SurfaceControl surface = CreateSurface(name, parent, Foreground);
            surface.Image.raycastTarget = true;
            UnityEngine.UI.Button button = surface.Rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = surface.Image;
            UnityEngine.UI.ColorBlock colors = button.colors;
            colors.normalColor = ControlBackground;
            colors.highlightedColor = ControlHighlight;
            colors.pressedColor = Background;
            colors.selectedColor = Border;
            colors.disabledColor = InsetBackground;
            colors.colorMultiplier = 1;
            button.colors = colors;
            button.onClick.AddListener(() => clicked());
            TextMeshProUGUI label = CreateText("Label", surface.Rect, SmallFontSize);
            label.alignment = TextAlignmentOptions.Center;
            Place(label.rectTransform, 0, 0, 1, 1, 8, 2, -8, -2);
            return new ChoiceControl(surface.Rect, button, label, surface.Outline);
        }

        private TextMeshProUGUI CreateText(string name, RectTransform parent, int size)
        {
            RectTransform rect = CreateRect(name, parent);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.fontSize = size;
            text.color = Foreground;
            text.raycastTarget = false;
            text.richText = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private ScrollControl CreateScroll(string name, RectTransform parent, bool horizontal)
        {
            RectTransform rect = CreateRect(name, parent);
            UnityEngine.UI.ScrollRect scroll = rect.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.horizontal = horizontal;
            scroll.vertical = !horizontal;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25;
            RectTransform viewport = CreateRect("Viewport", rect);
            Place(viewport, 0, 0, 1, 1, 0, 0, 0, 0);
            UnityEngine.UI.Image image = viewport.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.raycastTarget = true;
            UnityEngine.UI.Mask mask = viewport.gameObject.AddComponent<UnityEngine.UI.Mask>();
            mask.showMaskGraphic = false;
            RectTransform content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            scroll.viewport = viewport;
            scroll.content = content;
            return new ScrollControl(rect, viewport, content, scroll);
        }

        private static void SetEnabled(ChoiceControl control, bool enabled)
        {
            control.Button.interactable = enabled;
            control.Label.color = enabled ? Foreground : Secondary;
        }

        private static void SetHorizontalContent(RectTransform content, float width)
        {
            content.anchorMin = new Vector2(0, 0);
            content.anchorMax = new Vector2(0, 1);
            content.pivot = new Vector2(0, 0.5f);
            content.sizeDelta = new Vector2(width, 0);
        }

        private static void PlaceCard(RectTransform rect, float x, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static RectTransform CreateRect(string name, RectTransform parent)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)child.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Place(RectTransform rect, float minimumX, float minimumY, float maximumX,
            float maximumY, float left, float bottom, float right, float top)
        {
            rect.anchorMin = new Vector2(minimumX, minimumY);
            rect.anchorMax = new Vector2(maximumX, maximumY);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }

        private sealed class SurfaceControl
        {
            public SurfaceControl(RectTransform rect, UnityEngine.UI.Image image, UnityEngine.UI.Outline outline)
            {
                Rect = rect;
                Image = image;
                Outline = outline;
            }
            public RectTransform Rect { get; }
            public UnityEngine.UI.Image Image { get; }
            public UnityEngine.UI.Outline Outline { get; }
        }

        private sealed class ChoiceControl
        {
            public ChoiceControl(RectTransform rect, UnityEngine.UI.Button button, TextMeshProUGUI label,
                UnityEngine.UI.Outline outline)
            {
                Rect = rect;
                Button = button;
                Label = label;
                Outline = outline;
            }
            public RectTransform Rect { get; }
            public UnityEngine.UI.Button Button { get; }
            public TextMeshProUGUI Label { get; }
            public UnityEngine.UI.Outline Outline { get; }
        }

        private sealed class ActorControl
        {
            public ActorControl(ChoiceControl hit, TextMeshProUGUI badge, TextMeshProUGUI name,
                GaugeControl health, bool isPlayer)
            {
                Hit = hit;
                Badge = badge;
                Name = name;
                Health = health;
                IsPlayer = isPlayer;
            }
            public string Id { get; set; }
            public ChoiceControl Hit { get; }
            public TextMeshProUGUI Badge { get; }
            public TextMeshProUGUI Name { get; }
            public GaugeControl Health { get; }
            public bool IsPlayer { get; }
        }

        private sealed class SkillControl
        {
            public SkillControl(RectTransform rect, TextMeshProUGUI title, GaugeControl gauge)
            {
                Rect = rect;
                Title = title;
                Gauge = gauge;
            }
            public RectTransform Rect { get; }
            public TextMeshProUGUI Title { get; }
            public GaugeControl Gauge { get; }
        }

        private sealed class GaugeControl
        {
            public GaugeControl(RectTransform rect, RectTransform fill, TextMeshProUGUI value)
            {
                Rect = rect;
                Fill = fill;
                Value = value;
            }
            public RectTransform Rect { get; }
            public RectTransform Fill { get; }
            public TextMeshProUGUI Value { get; }
        }

        private sealed class ScrollControl
        {
            public ScrollControl(RectTransform rect, RectTransform viewport, RectTransform content,
                UnityEngine.UI.ScrollRect scroll)
            {
                Rect = rect;
                Viewport = viewport;
                Content = content;
                Scroll = scroll;
            }
            public RectTransform Rect { get; }
            public RectTransform Viewport { get; }
            public RectTransform Content { get; }
            public UnityEngine.UI.ScrollRect Scroll { get; }
        }

        private sealed class MapNodeControl
        {
            public MapNodeControl(ChoiceControl control)
            {
                Control = control;
            }
            public ChoiceControl Control { get; }
            public int ChoiceIndex { get; set; }
            public bool HasState { get; set; }
            public bool CanSelect { get; set; }
            public bool IsChosen { get; set; }
        }
    }
}
