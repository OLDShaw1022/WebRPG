using System;
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
}
