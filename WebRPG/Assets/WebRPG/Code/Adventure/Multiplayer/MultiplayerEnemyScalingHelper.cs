using System;
using System.Linq;

namespace WebRPG
{
    // 複製並依 JSON 參數強化組隊敵人，保留原始設定。
    public static class MultiplayerEnemyScalingHelper
    {
        public static EnemyDefinition Scale(EnemyDefinition source, MultiplayerAdventureDefinition settings, int count)
        {
            float healthScale = 1 + (count - 1) * settings.EnemyHealthPerAdditionalPlayer;
            float damageScale = 1 + (count - 1) * settings.EnemyDamagePerAdditionalPlayer;
            EnemySkillDefinition special = source.SpecialSkill;
            return new EnemyDefinition
            {
                Id = source.Id, Name = source.Name,
                Health = Math.Max(1, (int)Math.Ceiling(source.Health * healthScale)),
                Damage = Math.Max(1, (int)Math.Ceiling(source.Damage * damageScale)),
                CastTime = source.CastTime,
                SpecialSkill = special == null ? null : new EnemySkillDefinition
                {
                    Id = special.Id, Name = special.Name, CastTime = special.CastTime, Cooldown = special.Cooldown,
                    BasicAttacksBeforeUse = special.BasicAttacksBeforeUse,
                    Effects = special.Effects.Select(effect => new EffectDefinition
                    {
                        Type = effect.Type, PerLevel = effect.PerLevel,
                        Amount = Math.Max(1, (int)Math.Ceiling(effect.Amount *
                            (effect.Type == AdventureEffectType.Damage ? damageScale : healthScale)))
                    }).ToArray()
                }
            };
        }
    }
}
