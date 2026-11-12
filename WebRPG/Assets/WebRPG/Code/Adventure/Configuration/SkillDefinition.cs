namespace WebRPG
{
    // 定義技能識別、名稱、冷卻、等級上限與效果。
    public sealed class SkillDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public float Cooldown { get; set; }
        public float CastTime { get; set; } = 3;
        public int MaximumLevel { get; set; }
        public EffectDefinition[] Effects { get; set; }
    }
}
