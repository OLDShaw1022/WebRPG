namespace WebRPG
{
    // 承載角色身分、存活狀態、生命值與技能畫面資料。
    public sealed class AdventureCharacterScreenModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool IsLocalPlayer { get; set; }
        public bool IsDefeated { get; set; }
        public bool IsLocked { get; set; }
        public AdventureGaugeScreenModel Action { get; set; } = new AdventureGaugeScreenModel();
        public string ActionDescription { get; set; } = string.Empty;
        public string SpecialIntent { get; set; } = string.Empty;
        public AdventureGaugeScreenModel Health { get; set; }
        public AdventureGaugeScreenModel BasicAttack { get; set; }
        public AdventureGaugeScreenModel[] Skills { get; set; }
    }
}
