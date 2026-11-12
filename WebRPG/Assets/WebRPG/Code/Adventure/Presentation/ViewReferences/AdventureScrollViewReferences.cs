using System;
using UnityEngine;

namespace WebRPG
{
    // 保存捲動區域的矩形、視窗、內容與捲動元件參照。
    [Serializable]
    public sealed class AdventureScrollViewReferences
    {
        public RectTransform Rect;
        public RectTransform Viewport;
        public RectTransform Content;
        public UnityEngine.UI.ScrollRect Scroll;
    }
}
