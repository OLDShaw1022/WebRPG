using System;
using TMPro;

namespace WebRPG
{
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
}
