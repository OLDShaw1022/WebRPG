using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WebRPG.Tests
{
    // 驗證登入入口具備可編輯的固定欄位與主場景綁定。
    public sealed class AccountPrefabTests
    {
        [Test]
        public void AccountPrefabHasAllFormReferences()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WebRPG/Prefabs/AccountCanvas.prefab");
            Assert.That(prefab, Is.Not.Null);
            Component view = prefab.GetComponent("AccountView");
            Assert.That(view, Is.Not.Null);
            SerializedObject serialized = new SerializedObject(view);
            foreach (string field in new[] { "_email", "_password", "_username", "_login", "_register", "_status", "_server" })
                Assert.That(serialized.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        }
    }
}
