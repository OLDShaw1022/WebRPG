using System;
using UnityEngine;

namespace WebRPG
{
    internal static class AdventureUiHelper
    {
        public static void BindClick(AdventureButtonViewReferences control, Action clicked)
        {
            control.Button.onClick.AddListener(() => clicked());
        }

        public static void BindActor(AdventureActorViewReferences actor, Action<string> focus)
        {
            BindClick(actor.Hit, () => focus(actor.Id));
            AddTrigger(actor.Trigger, UnityEngine.EventSystems.EventTriggerType.PointerEnter, () => focus(actor.Id));
            AddTrigger(actor.Trigger, UnityEngine.EventSystems.EventTriggerType.PointerExit, () => focus(null));
            AddTrigger(actor.Trigger, UnityEngine.EventSystems.EventTriggerType.Select, () => focus(actor.Id));
            AddTrigger(actor.Trigger, UnityEngine.EventSystems.EventTriggerType.Deselect, () => focus(null));
        }

        private static void AddTrigger(UnityEngine.EventSystems.EventTrigger trigger,
            UnityEngine.EventSystems.EventTriggerType type, Action action)
        {
            UnityEngine.EventSystems.EventTrigger.Entry entry = new UnityEngine.EventSystems.EventTrigger.Entry
            {
                eventID = type
            };
            entry.callback.AddListener(data => action());
            trigger.triggers.Add(entry);
        }


        public static void SetGauge(AdventureGaugeViewReferences control, AdventureGaugeScreenModel model, bool includeTitle)
        {
            float amount = float.IsNaN(model.FillAmount) ? 0 : Mathf.Clamp01(model.FillAmount);
            control.Fill.gameObject.SetActive(amount > 0);
            control.Fill.anchorMax = new Vector2(amount, 1);
            control.Value.text = includeTitle ? model.Title + " 繚 " + model.Text : model.Text;
        }


        public static void SetEnabled(AdventureButtonViewReferences control, bool enabled, Color foreground, Color secondary)
        {
            control.Button.interactable = enabled;
            control.Label.color = enabled ? foreground : secondary;
        }

    }
}
