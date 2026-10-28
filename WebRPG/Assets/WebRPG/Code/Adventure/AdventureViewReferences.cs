// 定義冒險介面各類控制項的元件參照與地圖節點顯示狀態。
using System;
using TMPro;
using UnityEngine;

namespace WebRPG
{
    [Serializable]
    public sealed class AdventureSurfaceViewReferences
    {
        public RectTransform Rect;
        public UnityEngine.UI.Image Image;
        public UnityEngine.UI.Outline Outline;
    }

    [Serializable]
    public sealed class AdventureButtonViewReferences
    {
        public RectTransform Rect;
        public UnityEngine.UI.Button Button;
        public TextMeshProUGUI Label;
        public UnityEngine.UI.Outline Outline;
    }

    [Serializable]
    public sealed class AdventureActorViewReferences
    {
        public AdventureButtonViewReferences Hit;
        public TextMeshProUGUI Badge;
        public TextMeshProUGUI Name;
        public AdventureGaugeViewReferences Health;
        public UnityEngine.EventSystems.EventTrigger Trigger;
        public bool IsPlayer;

        [NonSerialized]
        public string Id;
    }

    [Serializable]
    public sealed class AdventureSkillViewReferences
    {
        public RectTransform Rect;
        public TextMeshProUGUI Title;
        public AdventureGaugeViewReferences Gauge;
    }

    [Serializable]
    public sealed class AdventureGaugeViewReferences
    {
        public RectTransform Rect;
        public RectTransform Fill;
        public TextMeshProUGUI Value;
    }

    [Serializable]
    public sealed class AdventureScrollViewReferences
    {
        public RectTransform Rect;
        public RectTransform Viewport;
        public RectTransform Content;
        public UnityEngine.UI.ScrollRect Scroll;
    }

    internal sealed class AdventureMapNodeViewState
    {
        public AdventureMapNodeViewState(AdventureButtonViewReferences control)
        {
            Control = control;
        }

        public AdventureButtonViewReferences Control { get; }
        public int ChoiceIndex { get; set; }
        public bool HasState { get; set; }
        public bool CanSelect { get; set; }
        public bool IsChosen { get; set; }
    }
}
