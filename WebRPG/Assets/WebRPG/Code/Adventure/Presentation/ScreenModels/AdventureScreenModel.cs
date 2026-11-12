using System;

namespace WebRPG
{
    // 承載冒險畫面的文字、角色、地圖與操作狀態。
    public sealed class AdventureScreenModel
    {
        public string Heading { get; set; }
        public string Summary { get; set; }
        public string Situation { get; set; }
        public string Journal { get; set; }
        public string[] Choices { get; set; }
        public AdventureGaugeScreenModel PlayerHealth { get; set; }
        public AdventureGaugeScreenModel BasicAttack { get; set; }
        public AdventureGaugeScreenModel[] Skills { get; set; }
        public AdventureCharacterScreenModel[] Players { get; set; }
        public AdventureCharacterScreenModel[] Enemies { get; set; }
        public AdventureMapRowScreenModel[] MapRows { get; set; }
        public int MapCurrentRow { get; set; }
        public string MapNotice { get; set; }
        public bool ShowMap { get; set; }
        public bool CanToggleMap { get; set; }
        public string MapButtonText { get; set; }
        public string InspectedCharacterId { get; set; }
        public string InspectedCharacterName { get; set; }
        public AdventureGaugeScreenModel[] InspectedSkills { get; set; }
        public bool ShowBattle { get; set; }
        public float BattleSpeed { get; set; } = 1;
        public AdventureProjectileScreenModel Projectile { get; set; }
        public AdventureIconScreenModel[] Relics { get; set; } = Array.Empty<AdventureIconScreenModel>();
        public bool ShowResultActions { get; set; }
        public bool CanStart { get; set; }
        public bool CanAbandon { get; set; }
        public bool CanChoose { get; set; } = true;
        public bool CanChangeBattleSpeed { get; set; } = true;
    }
}
