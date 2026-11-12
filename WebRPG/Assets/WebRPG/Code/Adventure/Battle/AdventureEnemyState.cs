namespace WebRPG
{
    // 記錄敵人的生命、施放動作與特殊技能循環。
    public sealed class AdventureEnemyState
    {
        public AdventureEnemyState(EnemyDefinition definition)
        {
            Definition = definition;
            Health = definition.Health;
            BasicAttacksUntilSpecial = definition.SpecialSkill?.BasicAttacksBeforeUse ?? 0;
        }

        public EnemyDefinition Definition { get; }
        public int Health { get; internal set; }
        public AdventureActionState Action { get; internal set; }
        public int BasicAttacksUntilSpecial { get; internal set; }
        public float RemainingSpecialCooldown { get; internal set; }
    }
}
