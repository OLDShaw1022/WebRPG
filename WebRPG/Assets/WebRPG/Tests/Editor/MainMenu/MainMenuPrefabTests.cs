using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WebRPG.Tests
{
    // 驗證主選單 Prefab 已包含必要控制項與畫面元件參照。
    public sealed class MainMenuPrefabTests
    {

        private const string MainMenuPrefabPath = "Assets/WebRPG/Prefabs/MainMenuCanvas.prefab";
        private const string OfferTemplatePath = "OfferTemplate";
        private const string IconName = "Icon";
        private const string OfferName = "Name";
        private const string CostName = "Cost";
        private const string DialogName = "Dialog";

        [Test]
        public void MenuControlsAndViewReferencesExistBeforePlaying()
        {
            MainMenuView view = AssetDatabase.LoadAssetAtPath<MainMenuView>(
                MainMenuPrefabPath);
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

        [Test]
        public void ShopHasSeparateHistoryNavigation()
        {
            MainMenuView menu = AssetDatabase.LoadAssetAtPath<MainMenuView>(MainMenuPrefabPath);
            var shop = new SerializedObject(menu.Shop);
            SerializedProperty history = shop.FindProperty("_history");
            Assert.That(history, Is.Not.Null);
            Assert.That(history.objectReferenceValue, Is.TypeOf<UnityEngine.UI.Button>());
            var button = (UnityEngine.UI.Button)history.objectReferenceValue;
            Assert.That(button.gameObject.activeSelf, Is.False);

            SerializedProperty title = shop.FindProperty("_title");
            Assert.That(title, Is.Not.Null);
            Assert.That(title.objectReferenceValue, Is.Not.Null);
        }

        [Test]
        public void ShopHasScrollableCardsAndAnInitiallyHiddenPurchaseDialog()
        {
            MainMenuView menu = AssetDatabase.LoadAssetAtPath<MainMenuView>(MainMenuPrefabPath);
            ShopView shop = menu.Shop;
            Assert.That(shop, Is.Not.Null);

            var serializedShop = new SerializedObject(shop);
            foreach (string field in new[] { "_offersScroll", "_offerTemplate", "_dialog", "_dialogTitle",
                "_dialogMessage", "_confirm", "_dismiss" })
            {
                SerializedProperty property = serializedShop.FindProperty(field);
                Assert.That(property, Is.Not.Null, field);
                Assert.That(property.objectReferenceValue, Is.Not.Null, field);
            }

            Transform template = shop.transform.Find(OfferTemplatePath);
            Assert.That(template, Is.Not.Null);
            Assert.That(template.gameObject.activeSelf, Is.False);
            Assert.That(template.Find(IconName), Is.Not.Null);
            Assert.That(template.Find(OfferName), Is.Not.Null);
            Assert.That(template.Find(CostName), Is.Not.Null);
            Assert.That(shop.transform.Find(DialogName).gameObject.activeSelf, Is.False);
        }
    }
}
