using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WebRPG.Tests
{
    public sealed class MainMenuPrefabTests
    {
        [Test]
        public void MenuControlsAndViewReferencesExistBeforePlaying()
        {
            MainMenuView view = AssetDatabase.LoadAssetAtPath<MainMenuView>(
                "Assets/WebRPG/Prefabs/MainMenuCanvas.prefab");
            Assert.That(view, Is.Not.Null);
            SerializedObject serializedView = new SerializedObject(view);
            foreach (string field in new[] { "_singlePlayerButton", "_quitButton", "_message" })
            {
                Assert.That(serializedView.FindProperty(field).objectReferenceValue,
                    Is.Not.Null, field);
            }

            foreach (string name in new[] { "Title", "SinglePlayer", "Multiplayer", "Settings", "Quit", "Message" })
            {
                Assert.That(view.transform.Find("Menu/" + name), Is.Not.Null, name);
            }
        }
    }
}
