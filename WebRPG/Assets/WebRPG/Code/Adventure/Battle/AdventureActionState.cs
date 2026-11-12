namespace WebRPG
{
    // 保存角色目前唯一的施放動作及其進度。
    public sealed class AdventureActionState
    {
        public AdventureActionState(string name, float castTime, int skillIndex = -1, bool isSpecial = false)
        {
            Name = name;
            CastTime = castTime;
            RemainingCastTime = castTime;
            SkillIndex = skillIndex;
            IsSpecial = isSpecial;
        }

        public string Name { get; }
        public float CastTime { get; }
        public float RemainingCastTime { get; internal set; }
        public int SkillIndex { get; }
        public bool IsSpecial { get; }
    }
}
