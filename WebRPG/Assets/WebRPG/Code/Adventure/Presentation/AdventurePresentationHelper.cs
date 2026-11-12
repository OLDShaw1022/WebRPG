using System;
using System.Collections.Generic;

namespace WebRPG
{
    // 在已確認的同步進度之間平滑顯示，不推進或預測戰鬥模型。
    public sealed class AdventurePresentationHelper
    {
        private const string ActionKeyPrefix = "action:";
        private const string SkillKeyPrefix = "skill:";
        private const string ProjectileKey = "projectile";
        private const string Separator = "|";
        private readonly float _duration;
        private readonly Dictionary<string, ProgressState> _progress = new Dictionary<string, ProgressState>();

        public AdventurePresentationHelper(float duration)
        {
            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0)
                throw new ArgumentOutOfRangeException(nameof(duration));
            _duration = duration;
        }

        public void Apply(AdventureScreenModel screen, float seconds)
        {
            if (!screen.ShowBattle)
            {
                _progress.Clear();
                return;
            }
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0) seconds = 0;
            foreach (AdventureCharacterScreenModel character in screen.Players)
                SmoothAction(character, seconds);
            foreach (AdventureCharacterScreenModel character in screen.Enemies)
                SmoothAction(character, seconds);
            for (int index = 0; index < screen.InspectedSkills.Length; index++)
            {
                AdventureGaugeScreenModel gauge = screen.InspectedSkills[index];
                screen.InspectedSkills[index] = SmoothGauge(
                    SkillKeyPrefix + screen.InspectedCharacterId + Separator + index, gauge, seconds);
            }
            if (screen.Projectile == null)
            {
                _progress.Remove(ProjectileKey);
                return;
            }
            AdventureProjectileScreenModel projectile = screen.Projectile;
            projectile.Progress = Sample(ProjectileKey, projectile.Progress,
                projectile.SourceCharacterId + Separator + projectile.TargetCharacterId + Separator + projectile.Name,
                    seconds);
        }

        private void SmoothAction(AdventureCharacterScreenModel character, float seconds)
        {
            character.Action = SmoothGauge(ActionKeyPrefix + character.Id, character.Action, seconds);
        }

        private AdventureGaugeScreenModel SmoothGauge(string key, AdventureGaugeScreenModel gauge, float seconds)
        {
            return new AdventureGaugeScreenModel
            {
                Title = gauge.Title, Text = gauge.Text, CanUse = gauge.CanUse, IconId = gauge.IconId,
                Description = gauge.Description, CompactText = gauge.CompactText, DisplayName = gauge.DisplayName,
                FillAmount = Sample(key, gauge.FillAmount, gauge.Title + Separator + gauge.IconId, seconds)
            };
        }

        private float Sample(string key, float target, string identity, float seconds)
        {
            if (!_progress.TryGetValue(key, out ProgressState state) || state.Identity != identity
                || target < state.Target)
            {
                _progress[key] = new ProgressState { Identity = identity, Start = target, Target = target,
                    Value = target };
                return target;
            }
            if (target != state.Target)
            {
                state.Start = state.Value;
                state.Target = target;
                state.Elapsed = 0;
            }
            state.Elapsed = Math.Min(_duration, state.Elapsed + seconds);
            state.Value = state.Start + (state.Target - state.Start) * (state.Elapsed / _duration);
            return state.Value;
        }

        // 保存單一視覺進度的插值端點與時間，不持有遊戲模型。
        private sealed class ProgressState
        {
            public string Identity;
            public float Start;
            public float Target;
            public float Value;
            public float Elapsed;
        }
    }
}
