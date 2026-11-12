namespace WebRPG
{
    // 記錄冒險技能的設定、目前等級與剩餘冷卻時間。
    public sealed class AdventureSkillState
    {
        public AdventureSkillState(SkillDefinition definition)
        {
            Definition = definition;
            RemainingCooldown = 0;
        }

        public SkillDefinition Definition { get; }
        public int Level { get; internal set; } = 1;
        public float RemainingCooldown { get; internal set; }
    }
}
