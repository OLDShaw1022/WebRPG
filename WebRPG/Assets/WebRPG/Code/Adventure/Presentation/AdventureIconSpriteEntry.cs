using System;
using UnityEngine;

namespace WebRPG
{
    // 保存一個技能或遺物識別碼與可直接拖入的 Sprite。
    [Serializable]
    public sealed class AdventureIconSpriteEntry
    {
        public string Id;
        public Sprite Sprite;
    }
}
