using System;
using TMPro;
using UnityEngine;

namespace WebRPG
{
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
}
