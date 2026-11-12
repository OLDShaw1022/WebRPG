using System;
using UnityEngine;

namespace WebRPG
{
    // 提供冒險介面的事件綁定、數值條更新與按鈕狀態設定。
    internal static class AdventureUiHelper
    {
        private const string GaugeTitleSeparator = " · ";

        public static void BindClick(AdventureButtonViewReferences control, Action clicked)
        {
            control.Button.onClick.AddListener(() => clicked());
        }

        public static void BindActor(AdventureActorViewReferences actor, Action<string> focus)
        {
            BindClick(actor.Hit, () => focus(actor.Id));
        }

        public static void SetGauge(AdventureGaugeViewReferences control, AdventureGaugeScreenModel model, bool includeTitle)
        {
            float amount = float.IsNaN(model.FillAmount) ? 0 : Mathf.Clamp01(model.FillAmount);
            control.Fill.gameObject.SetActive(amount > 0);
            control.Fill.anchorMax = new Vector2(amount, 1);
            control.Value.text = includeTitle ? model.Title + GaugeTitleSeparator + model.Text : model.Text;
        }


        public static AdventureActorViewReferences FindActor(AdventureActorViewReferences[] players,
            AdventureActorViewReferences[] enemies, string characterId)
        {
            foreach (AdventureActorViewReferences actor in players)
            {
                if (actor.Id == characterId) return actor;
            }
            foreach (AdventureActorViewReferences actor in enemies)
            {
                if (actor.Id == characterId) return actor;
            }
            return null;
        }

        public static void RenderSkills(AdventureSkillViewReferences[] controls, AdventureGaugeScreenModel[] skills)
        {
            for (int index = 0; index < controls.Length; index++)
            {
                bool visible = index < skills.Length;
                controls[index].Rect.gameObject.SetActive(visible);
                if (visible)
                {
                    controls[index].Title.text = skills[index].DisplayName;
                    AdventureGaugeScreenModel skill = skills[index];
                    AdventureUiHelper.SetGauge(controls[index].Gauge, skill, false);
                    controls[index].Gauge.Value.text = skill.CompactText;
                    controls[index].Icon.SetIcon(skill.IconId);
                    controls[index].Tooltip.SetContent(skill.Title, skill.Description, skill.Text);
                    controls[index].Button.interactable = skill.CanUse;
                }
            }
        }

        public static void RenderChoices(AdventureButtonViewReferences[] controls, string[] choices, bool canChoose)
        {
            for (int index = 0; index < controls.Length; index++)
            {
                bool visible = index < choices.Length;
                controls[index].Rect.gameObject.SetActive(visible);
                if (visible)
                {
                    controls[index].Label.text = choices[index];
                    controls[index].Button.interactable = canChoose;
                }
            }
        }

        public static void SetEnabled(AdventureButtonViewReferences control, bool enabled, Color foreground, Color secondary)
        {
            control.Button.interactable = enabled;
            control.Label.color = enabled ? foreground : secondary;
        }

    }
}
