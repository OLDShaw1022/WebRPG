namespace WebRPG
{
    // 定義敵人的基本攻擊與專屬特殊技能。
    public sealed class EnemyDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Health { get; set; }
        public int Damage { get; set; }
        public float CastTime { get; set; } = 3;
        public EnemySkillDefinition SpecialSkill { get; set; }
    }
}
