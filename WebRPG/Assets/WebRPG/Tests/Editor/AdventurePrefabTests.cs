using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WebRPG.Tests
{
    // 驗證冒險 Prefab 的元件參照、固定版面與動態地圖重用。
    public sealed class AdventurePrefabTests
    {
        [Test]
        public void PrefabContainsFixedScreensAndCompleteReferencesBeforePlaying()
        {
            AdventureView prefab = AssetDatabase.LoadAssetAtPath<AdventureView>(
                "Assets/WebRPG/Prefabs/AdventureCanvas.prefab");
            foreach (string name in new[] { "LocalPlayerHud", "BattleScreen", "Actions", "InspectedSkills", "MapScreen", "Details", "Templates" })
            {
                Assert.That(prefab.transform.Find(name), Is.Not.Null, name);
            }
            foreach (string path in new[] { "PauseMenu/MenuPanel/Title", "PauseMenu/SettingsPanel/Title", "PauseMenu/ConfirmReturn/Title" })
            {
                TMPro.TextMeshProUGUI title = prefab.transform.Find(path).GetComponent<TMPro.TextMeshProUGUI>();
                Assert.That(title.GetPreferredValues().y, Is.LessThanOrEqualTo(title.rectTransform.rect.height), path);
            }
            SerializedObject serialized = new SerializedObject(prefab);
            SerializedProperty property = serialized.GetIterator();
            while (property.NextVisible(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference)
                {
                    Assert.That(property.objectReferenceValue, Is.Not.Null, property.propertyPath);
                }
            }
            Assert.That(serialized.FindProperty("_toolbar").arraySize, Is.EqualTo(2));
            Assert.That(serialized.FindProperty("_players").arraySize, Is.EqualTo(4));
            Assert.That(serialized.FindProperty("_enemies").arraySize, Is.EqualTo(4));
            Assert.That(serialized.FindProperty("_skills").arraySize, Is.EqualTo(7));
            Assert.That(serialized.FindProperty("_choices").arraySize, Is.EqualTo(3));
        }

        [Test]
        public void AwakeBindsExistingControlsWithoutCreatingFixedHierarchy()
        {
            AdventureView prefab = AssetDatabase.LoadAssetAtPath<AdventureView>(
                "Assets/WebRPG/Prefabs/AdventureCanvas.prefab");
            AdventureView instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                int before = instance.GetComponentsInChildren<Transform>(true).Length;
                typeof(AdventureView).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(instance, null);
                Assert.That(instance.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(before));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance.gameObject);
            }
        }

        [Test]
        public void FullWidthHeaderContainsAdjacentToolbarAndLeavesRoomForRelicsAndJournal()
        {
            AdventureView instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<AdventureView>(
                "Assets/WebRPG/Prefabs/AdventureCanvas.prefab"));
            try
            {
                instance.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                RectTransform canvas = (RectTransform)instance.transform;
                canvas.sizeDelta = new Vector2(1280, 720);
                Canvas.ForceUpdateCanvases();
                RectTransform header = instance.transform.Find("LocalPlayerHud") as RectTransform;
                RectTransform menu = header.Find("Menu") as RectTransform;
                RectTransform map = header.Find("MapToggle") as RectTransform;
                RectTransform relics = instance.transform.Find("Relics") as RectTransform;
                RectTransform journal = instance.transform.Find("Details") as RectTransform;
                Assert.That(header.rect.width, Is.EqualTo(canvas.rect.width));
                Assert.That(header.offsetMin.x, Is.Zero);
                Assert.That(header.offsetMax.x, Is.Zero);
                Assert.That(menu.parent, Is.SameAs(header));
                Assert.That(map.parent, Is.SameAs(header));
                Assert.That(menu.anchoredPosition.y, Is.EqualTo(map.anchoredPosition.y));
                Assert.That(menu.anchoredPosition.x - map.anchoredPosition.x, Is.EqualTo(map.rect.width + 12));
                Assert.That(relics.parent, Is.SameAs(instance.transform));
                Vector3[] headerCorners = new Vector3[4];
                Vector3[] relicCorners = new Vector3[4];
                Vector3[] journalCorners = new Vector3[4];
                header.GetWorldCorners(headerCorners);
                relics.GetWorldCorners(relicCorners);
                journal.GetWorldCorners(journalCorners);
                Assert.That(relicCorners[1].y, Is.LessThan(headerCorners[0].y));
                Assert.That(journalCorners[1].y, Is.LessThan(headerCorners[0].y));
                Assert.That(journalCorners[1].y, Is.EqualTo(relicCorners[1].y).Within(.01f));
                typeof(AdventureView).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(instance, null);
                AdventureConfiguration configuration = JsonConvert.DeserializeObject<AdventureConfiguration>(
                    File.ReadAllText(Path.Combine(Application.dataPath, "WebRPG/Data/AdventurePrototype.json")));
                using (AdventurePresenter presenter = new AdventurePresenter(new AdventureModel(configuration, 1), instance))
                {
                    string summary = header.Find("LocalSummary").GetComponent<TMPro.TextMeshProUGUI>().text;
                    Assert.That(summary, Does.StartWith("本機玩家 · 生命 "));
                    Assert.That(summary, Does.EndWith(" 層"));
                    Assert.That(summary, Does.Not.Contain("攻擊"));
                    Assert.That(summary, Does.Not.Contain("技能"));
                    Assert.That(summary, Does.Not.Contain("遺物"));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance.gameObject);
            }
        }

        [Test]
        public void RenderingPreservesEditorAuthoredFixedLayoutAndFontSize()
        {
            AdventureView prefab = AssetDatabase.LoadAssetAtPath<AdventureView>(
                "Assets/WebRPG/Prefabs/AdventureCanvas.prefab");
            AdventureView instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                RectTransform header = instance.transform.Find("LocalPlayerHud") as RectTransform;
                Assert.That(header, Is.Not.Null);
                header.offsetMin = new Vector2(27, -211);
                header.offsetMax = new Vector2(-31, -19);
                Component heading = instance.transform.Find("LocalPlayerHud/Heading")
                    .GetComponents<Component>().Single(item => item.GetType().Name == "TextMeshProUGUI");
                SerializedObject text = new SerializedObject(heading);
                text.FindProperty("m_fontSize").floatValue = 33;
                text.ApplyModifiedPropertiesWithoutUndo();
                typeof(AdventureView).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(instance, null);
                AdventureConfiguration configuration = JsonConvert.DeserializeObject<AdventureConfiguration>(
                    File.ReadAllText(Path.Combine(Application.dataPath, "WebRPG/Data/AdventurePrototype.json")));
                using (AdventurePresenter presenter = new AdventurePresenter(new AdventureModel(configuration, 17), instance))
                {
                    Assert.That(header.offsetMin, Is.EqualTo(new Vector2(27, -211)));
                    Assert.That(header.offsetMax, Is.EqualTo(new Vector2(-31, -19)));
                    text.Update();
                    Assert.That(text.FindProperty("m_fontSize").floatValue, Is.EqualTo(33));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance.gameObject);
            }
        }

        [Test]
        public void CentralMenuPausesSinglePlayerAndRequiresConfirmationToReturn()
        {
            AdventureView prefab = AssetDatabase.LoadAssetAtPath<AdventureView>(
                "Assets/WebRPG/Prefabs/AdventureCanvas.prefab");
            AdventureView instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                typeof(AdventureView).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(instance, null);
                typeof(AdventureMenuView).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(instance.Menu, null);
                AdventureConfiguration configuration = JsonConvert.DeserializeObject<AdventureConfiguration>(
                    File.ReadAllText(Path.Combine(Application.dataPath, "WebRPG/Data/AdventurePrototype.json")));
                AdventureModel model = new AdventureModel(configuration, 1);
                model.StartRun();
                model.SelectNode(0);
                int returned = 0;
                using (AdventurePresenter presenter = new AdventurePresenter(model, instance))
                using (AdventureMenuPresenter menu = new AdventureMenuPresenter(new AdventureMenuModel(true),
                    instance.Menu, presenter.SetPaused, instance.RequestAbandon, instance.RequestStart, () => returned++))
                {
                    instance.MenuRequested += menu.Open;
                    instance.transform.Find("LocalPlayerHud/Menu").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    Assert.That(instance.transform.Find("PauseMenu").gameObject.activeSelf, Is.True);
                    float cooldown = model.RemainingBasicAttackCooldown;
                    presenter.Tick(.5f);
                    Assert.That(model.RemainingBasicAttackCooldown, Is.EqualTo(cooldown));
                    instance.transform.Find("PauseMenu/MenuPanel/Settings").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    Assert.That(instance.transform.Find("PauseMenu/SettingsPanel").gameObject.activeSelf, Is.True);
                    instance.transform.Find("PauseMenu/SettingsPanel/Back").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    instance.transform.Find("PauseMenu/MenuPanel/ReturnToMainMenu").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    Assert.That(returned, Is.Zero);
                    instance.transform.Find("PauseMenu/ConfirmReturn/Cancel").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    Assert.That(returned, Is.Zero);
                    Assert.That(model.Phase, Is.EqualTo(AdventurePhase.Battle));
                    instance.transform.Find("PauseMenu/MenuPanel/Resume").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    presenter.Tick(.1f);
                    Assert.That(model.RemainingBasicAttackCooldown, Is.LessThan(cooldown));
                    menu.Open();
                    instance.transform.Find("PauseMenu/MenuPanel/ReturnToMainMenu").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    instance.transform.Find("PauseMenu/ConfirmReturn/Confirm").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    Assert.That(returned, Is.EqualTo(1));
                    Assert.That(instance.transform.Find("Details").gameObject.activeSelf, Is.True);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance.gameObject);
            }
        }

        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        public void AllActorsAndIconsFitWithoutScrollingAndKeepFixedSizes(int width, int height)
        {
            AdventureView instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<AdventureView>(
                "Assets/WebRPG/Prefabs/AdventureCanvas.prefab"));
            try
            {
                instance.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                ((RectTransform)instance.transform).sizeDelta = new Vector2(width, height);
                Canvas.ForceUpdateCanvases();
                AdventureGaugeScreenModel gauge = new AdventureGaugeScreenModel
                {
                    Title = "鐵劍斬擊 Lv.2", DisplayName = "鐵劍斬擊", Text = "倒數 1.0 秒", CompactText = "1.0秒",
                    IconId = "slash", Description = "傷害20", FillAmount = .5f
                };
                AdventureCharacterScreenModel actor = new AdventureCharacterScreenModel
                {
                    Id = "test", Name = "測試角色", Health = gauge
                };
                AdventureScreenModel screen = new AdventureScreenModel
                {
                    PlayerHealth = gauge, Summary = "測試摘要", Journal = "測試紀錄", Situation = string.Empty,
                    Choices = Array.Empty<string>(), ShowBattle = true,
                    Players = Enumerable.Repeat(actor, 4).ToArray(), Enemies = Enumerable.Repeat(actor, 4).ToArray(),
                    InspectedSkills = Enumerable.Repeat(gauge, 7).ToArray(), InspectedCharacterName = "本機玩家",
                    Relics = Enumerable.Range(0, 3).Select(index => new AdventureIconScreenModel
                    {
                        Id = "heart", Title = "石心護符", Description = "生命上限+30"
                    }).ToArray()
                };
                typeof(AdventureView).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(instance, null);
                instance.Render(screen);
                Canvas.ForceUpdateCanvases();
                RectTransform arena = instance.transform.Find("BattleScreen") as RectTransform;
                RectTransform versus = arena.Find("Versus") as RectTransform;
                Assert.That(versus.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(versus.anchorMin, Is.EqualTo(new Vector2(.5f, .5f)));
                Rect visibleBounds = arena.rect;
                visibleBounds.min -= Vector2.one * .01f;
                visibleBounds.max += Vector2.one * .01f;
                foreach (Transform card in arena.Find("CharacterRow/Viewport/Content"))
                {
                    RectTransform rect = card as RectTransform;
                    Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(176, 170)));
                    Vector3[] corners = new Vector3[4];
                    rect.GetWorldCorners(corners);
                    foreach (Vector3 corner in corners)
                    {
                        Assert.That(visibleBounds.Contains(arena.InverseTransformPoint(corner)), Is.True, card.name);
                    }
                }
                foreach (string path in new[] { "BattleScreen", "InspectedSkills", "Relics" })
                {
                    Assert.That(instance.transform.Find(path).GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true)
                        .All(scroll => !scroll.enabled), Is.True, path);
                }
                foreach (Transform skill in instance.transform.Find("InspectedSkills/SkillRow/Viewport/Content"))
                {
                    Assert.That(((RectTransform)skill).sizeDelta, Is.EqualTo(new Vector2(88, 112)));
                    TMPro.TextMeshProUGUI name = skill.Find("SkillName").GetComponent<TMPro.TextMeshProUGUI>();
                    Assert.That(name.gameObject.activeInHierarchy, Is.True);
                    Assert.That(name.text, Is.EqualTo("鐵劍斬擊"));
                    Assert.That(name.GetPreferredValues().x, Is.LessThanOrEqualTo(name.rectTransform.rect.width));
                    Assert.That(name.GetPreferredValues().y, Is.LessThanOrEqualTo(name.rectTransform.rect.height));
                    UnityEngine.UI.Image placeholder = skill.Find("Icon").GetComponent<UnityEngine.UI.Image>();
                    Assert.That(placeholder, Is.Not.Null);
                    Assert.That(placeholder.sprite, Is.Null);
                    Assert.That(placeholder.preserveAspect, Is.True);
                    TMPro.TextMeshProUGUI value = skill.Find("Cooldown/Value").GetComponent<TMPro.TextMeshProUGUI>();
                    Assert.That(value.GetPreferredValues().y, Is.LessThanOrEqualTo(value.rectTransform.rect.height), skill.name);
                }
                Transform relics = instance.transform.Find("Relics/Label");
                AdventureIconItemView[] relicIcons = relics.GetComponentsInChildren<AdventureIconItemView>();
                Assert.That(relicIcons, Has.Length.EqualTo(3));
                foreach (AdventureIconItemView relic in relicIcons)
                {
                    Assert.That(relic.Rect.sizeDelta, Is.EqualTo(new Vector2(104, 80)));
                    TMPro.TextMeshProUGUI name = relic.transform.Find("Label").GetComponent<TMPro.TextMeshProUGUI>();
                    Assert.That(name.gameObject.activeInHierarchy, Is.True);
                    Assert.That(name.text, Is.EqualTo("石心護符"));
                    Assert.That(name.GetPreferredValues().x, Is.LessThanOrEqualTo(name.rectTransform.rect.width));
                    Assert.That(name.GetPreferredValues().y, Is.LessThanOrEqualTo(name.rectTransform.rect.height));
                    Assert.That(relic.transform.Find("Icon").GetComponent<UnityEngine.UI.Image>().sprite, Is.Null);
                }
                AdventureTooltipTargetView target = relicIcons[0].GetComponent<AdventureTooltipTargetView>();
                target.OnPointerEnter(null);
                Assert.That(instance.transform.Find("TooltipHost/Tooltip").gameObject.activeSelf, Is.True);
                Assert.That(instance.transform.Find("TooltipHost/Tooltip/Description").GetComponent<TMPro.TextMeshProUGUI>().text,
                    Is.EqualTo("生命上限+30"));
                target.OnPointerExit(null);
                Assert.That(instance.transform.Find("TooltipHost/Tooltip").gameObject.activeSelf, Is.False);
                AdventureTooltipTargetView skillTarget = instance.transform.Find("InspectedSkills/SkillRow/Viewport/Content/Skill0")
                    .GetComponent<AdventureTooltipTargetView>();
                skillTarget.OnSelect(null);
                Assert.That(instance.transform.Find("TooltipHost/Tooltip/Description").GetComponent<TMPro.TextMeshProUGUI>().text,
                    Is.EqualTo("傷害20"));
                skillTarget.OnDeselect(null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance.gameObject);
            }
        }

        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        public void TooltipFollowsPointerAndStaysWithinAllScreenEdges(int width, int height)
        {
            AdventureView instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<AdventureView>(
                "Assets/WebRPG/Prefabs/AdventureCanvas.prefab"));
            try
            {
                instance.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                RectTransform root = (RectTransform)instance.transform;
                root.sizeDelta = new Vector2(width, height);
                Canvas.ForceUpdateCanvases();
                AdventureTooltipTargetView target = instance.transform.Find("InspectedSkills/SkillRow/Viewport/Content/Skill0")
                    .GetComponent<AdventureTooltipTargetView>();
                AdventureTooltipView tooltip = instance.transform.Find("TooltipHost").GetComponent<AdventureTooltipView>();
                RectTransform panel = instance.transform.Find("TooltipHost/Tooltip") as RectTransform;
                target.SetContent("技能", "測試說明", "倒數 1.0 秒");
                UnityEngine.EventSystems.PointerEventData pointer = new UnityEngine.EventSystems.PointerEventData(null);
                Vector2 center = root.rect.center;
                pointer.position = RectTransformUtility.WorldToScreenPoint(null, root.TransformPoint(center));
                target.OnPointerEnter(pointer);
                Vector3 before = root.InverseTransformPoint(panel.position);
                pointer.position = RectTransformUtility.WorldToScreenPoint(null, root.TransformPoint(center + new Vector2(20, 30)));
                typeof(AdventureTooltipView).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(tooltip, null);
                Vector3 after = root.InverseTransformPoint(panel.position);
                Assert.That(after.x - before.x, Is.EqualTo(20).Within(.01f));
                Assert.That(after.y - before.y, Is.EqualTo(30).Within(.01f));
                Rect bounds = root.rect;
                foreach (Vector2 point in new[] { bounds.min + Vector2.one, bounds.max - Vector2.one,
                    new Vector2(bounds.xMin + 1, bounds.yMax - 1), new Vector2(bounds.xMax - 1, bounds.yMin + 1) })
                {
                    pointer.position = RectTransformUtility.WorldToScreenPoint(null, root.TransformPoint(point));
                    target.OnPointerMove(pointer);
                    Vector3[] corners = new Vector3[4];
                    panel.GetWorldCorners(corners);
                    foreach (Vector3 corner in corners)
                    {
                        Vector3 local = root.InverseTransformPoint(corner);
                        Assert.That(local.x, Is.InRange(bounds.xMin, bounds.xMax));
                        Assert.That(local.y, Is.InRange(bounds.yMin, bounds.yMax));
                    }
                }
                target.OnPointerExit(pointer);
                Assert.That(panel.gameObject.activeSelf, Is.False);
                target.OnSelect(null);
                Assert.That(panel.gameObject.activeSelf, Is.True);
                Vector3 keyboardPosition = panel.position;
                pointer.position += new Vector2(50, 50);
                typeof(AdventureTooltipView).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(tooltip, null);
                Assert.That(panel.position, Is.EqualTo(keyboardPosition));
                target.OnDeselect(null);
                Assert.That(panel.gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance.gameObject);
            }
        }

        [Test]
        public void MenuOverlaysExistingAdventureWithOneTranslucentBackdrop()
        {
            AdventureView instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<AdventureView>(
                "Assets/WebRPG/Prefabs/AdventureCanvas.prefab"));
            try
            {
                Transform[] underlay = new[] { instance.transform.Find("LocalPlayerHud"), instance.transform.Find("Relics"),
                    instance.transform.Find("BattleScreen"), instance.transform.Find("MapScreen"), instance.transform.Find("Details") };
                bool[] active = underlay.Select(item => item.gameObject.activeSelf).ToArray();
                instance.transform.Find("TooltipHost").SetAsLastSibling();
                instance.Menu.Render(AdventureMenuScreen.Menu);
                Transform overlay = instance.transform.Find("PauseMenu");
                Assert.That(overlay.GetSiblingIndex(), Is.EqualTo(instance.transform.childCount - 1));
                UnityEngine.UI.Image backdrop = overlay.GetComponent<UnityEngine.UI.Image>();
                Assert.That(backdrop.color.a, Is.InRange(.4f, .8f));
                Assert.That(backdrop.raycastTarget, Is.True);
                Assert.That(overlay.GetComponent<UnityEngine.UI.Outline>().enabled, Is.False);
                Assert.That(underlay.Select(item => item.gameObject.activeSelf).ToArray(), Is.EqualTo(active));
                instance.Menu.Render(AdventureMenuScreen.Settings);
                Assert.That(overlay.GetSiblingIndex(), Is.EqualTo(instance.transform.childCount - 1));
                Assert.That(underlay.Select(item => item.gameObject.activeSelf).ToArray(), Is.EqualTo(active));
                instance.Menu.Render(AdventureMenuScreen.Hidden);
                Assert.That(overlay.gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance.gameObject);
            }
        }

        [Test]
        public void CatalogSpriteCanReplaceAndRestoreSquarePlaceholder()
        {
            AdventureView instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<AdventureView>(
                "Assets/WebRPG/Prefabs/AdventureCanvas.prefab"));
            AdventureIconSpriteCatalog catalog = UnityEngine.Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<AdventureIconSpriteCatalog>("Assets/WebRPG/Data/AdventureIconSprites.asset"));
            try
            {
                Sprite replacement = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/WebRPG/Arts/UI/FantasyUIBorder.png");
                Assert.That(replacement, Is.Not.Null);
                SerializedObject data = new SerializedObject(catalog);
                SerializedProperty entries = data.FindProperty("_entries");
                for (int index = 0; index < entries.arraySize; index++)
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                    if (entry.FindPropertyRelative("Id").stringValue == "slash")
                    {
                        entry.FindPropertyRelative("Sprite").objectReferenceValue = replacement;
                    }
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                AdventureIconView icon = instance.transform.Find("InspectedSkills/SkillRow/Viewport/Content/Skill0/Icon")
                    .GetComponent<AdventureIconView>();
                SerializedObject control = new SerializedObject(icon);
                control.FindProperty("_catalog").objectReferenceValue = catalog;
                control.ApplyModifiedPropertiesWithoutUndo();
                icon.SetIcon("slash");
                UnityEngine.UI.Image image = icon.GetComponent<UnityEngine.UI.Image>();
                Assert.That(image.sprite, Is.SameAs(replacement));
                icon.SetIcon("empty");
                Assert.That(image.sprite, Is.Null);
                Assert.That(image.preserveAspect, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance.gameObject);
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void MapUsesEditableTemplateAndReusesNodesOnReplay()
        {
            AdventureView prefab = AssetDatabase.LoadAssetAtPath<AdventureView>(
                "Assets/WebRPG/Prefabs/AdventureCanvas.prefab");
            AdventureView instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                Transform template = instance.transform.Find("Templates/MapNodeTemplate");
                Assert.That(template, Is.Not.Null);
                Component label = template.Find("Label").GetComponents<Component>()
                    .Single(item => item.GetType().Name == "TextMeshProUGUI");
                SerializedObject text = new SerializedObject(label);
                text.FindProperty("m_fontSize").floatValue = 25;
                text.ApplyModifiedPropertiesWithoutUndo();
                typeof(AdventureView).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(instance, null);
                AdventureConfiguration configuration = JsonConvert.DeserializeObject<AdventureConfiguration>(
                    File.ReadAllText(Path.Combine(Application.dataPath, "WebRPG/Data/AdventurePrototype.json")));
                AdventureModel model = new AdventureModel(configuration, 17);
                model.StartRun();
                Transform nodes = instance.transform.Find("MapScreen/NodeMap/Viewport/Content/Nodes");
                using (AdventurePresenter presenter = new AdventurePresenter(model, instance))
                {
                    Assert.That(nodes.childCount, Is.GreaterThan(0));
                    foreach (Transform node in nodes)
                    {
                        Component nodeLabel = node.Find("Label").GetComponents<Component>()
                            .Single(item => item.GetType().Name == "TextMeshProUGUI");
                        Assert.That(new SerializedObject(nodeLabel).FindProperty("m_fontSize").floatValue,
                            Is.EqualTo(25));
                    }
                }
                int count = nodes.childCount;
                model.AbandonRun();
                model.StartRun();
                using (AdventurePresenter presenter = new AdventurePresenter(model, instance))
                {
                    Assert.That(nodes.childCount, Is.EqualTo(count));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance.gameObject);
            }
        }
    }
}
