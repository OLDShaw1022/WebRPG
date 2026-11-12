using System;
using TMPro;
using UnityEngine;

namespace WebRPG
{
    // 保存數值條的矩形、填色區域與文字參照。
    [Serializable]
    public sealed class AdventureGaugeViewReferences
    {
        public RectTransform Rect;
        public RectTransform Fill;
        public TextMeshProUGUI Value;
    }
}
