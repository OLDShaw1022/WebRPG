using System;
using System.Linq;

namespace WebRPG
{
    // 將隊友的權威模型轉成角色卡資料。
    public static class AdventurePartyScreenHelper
    {
        private const string HealthTitle = "生命";
        private const string HealthFormat = "生命 {0} / {1}";
        public static AdventureCharacterScreenModel[] Characters(MultiplayerAdventureSession session,
            AdventureCharacterScreenModel localPlayer)
        {
            return session.GetMembers().Select((member, index) => member.PlayerId == session.PlayerId ? localPlayer :
                Character(session.Party.Players[index], member.PlayerId, member.Username)).ToArray();
        }

        public static AdventureCharacterScreenModel Character(AdventureModel player, string id, string name)
        {
            bool fighting = player.Phase == AdventurePhase.Battle;
            return new AdventureCharacterScreenModel
            {
                Id = id, Name = name, IsLocalPlayer = false, IsDefeated = player.Health == 0,
                Health = new AdventureGaugeScreenModel
                {
                    Title = HealthTitle, Text = string.Format(HealthFormat, player.Health, player.MaximumHealth),
                    FillAmount = player.MaximumHealth > 0 ? (float)player.Health / player.MaximumHealth : 0
                },
                Action = AdventureBattleScreenHelper.CastGauge(player.PlayerAction, player.Health == 0),
                BasicAttack = AdventureBattleScreenHelper.BasicAttackGauge(player.BasicAttackDamage,
                    player.Configuration.BasicAttack.CastTime, player.PlayerAction, fighting),
                Skills = player.Skills.Select((skill, index) =>
                    AdventureBattleScreenHelper.SkillGauge(skill, fighting, player.PlayerAction, index, false)).ToArray()
            };
        }
    }
}
