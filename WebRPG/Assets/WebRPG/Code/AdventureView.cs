using System;
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

        private static readonly Color Background = new Color32(14, 20, 31, 255);
        private static readonly Color Panel = new Color32(24, 33, 49, 255);
        private static readonly Color Border = new Color32(100, 112, 130, 255);
        private static readonly Color Foreground = new Color32(242, 244, 248, 255);
        private static readonly Color Secondary = new Color32(190, 199, 213, 255);
        private static readonly Color Accent = new Color32(217, 184, 108, 255);
        private const int LargeFontSize = 28;
        private const int MediumFontSize = 22;
        private const int SmallFontSize = 18;

        private RectTransform _sidebar;
        private RectTransform _header;
        private RectTransform _body;
        private RectTransform _routePanel;
        private RectTransform _activityPanel;
        private RectTransform _footer;
        private RectTransform _skillGrid;
        private TextMeshProUGUI _heading;
        private TextMeshProUGUI _summary;
        private TextMeshProUGUI _route;
        private UnityEngine.UI.ScrollRect _routeScroll;
        private RectTransform _routeContent;
        private RectTransform _journalArea;
        private UnityEngine.UI.ScrollRect _journalScroll;
        private RectTransform _journalContent;
        private int _visibleChoices;
        private TextMeshProUGUI _situation;
        private TextMeshProUGUI _journal;
        private TextMeshProUGUI _relics;
        private readonly TextMeshProUGUI[] _skills = new TextMeshProUGUI[6];
        private readonly ChoiceControl[] _choices = new ChoiceControl[3];
        private ChoiceControl _start;
        private ChoiceControl _abandon;
        private bool _hasLayout;
        private bool _narrow;

        public event Action StartRequested;
        public event Action AbandonRequested;
        public event Action<int> ChoiceRequested;

        private void Awake()
        {
            if (!_root || !_font)
            {
                throw new InvalidOperationException("AdventureView requires root and font references.");
            }

            UnityEngine.UI.Image backdrop = _root.gameObject.AddComponent<UnityEngine.UI.Image>();
            backdrop.color = Background;
            backdrop.raycastTarget = false;
            _sidebar = CreatePanel("Sidebar", _root);
            TextMeshProUGUI brand = CreateText("Brand", _sidebar, MediumFontSize);
            brand.text = "WEB RPG\n\n灰燼之塔\n\n挑戰 / 單人\n\n試玩規則 v2";
            brand.color = Accent;
            Place(brand.rectTransform, 0, 0, 1, 1, 18, 18, -18, -18);
            _header = CreatePanel("Header", _root);
            _heading = CreateText("Heading", _header, LargeFontSize);
            _summary = CreateText("Summary", _header, SmallFontSize);
            _start = CreateButton("Start", _header, () => StartRequested?.Invoke());
            _start.Label.text = "開始挑戰";
            _abandon = CreateButton("Abandon", _header, () => AbandonRequested?.Invoke());
            _abandon.Label.text = "放棄本輪";
            _body = CreateRect("Body", _root);
            _routePanel = CreatePanel("Route", _body);
            _route = CreateScrollText("RouteText", _routePanel, out _routeScroll, out _routeContent);
            _activityPanel = CreatePanel("Activity", _body);
            _situation = CreateText("Situation", _activityPanel, SmallFontSize);
            for (int index = 0; index < _choices.Length; index++)
            {
                int choiceIndex = index;
                _choices[index] = CreateButton("Choice" + index, _activityPanel,
                    () => ChoiceRequested?.Invoke(choiceIndex));
            }

            _journalArea = CreateRect("JournalArea", _activityPanel);
            _journal = CreateScrollText("Journal", _journalArea, out _journalScroll, out _journalContent);
            _journal.color = Secondary;
            _footer = CreatePanel("Loadout", _root);
            _skillGrid = CreateRect("SkillGrid", _footer);
            _relics = CreateText("Relics", _footer, SmallFontSize);
            for (int index = 0; index < _skills.Length; index++)
            {
                RectTransform slot = CreatePanel("SkillSlot" + index, _skillGrid);
                _skills[index] = CreateText("Skill", slot, SmallFontSize);
                Place(_skills[index].rectTransform, 0, 0, 1, 1, 10, 4, -10, -4);
            }

            UpdateLayout();
        }

        private void Update()
        {
            UpdateLayout();
        }

        public void Render(AdventureScreenModel screen)
        {
            _heading.text = screen.Heading;
            _summary.text = screen.Summary;
            if (_route.text != screen.Route)
            {
                _route.text = screen.Route;
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_routeContent);
                _routeScroll.verticalNormalizedPosition = 1;
            }
            _situation.text = screen.Situation;
            if (_visibleChoices != screen.Choices.Length)
            {
                _visibleChoices = screen.Choices.Length;
                LayoutActivity();
            }

            if (_journal.text != screen.Journal)
            {
                _journal.text = screen.Journal;
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_journalContent);
                _journalScroll.verticalNormalizedPosition = 0;
            }
            _relics.text = screen.Relics;
            _start.Button.interactable = screen.CanStart;
            _abandon.Button.interactable = screen.CanAbandon;
            _start.Label.color = screen.CanStart ? Foreground : Secondary;
            _abandon.Label.color = screen.CanAbandon ? Foreground : Secondary;
            for (int index = 0; index < _skills.Length; index++)
            {
                _skills[index].text = screen.Skills[index];
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
        }

        private void UpdateLayout()
        {
            bool narrow = _root.rect.width < 1000 || _root.rect.height > _root.rect.width;
            if (_hasLayout && _narrow == narrow)
            {
                return;
            }

            _hasLayout = true;
            _narrow = narrow;
            float left = narrow ? 12 : 204;
            float footerHeight = narrow ? 300 : 144;
            _sidebar.gameObject.SetActive(!narrow);
            Place(_sidebar, 0, 0, 0, 1, 12, 12, 192, -12);
            Place(_header, 0, 1, 1, 1, left, -108, -12, -12);
            Place(_footer, 0, 0, 1, 0, left, 12, -12, footerHeight);
            Place(_body, 0, 0, 1, 1, left, footerHeight + 12, -12, -120);
            Place(_heading.rectTransform, 0, 0.4f, 1, 1, 16, 0, -16, -10);
            Place(_summary.rectTransform, 0, 0, 0.55f, 0.5f, 16, 8, -8, -4);
            Place(_start.Rect, 0.57f, 0, 0.79f, 0.5f, 0, 8, -8, -4);
            Place(_abandon.Rect, 0.79f, 0, 1, 0.5f, 0, 8, -16, -4);
            if (narrow)
            {
                Place(_routePanel, 0, 0.69f, 1, 1, 0, 8, 0, 0);
                Place(_activityPanel, 0, 0, 1, 0.69f, 0, 0, 0, 0);
            }
            else
            {
                Place(_routePanel, 0, 0, 0.34f, 1, 0, 0, -8, 0);
                Place(_activityPanel, 0.34f, 0, 1, 1, 4, 0, 0, 0);
            }

            Place(_relics.rectTransform, 0, 0, 1, 0, 16, 8, -16, narrow ? 70 : 42);
            Place(_skillGrid, 0, 0, 1, 1, 12, narrow ? 74 : 46, -12, -10);
            int columns = narrow ? 2 : 6;
            int rows = narrow ? 3 : 1;
            for (int index = 0; index < _skills.Length; index++)
            {
                RectTransform slot = (RectTransform)_skills[index].transform.parent;
                int column = index % columns;
                int row = index / columns;
                Place(slot, (float)column / columns, 1f - (float)(row + 1) / rows,
                    (float)(column + 1) / columns, 1f - (float)row / rows, 3, 3, -3, -3);
            }

            LayoutActivity();
        }

        private void LayoutActivity()
        {
            float situationBottom = _visibleChoices == 0 ? 0.5f : 0.8f;
            float journalTop = _visibleChoices == 0 ? 0.5f : 0.8f - _visibleChoices * 0.15f;
            Place(_situation.rectTransform, 0, situationBottom, 1, 1, 16, 4, -16, -12);
            for (int index = 0; index < _choices.Length; index++)
            {
                Place(_choices[index].Rect, 0, 0.8f - (index + 1) * 0.15f,
                    1, 0.8f - index * 0.15f, 16, 4, -16, -4);
            }

            Place(_journalArea, 0, 0, 1, journalTop, 0, 0, 0, 0);
        }

        private TextMeshProUGUI CreateScrollText(string name, RectTransform parent,
            out UnityEngine.UI.ScrollRect scroll, out RectTransform content)
        {
            scroll = parent.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25;
            RectTransform viewport = CreateRect("Viewport", parent);
            Place(viewport, 0, 0, 1, 1, 16, 8, -16, -8);
            UnityEngine.UI.Image viewportImage = viewport.gameObject.AddComponent<UnityEngine.UI.Image>();
            viewportImage.raycastTarget = true;
            UnityEngine.UI.Mask mask = viewport.gameObject.AddComponent<UnityEngine.UI.Mask>();
            mask.showMaskGraphic = false;
            content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            UnityEngine.UI.VerticalLayoutGroup layout =
                content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            UnityEngine.UI.ContentSizeFitter fitter =
                content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            TextMeshProUGUI text = CreateText(name, content, SmallFontSize);
            text.overflowMode = TextOverflowModes.Overflow;
            scroll.viewport = viewport;
            scroll.content = content;
            return text;
        }

        private RectTransform CreatePanel(string name, RectTransform parent)
        {
            RectTransform rect = CreateRect(name, parent);
            UnityEngine.UI.Image image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = Panel;
            image.raycastTarget = false;
            UnityEngine.UI.Outline outline = rect.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = Border;
            outline.effectDistance = new Vector2(1, -1);
            return rect;
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

        private ChoiceControl CreateButton(string name, RectTransform parent, Action clicked)
        {
            RectTransform rect = CreateRect(name, parent);
            UnityEngine.UI.Image image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = Color.white;
            image.raycastTarget = true;
            UnityEngine.UI.Outline outline = rect.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = Border;
            outline.effectDistance = new Vector2(1, -1);
            UnityEngine.UI.Button button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            UnityEngine.UI.ColorBlock colors = button.colors;
            colors.normalColor = Panel;
            colors.highlightedColor = Border;
            colors.pressedColor = Background;
            colors.selectedColor = Border;
            colors.disabledColor = Panel;
            colors.colorMultiplier = 1;
            button.colors = colors;
            button.onClick.AddListener(() => clicked());
            TextMeshProUGUI label = CreateText("Label", rect, SmallFontSize);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            Place(label.rectTransform, 0, 0, 1, 1, 12, 2, -12, -2);
            return new ChoiceControl(rect, button, label);
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

        private sealed class ChoiceControl
        {
            public ChoiceControl(RectTransform rect, UnityEngine.UI.Button button, TextMeshProUGUI label)
            {
                Rect = rect;
                Button = button;
                Label = label;
            }

            public RectTransform Rect { get; }
            public UnityEngine.UI.Button Button { get; }
            public TextMeshProUGUI Label { get; }
        }
    }
}
