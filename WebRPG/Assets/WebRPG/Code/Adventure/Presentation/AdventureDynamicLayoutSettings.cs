using System;

namespace WebRPG
{
    // 定義角色、技能與地圖動態排列使用的尺寸和間距。
    [Serializable]
    public sealed class AdventureDynamicLayoutSettings
    {
        public float CharacterWidth = 176;
        public float CharacterHeight = 170;
        public float CharacterSpacing = 16;
        public float CharacterPadding = 16;
        public float FactionGap = 64;
        public float SkillWidth = 88;
        public float SkillHeight = 112;
        public float RelicWidth = 88;
        public float RelicHeight = 56;
        public float RelicSpacing = 8;
        public float SkillSpacing = 12;
        public float SkillPadding = 8;
        public float MapRowHeight = 144;
        public float MapNodeHeight = 104;
        public float MapPadding = 32;
        public float MapNodeMinimumWidth = 128;
        public float MapNodeMaximumWidth = 200;
        public float ConnectionThickness = 2;
    }
}
