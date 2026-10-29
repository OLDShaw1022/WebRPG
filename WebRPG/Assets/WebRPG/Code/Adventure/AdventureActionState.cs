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

    // 保存投射物的固定目標、待結算效果與飛行進度。
    public sealed class AdventureProjectileState
    {
        internal AdventureProjectileState(long id, int sourceEnemyIndex, int targetEnemyIndex,
            string name, int damage, int healing, float duration, bool isBasicAttack, bool isSpecial)
        {
            Id = id;
            SourceEnemyIndex = sourceEnemyIndex;
            TargetEnemyIndex = targetEnemyIndex;
            Name = name;
            Damage = damage;
            Healing = healing;
            Duration = duration;
            RemainingTime = duration;
            IsBasicAttack = isBasicAttack;
            IsSpecial = isSpecial;
        }

        public long Id { get; }
        public int SourceEnemyIndex { get; }
        public int TargetEnemyIndex { get; }
        public string Name { get; }
        public int Damage { get; }
        public int Healing { get; }
        public float Duration { get; }
        public float RemainingTime { get; internal set; }
        public bool IsBasicAttack { get; }
        public bool IsSpecial { get; }
    }
}
