using System;
using System.Linq;

namespace WebRPG
{
    // 將戰鬥施放、技能冷卻與敵人意圖轉為唯讀畫面資料。
    internal static class AdventureBattleScreenHelper
    {
        private const string BasicAttackName = "普攻";
        private const string AttackIconId = "attack";
        private const string IdleText = "待戰";
        private const string DefeatedText = "已擊倒";
        private const string ReadyText = "可施放";
        private const string CastingText = "施放中";
        private const string WaitingText = "施放技能中";
        private const string WaitingCompactText = "技能中";
        private const string CastFormat = "{0} {1:0.0}秒";
        private const string CooldownFormat = "冷卻 {0:0.0} 秒";
        private const string CompactCooldownFormat = "{0:0.0}秒";
        private const string SkillTitleFormat = "{0} Lv.{1}";
        private const string TimingFormat = "施放 {0:0.0} 秒 · 冷卻 {1:0.0} 秒";
        private const string BasicDescriptionFormat = "傷害 {0} · 施放 {1:0.0} 秒 · 無冷卻";
        private const string CurrentActionFormat = "正在施放「{0}」\n{1}\n剩餘 {2:0.0} 秒";
        private const string ProjectileFormat = "「{0}」飛行中，命中後結算。";
        private const string DamageFormat = "造成 {0} 點傷害";
        private const string HealingFormat = "自身回復 {0} 點生命";
        private const string EffectSeparator = "、";
        private const string NewLine = "\n";
        private const string IntentFormat = "----------------\n特殊技能預告\n再完成 {0} 次普攻且冷卻結束後施放「{1}」\n{2}\n施放 {3:0.0} 秒 · 冷卻剩餘 {4:0.0} 秒";
        private const string CastingSpecialFormat = "----------------\n特殊技能施放中\n「{0}」完成後，再經過 {1} 次普攻且冷卻結束後使用。";
        private const string NoSpecialText = "----------------\n沒有特殊技能。";

        public static AdventureGaugeScreenModel CastGauge(AdventureActionState action, bool defeated = false)
        {
            bool active = action != null && !defeated;
            return new AdventureGaugeScreenModel
            {
                Title = active ? action.Name : string.Empty,
                DisplayName = active ? action.Name : string.Empty,
                Text = defeated ? DefeatedText : active
                    ? string.Format(CastFormat, action.Name, action.RemainingCastTime) : IdleText,
                CompactText = active ? string.Format(CompactCooldownFormat, action.RemainingCastTime) : IdleText,
                FillAmount = active ? Math.Max(0, Math.Min(1, 1 - action.RemainingCastTime / action.CastTime)) : 0
            };
        }

        public static AdventureGaugeScreenModel BasicAttackGauge(int damage, float castTime,
            AdventureActionState action, bool fighting, bool defeated = false)
        {
            AdventureGaugeScreenModel gauge = CastGauge(fighting && action != null && action.SkillIndex < 0 &&
                !action.IsSpecial ? action : null, defeated);
            gauge.Title = string.Format(BasicDescriptionFormat, damage, castTime);
            gauge.IconId = AttackIconId;
            gauge.DisplayName = BasicAttackName;
            gauge.Description = gauge.Title;
            if (fighting && action != null && action.SkillIndex >= 0)
            {
                gauge.Text = WaitingText;
                gauge.CompactText = WaitingCompactText;
            }
            return gauge;
        }

        public static AdventureGaugeScreenModel SkillGauge(AdventureSkillState skill, bool fighting,
            AdventureActionState action, int index, bool canUse)
        {
            bool casting = fighting && action != null && action.SkillIndex == index;
            float remaining = Math.Max(0, skill.RemainingCooldown);
            return new AdventureGaugeScreenModel
            {
                Title = string.Format(SkillTitleFormat, skill.Definition.Name, skill.Level),
                DisplayName = skill.Definition.Name,
                IconId = skill.Definition.Id,
                Description = string.Format(TimingFormat, skill.Definition.CastTime, skill.Definition.Cooldown) +
                    NewLine + Effects(skill.Definition.Effects, skill.Level),
                Text = !fighting ? IdleText : casting ? CastingText : remaining > 0
                    ? string.Format(CooldownFormat, remaining) : ReadyText,
                CompactText = !fighting ? IdleText : casting ? CastingText : remaining > 0
                    ? string.Format(CompactCooldownFormat, remaining) : ReadyText,
                FillAmount = !fighting ? 0 : remaining > 0
                    ? Math.Max(0, Math.Min(1, 1 - remaining / skill.Definition.Cooldown)) : 1,
                CanUse = canUse
            };
        }

        public static string EnemyAction(AdventureEnemyState enemy, AdventureProjectileState projectile, int index)
        {
            if (enemy.Health == 0)
            {
                return DefeatedText;
            }
            if (projectile != null && projectile.SourceEnemyIndex == index)
            {
                return string.Format(ProjectileFormat, projectile.Name);
            }
            AdventureActionState action = enemy.Action;
            string effects = action.IsSpecial ? Effects(enemy.Definition.SpecialSkill.Effects, 1)
                : string.Format(DamageFormat, enemy.Definition.Damage);
            return string.Format(CurrentActionFormat, action.Name, effects, action.RemainingCastTime);
        }

        public static string EnemyIntent(AdventureEnemyState enemy)
        {
            if (enemy.Health == 0)
            {
                return string.Empty;
            }
            EnemySkillDefinition special = enemy.Definition.SpecialSkill;
            if (special == null)
            {
                return NoSpecialText;
            }
            if (enemy.Action.IsSpecial)
            {
                return string.Format(CastingSpecialFormat, special.Name, special.BasicAttacksBeforeUse);
            }
            return string.Format(IntentFormat, enemy.BasicAttacksUntilSpecial, special.Name,
                Effects(special.Effects, 1), special.CastTime, enemy.RemainingSpecialCooldown);
        }

        public static string Effects(EffectDefinition[] effects, int level)
        {
            return string.Join(EffectSeparator, effects.Select(effect => string.Format(
                effect.Type == AdventureEffectType.Damage ? DamageFormat : HealingFormat,
                effect.Amount + effect.PerLevel * (level - 1))));
        }
    }
}
