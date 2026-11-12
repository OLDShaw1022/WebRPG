using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace WebRPG
{
    // 依畫面尺寸調整角色、技能與地圖節點的動態排列。
    internal sealed class AdventureDynamicLayoutHelper
    {
        private const int MaximumFactionColumns = 2;
        private const float RelicHorizontalPadding = 8;
        private const float RelicTopPadding = 4;
        private const float MapNodeWidthDivisor = 4;
        private const float MapNodeWidthSpacing = 16;
        private const string ConnectionNamePrefix = "Connection";

        private readonly RectTransform _root;
        private readonly AdventureContentViewReferences _actorArea;
        private readonly AdventureActorViewReferences[] _players;
        private readonly AdventureActorViewReferences[] _enemies;
        private readonly TextMeshProUGUI _versus;
        private readonly AdventureContentViewReferences _skillArea;
        private readonly AdventureContentViewReferences _relicArea;
        private readonly List<AdventureIconItemView> _relicIcons;
        private readonly AdventureSkillViewReferences[] _skills;
        private readonly AdventureScrollViewReferences _mapScroll;
        private readonly RectTransform _mapLines;
        private readonly List<List<AdventureMapNodeViewState>> _mapRows;
        private readonly List<AdventureSurfaceViewReferences> _connections;
        private readonly AdventureSurfaceTemplateView _mapConnectionTemplate;
        private readonly AdventureDynamicLayoutSettings _settings;
        private readonly Color _foreground;
        private readonly Color _controlHighlight;
        private AdventureScreenModel _screen;

        public AdventureDynamicLayoutHelper(RectTransform root, AdventureContentViewReferences actorArea,
            AdventureActorViewReferences[] players, AdventureActorViewReferences[] enemies, TextMeshProUGUI versus,
            AdventureContentViewReferences skillArea, AdventureSkillViewReferences[] skills,
            AdventureContentViewReferences relicArea, List<AdventureIconItemView> relicIcons,
            AdventureScrollViewReferences mapScroll, RectTransform mapLines,
            List<List<AdventureMapNodeViewState>> mapRows, List<AdventureSurfaceViewReferences> connections,
            AdventureSurfaceTemplateView mapConnectionTemplate, AdventureDynamicLayoutSettings settings,
            Color foreground, Color controlHighlight)
        {
            _root = root;
            _actorArea = actorArea;
            _players = players;
            _enemies = enemies;
            _versus = versus;
            _skillArea = skillArea;
            _relicArea = relicArea;
            _relicIcons = relicIcons;
            _skills = skills;
            _mapScroll = mapScroll;
            _mapLines = mapLines;
            _mapRows = mapRows;
            _connections = connections;
            _mapConnectionTemplate = mapConnectionTemplate;
            _settings = settings;
            _foreground = foreground;
            _controlHighlight = controlHighlight;
        }

        public void Update(AdventureScreenModel screen)
        {
            _screen = screen;
            LayoutCharacters();
            LayoutSkills();
            LayoutRelics();
            LayoutMap();
        }

        public void UpdateMap(AdventureScreenModel screen)
        {
            _screen = screen;
            LayoutMap();
        }

        private void LayoutCharacters()
        {
            float available = Mathf.Max(1, _actorArea.Rect.rect.width - 2 * _settings.CharacterPadding);
            float half = Mathf.Max(1, (available - _settings.FactionGap) / 2);
            LayoutFaction(_players, _screen.Players.Length, half, -available / 2);
            LayoutFaction(_enemies, _screen.Enemies.Length, half, _settings.FactionGap / 2);
            _versus.gameObject.SetActive(_screen.Players.Length > 0 && _screen.Enemies.Length > 0);
        }

        private void LayoutFaction(AdventureActorViewReferences[] actors, int count, float available, float left)
        {
            if (count == 0)
            {
                return;
            }
            int columns = Math.Min(MaximumFactionColumns, count);
            int rows = Mathf.CeilToInt((float)count / columns);
            float width = _settings.CharacterWidth;
            float height = _settings.CharacterHeight;
            for (int index = 0; index < count; index++)
            {
                int row = index / columns;
                int rowCount = Mathf.Min(columns, count - row * columns);
                float groupWidth = rowCount * width + (rowCount - 1) * _settings.CharacterSpacing;
            float x = left + (available - groupWidth) / 2 + width / 2
                + (index % columns) * (width + _settings.CharacterSpacing);
                float y = ((rows - 1) / 2f - row) * (height + _settings.CharacterSpacing);
                RectTransform rect = actors[index].Hit.Rect;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = new Vector2(x, y);
                rect.sizeDelta = new Vector2(Mathf.Max(1, width), Mathf.Max(1, height));
            }
        }

        private void LayoutSkills()
        {
            int count = _screen.InspectedSkills.Length;
            if (count == 0)
            {
                return;
            }
            float width = _settings.SkillWidth;
            float spacing = _settings.SkillSpacing;
            float group = count * width + (count - 1) * spacing;
            float start = (_skillArea.Rect.rect.width - group) / 2;
            for (int index = 0; index < count; index++)
            {
                PlaceCard(_skills[index].Rect, start + width / 2 + index * (width + spacing), width,
                    _settings.SkillHeight);
            }
        }

        private void LayoutRelics()
        {
            int count = _screen.Relics.Length;
            float viewportWidth = Mathf.Max(1, _relicArea.Rect.rect.width);
            float contentWidth = Mathf.Max(viewportWidth, 2 * RelicHorizontalPadding + count * _settings.RelicWidth +
                Mathf.Max(0, count - 1) * _settings.RelicSpacing);
            RectTransform content = _relicArea.Content;
            content.anchorMin = content.anchorMax = new Vector2(0, 1);
            content.pivot = new Vector2(0, 1);
            content.sizeDelta = new Vector2(contentWidth, _relicArea.Rect.rect.height);
            content.anchoredPosition = new Vector2(
                Mathf.Clamp(content.anchoredPosition.x, viewportWidth - contentWidth, 0), 0);
            for (int index = 0; index < count; index++)
            {
                RectTransform rect = _relicIcons[index].Rect;
                rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(0, 1);
                rect.sizeDelta = new Vector2(_settings.RelicWidth, _settings.RelicHeight);
                rect.anchoredPosition = new Vector2(
                    RelicHorizontalPadding + index * (_settings.RelicWidth + _settings.RelicSpacing), -RelicTopPadding);
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
            float height = Math.Max(
                0, rows.Length - 1) * _settings.MapRowHeight + _settings.MapNodeHeight + 2 * _settings.MapPadding;
            _mapScroll.Content.anchorMin = new Vector2(0, 1);
            _mapScroll.Content.anchorMax = new Vector2(1, 1);
            _mapScroll.Content.pivot = new Vector2(0, 1);
            _mapScroll.Content.sizeDelta = new Vector2(0, height);
            float nodeWidth = Mathf.Clamp(
                width / MapNodeWidthDivisor - MapNodeWidthSpacing, _settings.MapNodeMinimumWidth,
                _settings.MapNodeMaximumWidth);
            int connection = 0;
            for (int row = 0; row < rows.Length; row++)
            {
                for (int index = 0; index < rows[row].Nodes.Length; index++)
                {
                    RectTransform rect = _mapRows[row][index].Control.Rect;
                    rect.anchorMin = rect.anchorMax = Vector2.zero;
                    rect.pivot = new Vector2(0.5f, 0);
                    rect.sizeDelta = new Vector2(nodeWidth, _settings.MapNodeHeight);
                    rect.anchoredPosition = new Vector2(width * (index + 1) / (rows[row].Nodes.Length + 1),
                        _settings.MapPadding + row * _settings.MapRowHeight);
                    if (row + 1 >= rows.Length)
                    {
                        continue;
                    }
                    for (int next = 0; next < rows[row + 1].Nodes.Length; next++)
                    {
                        if (connection >= _connections.Count)
                        {
                            AdventureSurfaceTemplateView instance = UnityEngine.Object.Instantiate(
                                _mapConnectionTemplate, _mapLines);
                            instance.name = ConnectionNamePrefix + connection;
                            AdventureSurfaceViewReferences line = instance.Control;
                            _connections.Add(line);
                        }

                        AdventureSurfaceViewReferences edge = _connections[connection++];
                        edge.Rect.gameObject.SetActive(true);
                        edge.Image.color = rows[row].Nodes[index].IsChosen && rows[row + 1].Nodes[next].IsChosen
                            ? _foreground : _controlHighlight;
                        Vector2 from = new Vector2(
                            rect.anchoredPosition.x,
                            _settings.MapPadding + row * _settings.MapRowHeight + _settings.MapNodeHeight);
                        Vector2 to = new Vector2(width * (next + 1) / (rows[row + 1].Nodes.Length + 1),
                            _settings.MapPadding + (row + 1) * _settings.MapRowHeight);
                        Vector2 delta = to - from;
                        edge.Rect.anchorMin = edge.Rect.anchorMax = Vector2.zero;
                        edge.Rect.pivot = new Vector2(0, 0.5f);
                        edge.Rect.anchoredPosition = from;
                        edge.Rect.sizeDelta = new Vector2(delta.magnitude, _settings.ConnectionThickness);
                        edge.Rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                    }
                }
            }

            for (int index = connection; index < _connections.Count; index++)
            {
                _connections[index].Rect.gameObject.SetActive(false);
            }
        }

        private static void PlaceCard(RectTransform rect, float x, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
