using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace WebRPG
{
    [Serializable]
    public sealed class AdventureDynamicLayoutSettings
    {
        public float NarrowWidth = 1100;
        public float CharacterWidth = 176;
        public float NarrowCharacterWidth = 144;
        public float CharacterHeight = 214;
        public float CharacterSpacing = 16;
        public float CharacterPadding = 16;
        public float FactionGap = 40;
        public float SkillWidth = 160;
        public float NarrowSkillWidth = 144;
        public float SkillHeight = 80;
        public float SkillSpacing = 12;
        public float SkillPadding = 8;
        public float MapRowHeight = 144;
        public float MapNodeHeight = 104;
        public float MapPadding = 32;
        public float MapNodeMinimumWidth = 128;
        public float MapNodeMaximumWidth = 200;
        public float ConnectionThickness = 2;
    }

    internal sealed class AdventureDynamicLayoutHelper
    {
        private const string ConnectionNamePrefix = "Connection";

        private readonly RectTransform _root;
        private readonly AdventureScrollViewReferences _actorScroll;
        private readonly AdventureActorViewReferences[] _players;
        private readonly AdventureActorViewReferences[] _enemies;
        private readonly TextMeshProUGUI _versus;
        private readonly AdventureScrollViewReferences _skillScroll;
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

        public AdventureDynamicLayoutHelper(RectTransform root, AdventureScrollViewReferences actorScroll,
            AdventureActorViewReferences[] players, AdventureActorViewReferences[] enemies, TextMeshProUGUI versus,
            AdventureScrollViewReferences skillScroll, AdventureSkillViewReferences[] skills,
            AdventureScrollViewReferences mapScroll, RectTransform mapLines,
            List<List<AdventureMapNodeViewState>> mapRows, List<AdventureSurfaceViewReferences> connections,
            AdventureSurfaceTemplateView mapConnectionTemplate, AdventureDynamicLayoutSettings settings,
            Color foreground, Color controlHighlight)
        {
            _root = root;
            _actorScroll = actorScroll;
            _players = players;
            _enemies = enemies;
            _versus = versus;
            _skillScroll = skillScroll;
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
            bool narrow = _root.rect.width < _settings.NarrowWidth;
            LayoutCharacters(narrow ? _settings.NarrowCharacterWidth : _settings.CharacterWidth);
            LayoutSkills(narrow ? _settings.NarrowSkillWidth : _settings.SkillWidth);
            LayoutMap();
        }

        public void UpdateMap(AdventureScreenModel screen)
        {
            _screen = screen;
            LayoutMap();
        }

        private void LayoutCharacters(float width)
        {
            int playerCount = _screen.Players.Length;
            int enemyCount = _screen.Enemies.Length;
            int count = playerCount + enemyCount;
            float extra = playerCount > 0 && enemyCount > 0 ? _settings.FactionGap : 0;
            float group = count * width + Math.Max(0, count - 1) * _settings.CharacterSpacing + extra;
            float contentWidth = Mathf.Max(_actorScroll.Viewport.rect.width, group + 2 * _settings.CharacterPadding);
            SetHorizontalContent(_actorScroll.Content, contentWidth);
            float start = (contentWidth - group) / 2;
            for (int index = 0; index < playerCount; index++)
            {
                PlaceCard(_players[index].Hit.Rect, start + width / 2 + index * (width + _settings.CharacterSpacing), width, _settings.CharacterHeight);
            }

            for (int index = 0; index < enemyCount; index++)
            {
                float x = start + (playerCount + index) * (width + _settings.CharacterSpacing) + extra + width / 2;
                PlaceCard(_enemies[index].Hit.Rect, x, width, _settings.CharacterHeight);
            }

            _versus.gameObject.SetActive(extra > 0);
            float separator = start + playerCount * width + Math.Max(0, playerCount - 1) * _settings.CharacterSpacing + _settings.CharacterSpacing + extra / 2 - 8;
            PlaceCard(_versus.rectTransform, separator, 44, 36);
        }

        private void LayoutSkills(float width)
        {
            int count = _screen.InspectedSkills.Length;
            float group = count * width + Math.Max(0, count - 1) * _settings.SkillSpacing;
            float contentWidth = Mathf.Max(_skillScroll.Viewport.rect.width, group + 2 * _settings.SkillPadding);
            SetHorizontalContent(_skillScroll.Content, contentWidth);
            float start = (contentWidth - group) / 2;
            for (int index = 0; index < count; index++)
            {
                PlaceCard(_skills[index].Rect, start + width / 2 + index * (width + _settings.SkillSpacing), width, _settings.SkillHeight);
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
            float height = Math.Max(0, rows.Length - 1) * _settings.MapRowHeight + _settings.MapNodeHeight + 2 * _settings.MapPadding;
            _mapScroll.Content.anchorMin = new Vector2(0, 1);
            _mapScroll.Content.anchorMax = new Vector2(1, 1);
            _mapScroll.Content.pivot = new Vector2(0, 1);
            _mapScroll.Content.sizeDelta = new Vector2(0, height);
            float nodeWidth = Mathf.Clamp(width / 4 - 16, _settings.MapNodeMinimumWidth, _settings.MapNodeMaximumWidth);
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
                        Vector2 from = new Vector2(rect.anchoredPosition.x, _settings.MapPadding + row * _settings.MapRowHeight + _settings.MapNodeHeight);
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
    }
}
