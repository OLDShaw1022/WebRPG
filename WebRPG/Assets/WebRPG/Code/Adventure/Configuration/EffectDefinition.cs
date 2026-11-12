namespace WebRPG
{
    // 定義效果種類、基礎數值與每級成長量。
    public sealed class EffectDefinition
    {
        public AdventureEffectType Type { get; set; }
        public int Amount { get; set; }
        public int PerLevel { get; set; }
    }
}
