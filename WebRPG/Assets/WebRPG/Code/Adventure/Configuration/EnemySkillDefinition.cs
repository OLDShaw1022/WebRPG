namespace WebRPG
{
    // 定義敵人特殊技能的施放、冷卻與普攻循環。
    public sealed class EnemySkillDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public float CastTime { get; set; }
        public float Cooldown { get; set; }
        public int BasicAttacksBeforeUse { get; set; }
        public EffectDefinition[] Effects { get; set; }
    }
}
