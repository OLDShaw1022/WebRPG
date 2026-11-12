using System;
using TMPro;
using UnityEngine;

namespace WebRPG
{
    // 保存介面按鈕的矩形、文字與外框參照。
    [Serializable]
    public sealed class AdventureButtonViewReferences
    {
        public RectTransform Rect;
        public UnityEngine.UI.Button Button;
        public TextMeshProUGUI Label;
        public UnityEngine.UI.Outline Outline;
    }
}
