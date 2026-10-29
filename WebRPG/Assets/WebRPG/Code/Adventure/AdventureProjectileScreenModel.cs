namespace WebRPG
{
    // 承載投射物的角色識別、動作名稱與飛行進度快照。
    public sealed class AdventureProjectileScreenModel
    {
        public string SourceCharacterId { get; set; }
        public string TargetCharacterId { get; set; }
        public string Name { get; set; }
        public float Progress { get; set; }
    }
}
