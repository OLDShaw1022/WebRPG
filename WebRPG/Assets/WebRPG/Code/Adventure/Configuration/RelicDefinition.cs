namespace WebRPG
{
    // 定義遺物識別、名稱與被動效果。
    public sealed class RelicDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public EffectDefinition[] Effects { get; set; }
    }
}
