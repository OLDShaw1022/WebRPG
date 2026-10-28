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
            SerializedObject serialized = new SerializedObject(prefab);
            SerializedProperty property = serialized.GetIterator();
            while (property.NextVisible(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference)
                {
                    Assert.That(property.objectReferenceValue, Is.Not.Null, property.propertyPath);
                }
            }
            Assert.That(serialized.FindProperty("_toolbar").arraySize, Is.EqualTo(5));
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
