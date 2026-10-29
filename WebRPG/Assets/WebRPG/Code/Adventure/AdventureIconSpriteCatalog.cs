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

    // 保存共用圖示對照，讓所有槽位依識別碼取得正確圖片。
    public sealed class AdventureIconSpriteCatalog : ScriptableObject
    {
        [SerializeField] private AdventureIconSpriteEntry[] _entries = Array.Empty<AdventureIconSpriteEntry>();

        public Sprite GetSprite(string id)
        {
            foreach (AdventureIconSpriteEntry entry in _entries)
            {
                if (entry.Id == id)
                {
                    return entry.Sprite;
                }
            }
            return null;
        }
    }
}
