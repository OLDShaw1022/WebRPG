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


        public static void SetEnabled(AdventureButtonViewReferences control, bool enabled, Color foreground, Color secondary)
        {
            control.Button.interactable = enabled;
            control.Label.color = enabled ? foreground : secondary;
        }

    }
}
