using System;
using UnityEngine;

namespace WebRPG
{
    // 保存不捲動區域的可見矩形與動態內容參照。
    [Serializable]
    public sealed class AdventureContentViewReferences
    {
        public RectTransform Rect;
        public RectTransform Content;
    }
}
