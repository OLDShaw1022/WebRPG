namespace WebRPG
{
    // 定義普通攻擊的傷害與施放時間。
    public sealed class BasicAttackDefinition
    {
        public int Damage { get; set; }
        public float CastTime { get; set; } = 2;
    }
}
