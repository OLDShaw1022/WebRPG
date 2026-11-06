using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WebRPG.Tests
{
    // 驗證戰鬥卡片鎖定、施放讀條、行動提示與時間按鈕的實際 Prefab 綁定。
    public sealed class AdventureBattleUiTests
    {
        [Test]
        public void EnemyClickLocksAndHoverKeepsPlayerSkillsVisible()
        {
            AdventureView instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<AdventureView>(
                "Assets/WebRPG/Prefabs/AdventureCanvas.prefab"));
            try
            {
                typeof(AdventureView).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(instance, null);
                AdventureConfiguration configuration = AdventureBattleTestHelper.Configuration();
                configuration.Enemies[2].SpecialSkill = new EnemySkillDefinition
                {
                    Id = "test-heal", Name = "骨骼修補", CastTime = 4, Cooldown = 8,
                    BasicAttacksBeforeUse = 2,
                    Effects = new[] { new EffectDefinition { Type = AdventureEffectType.Heal, Amount = 8 } }
                };
                AdventureModel model = AdventureBattleTestHelper.StartBattle(configuration);
                using (AdventurePresenter presenter = new AdventurePresenter(model, instance))
                {
                    Transform enemy = instance.transform.Find("BattleScreen/CharacterRow/Viewport/Content/Enemy2");
                    enemy.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    Assert.That(model.LockedEnemyIndex, Is.EqualTo(2));
                    UnityEngine.UI.Outline outline = enemy.GetComponent<UnityEngine.UI.Outline>();
                    Color renderedOutline = outline.effectColor * enemy.GetComponent<CanvasRenderer>().GetColor();
                    Assert.That(renderedOutline.r, Is.GreaterThan(0.5f));
                    Assert.That(renderedOutline.r - renderedOutline.g, Is.GreaterThan(0.2f));
                    AdventureTooltipTargetView tooltip = enemy.GetComponent<AdventureTooltipTargetView>();
                    Assert.That(tooltip, Is.Not.Null);
                    tooltip.OnPointerEnter(null);
                    string description = instance.transform.Find("TooltipHost/Tooltip/Description")
                        .GetComponent<TMPro.TextMeshProUGUI>().text;
                    Assert.That(description, Does.Contain("普攻"));
                    TMPro.TextMeshProUGUI intent = instance.transform.Find("TooltipHost/Tooltip/Status")
                        .GetComponent<TMPro.TextMeshProUGUI>();
                    Assert.That(intent.GetPreferredValues(intent.text, intent.rectTransform.rect.width,
                        float.PositiveInfinity).y, Is.LessThanOrEqualTo(intent.rectTransform.rect.height));
                    Transform skills = instance.transform.Find("InspectedSkills/SkillRow/Viewport/Content");
                    Assert.That(skills.GetChild(1).gameObject.activeSelf, Is.True);
                    tooltip.OnPointerExit(null);
                    Assert.That(model.LockedEnemyIndex, Is.EqualTo(2));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance.gameObject);
            }
        }

        [Test]
        public void SkillButtonCancelsBasicAttackAndActorCastShowsSkill()
        {
            AdventureView instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<AdventureView>(
                "Assets/WebRPG/Prefabs/AdventureCanvas.prefab"));
            try
            {
                typeof(AdventureView).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(instance, null);
                AdventureModel model = AdventureBattleTestHelper.StartBattle();
                using (AdventurePresenter presenter = new AdventurePresenter(model, instance))
                {
                    presenter.Tick(0.5f);
                    UnityEngine.UI.Button skill = instance.transform.Find(
                        "InspectedSkills/SkillRow/Viewport/Content/Skill1").GetComponent<UnityEngine.UI.Button>();
                    Assert.That(skill, Is.Not.Null);
                    Assert.That(skill.GetComponents<UnityEngine.UI.Button>(), Has.Length.EqualTo(1));
                    Assert.That(skill.interactable, Is.True);
                    skill.onClick.Invoke();
                    Assert.That(model.PlayerAction.SkillIndex, Is.Zero);
                    Transform cast = instance.transform.Find(
                        "BattleScreen/CharacterRow/Viewport/Content/Player0/Cast/Value");
                    Assert.That(cast, Is.Not.Null);
                    Assert.That(cast.GetComponent<TMPro.TextMeshProUGUI>().text,
                        Does.Contain(model.Skills[0].Definition.Name));
                    Assert.That(skill.interactable, Is.False);
                    TMPro.TextMeshProUGUI basicStatus = instance.transform.Find(
                        "InspectedSkills/SkillRow/Viewport/Content/Skill0/Cooldown/Value")
                        .GetComponent<TMPro.TextMeshProUGUI>();
                    Assert.That(basicStatus.GetPreferredValues().x,
                        Is.LessThanOrEqualTo(basicStatus.rectTransform.rect.width));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance.gameObject);
            }
        }

        [Test]
        public void TimeButtonsControlProjectileMovementAndImpact()
        {
            AdventureView instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<AdventureView>(
                "Assets/WebRPG/Prefabs/AdventureCanvas.prefab"));
            try
            {
                instance.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                ((RectTransform)instance.transform).sizeDelta = new Vector2(1280, 720);
                Canvas.ForceUpdateCanvases();
                typeof(AdventureView).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(instance, null);
                AdventureModel model = AdventureBattleTestHelper.StartBattle();
                using (AdventurePresenter presenter = new AdventurePresenter(model, instance))
                {
                    Transform controls = instance.transform.Find("BattleScreen/TimeControls");
                    Assert.That(controls, Is.Not.Null);
                    Transform projectile = instance.transform.Find("BattleScreen/ProjectileLayer/Projectile");
                    Assert.That(projectile, Is.Not.Null);
                    presenter.Tick(2);
                    Assert.That(projectile.gameObject.activeSelf, Is.True);
                    TMPro.TextMeshProUGUI actionName = projectile.Find("ActionName")
                        .GetComponent<TMPro.TextMeshProUGUI>();
                    Assert.That(actionName.GetPreferredValues().y,
                        Is.LessThanOrEqualTo(actionName.rectTransform.rect.height));
                    Vector3 before = projectile.position;
                    presenter.Tick(0.25f);
                    Assert.That(Vector3.Distance(before, projectile.position), Is.GreaterThan(0));
                    controls.Find("Pause").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    Vector3 paused = projectile.position;
                    presenter.Tick(10);
                    Assert.That(projectile.position, Is.EqualTo(paused));
                    controls.Find("DoubleSpeed").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    presenter.Tick(0.125f);
                    Assert.That(projectile.gameObject.activeSelf, Is.False);
                    Assert.That(model.Enemies[0].Health, Is.EqualTo(94));
                    Assert.That(model.BattleSpeed, Is.EqualTo(2));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance.gameObject);
            }
        }

        [Test]
        public void EveryActorHasCastAndTooltipAndFixedLayoutStaysEditable()
        {
            AdventureView instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<AdventureView>(
                "Assets/WebRPG/Prefabs/AdventureCanvas.prefab"));
            try
            {
                Transform content = instance.transform.Find("BattleScreen/CharacterRow/Viewport/Content");
                foreach (Transform actor in content)
                {
                    Assert.That(actor.Find("Cast/Value"), Is.Not.Null, actor.name);
                    TMPro.TextMeshProUGUI name = actor.Find("CharacterName").GetComponent<TMPro.TextMeshProUGUI>();
                    Assert.That(name.GetPreferredValues().y,
                        Is.LessThanOrEqualTo(name.rectTransform.rect.height), actor.name);
                    Assert.That(actor.GetComponent<AdventureTooltipTargetView>(), Is.Not.Null, actor.name);
                }
                RectTransform cast = content.Find("Player0/Cast") as RectTransform;
                cast.anchoredPosition = new Vector2(3, 18);
                cast.sizeDelta = new Vector2(-20, 26);
                typeof(AdventureView).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(instance, null);
                using (AdventurePresenter presenter = new AdventurePresenter(
                    AdventureBattleTestHelper.StartBattle(), instance))
                {
                    presenter.Tick(0.25f);
                    Assert.That(cast.anchoredPosition, Is.EqualTo(new Vector2(3, 18)));
                    Assert.That(cast.sizeDelta, Is.EqualTo(new Vector2(-20, 26)));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance.gameObject);
            }
        }
    }
}
