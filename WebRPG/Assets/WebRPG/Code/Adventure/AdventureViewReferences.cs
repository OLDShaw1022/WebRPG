using System;
using TMPro;
using UnityEngine;

namespace WebRPG
{
    // 保存介面面板的矩形、圖片與外框參照。
    [Serializable]
    public sealed class AdventureSurfaceViewReferences
    {
        public RectTransform Rect;
        public UnityEngine.UI.Image Image;
        public UnityEngine.UI.Outline Outline;
    }

    // 保存介面按鈕的矩形、文字與外框參照。
    [Serializable]
    public sealed class AdventureButtonViewReferences
    {
        public RectTransform Rect;
        public UnityEngine.UI.Button Button;
        public TextMeshProUGUI Label;
        public UnityEngine.UI.Outline Outline;
    }

    // 保存角色控制項參照及角色身分資訊。
    [Serializable]
    public sealed class AdventureActorViewReferences
    {
        public AdventureButtonViewReferences Hit;
        public TextMeshProUGUI Badge;
        public TextMeshProUGUI Name;
        public AdventureGaugeViewReferences Health;
        public AdventureGaugeViewReferences Cast;
        public AdventureTooltipTargetView Tooltip;
        public UnityEngine.EventSystems.EventTrigger Trigger;
        public bool IsPlayer;

        [NonSerialized]
        public string Id;
    }

    // 保存技能卡片的矩形、標題與數值條參照。
    [Serializable]
    public sealed class AdventureSkillViewReferences
    {
        public RectTransform Rect;
        public TextMeshProUGUI Title;
        public AdventureGaugeViewReferences Gauge;
        public UnityEngine.UI.Button Button;
        public AdventureIconView Icon;
        public AdventureTooltipTargetView Tooltip;
    }

    // 保存數值條的矩形、填色區域與文字參照。
    [Serializable]
    public sealed class AdventureGaugeViewReferences
    {
        public RectTransform Rect;
        public RectTransform Fill;
        public TextMeshProUGUI Value;
    }

    // 保存不捲動區域的可見矩形與動態內容參照。
    [Serializable]
    public sealed class AdventureContentViewReferences
    {
        public RectTransform Rect;
        public RectTransform Content;
    }

    // 保存捲動區域的矩形、視窗、內容與捲動元件參照。
    [Serializable]
    public sealed class AdventureScrollViewReferences
    {
        public RectTransform Rect;
        public RectTransform Viewport;
        public RectTransform Content;
        public UnityEngine.UI.ScrollRect Scroll;
    }

    // 保存地圖節點控制項及其選取與顯示狀態。
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
