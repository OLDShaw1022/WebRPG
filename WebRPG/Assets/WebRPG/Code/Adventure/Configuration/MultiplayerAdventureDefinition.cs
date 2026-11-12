namespace WebRPG
{
    // 定義組隊敵人強化與戰勝復活的可調整原型參數。
    public sealed class MultiplayerAdventureDefinition
    {
        public float EnemyHealthPerAdditionalPlayer { get; set; } = 1;
        public float EnemyDamagePerAdditionalPlayer { get; set; }
        public float NetworkTimeoutSeconds { get; set; } = 10;
        public float RevivalHealthFraction { get; set; } = 0.2f;
    }
}
