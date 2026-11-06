using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using System.Linq;
using TMPro;
using WebRPGShared;

namespace WebRPG.Tests
{
    // 驗證多人入口與大廳固定版面均已序列化供編輯器調整。
    public sealed class LobbyPrefabTests
    {
        [Test]
        public void FourMaximumLengthMembersFitTheRoomPanel()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WebRPG/Prefabs/LobbyCanvas.prefab");
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                TestLobbyModel model = new TestLobbyModel
                {
                    Room = new LobbyRoomSnapshot
                    {
                        Id = new string('a', 32), Name = new string('房', 40), LeaderId = "self",
                        Members = Enumerable.Range(0, 4).Select(index => new LobbyMemberSnapshot
                        {
                            PlayerId = index == 0 ? "self" : index.ToString(),
                            Username = new string('隊', 23) + index, IsReady = false
                        }).ToArray()
                    }
                };
                using (LobbyPresenter presenter = new LobbyPresenter(model, instance.GetComponent<LobbyView>(), () => { }))
                {
                    Assert.That(instance.transform.Find("Lobby/BrowsePage").gameObject.activeSelf, Is.False);
                    Assert.That(instance.transform.Find("Lobby/RoomPage").gameObject.activeSelf, Is.True);
                    Canvas.ForceUpdateCanvases();
                    TextMeshProUGUI members = instance.transform.Find("Lobby/RoomPage/Members").GetComponent<TextMeshProUGUI>();
                    members.ForceMeshUpdate(true, true);
                    Assert.That(members.isTextOverflowing, Is.False, "四名成員與最長房名必須完整顯示");
                }
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void LobbyPrefabAndMainReferencesExist()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WebRPG/Prefabs/LobbyCanvas.prefab");
            Assert.That(prefab, Is.Not.Null, "多人房間大廳 Prefab 尚未實作");
            Component view = prefab.GetComponent("LobbyView");
            Assert.That(view, Is.Not.Null);
            SerializedObject serialized = new SerializedObject(view);
            foreach (string field in new[] { "_username", "_roomName", "_roomId",
                "_createButton", "_joinButton", "_refreshButton", "_leaveButton", "_readyButton",
                "_backButton", "_status", "_members", "_rooms", "_publicToggle", "_browsePage", "_roomPage", "_roomChat" })
                Assert.That(serialized.FindProperty(field).objectReferenceValue, Is.Not.Null, field);

            MainMenuView menu = AssetDatabase.LoadAssetAtPath<MainMenuView>("Assets/WebRPG/Prefabs/MainMenuCanvas.prefab");
            Assert.That(new SerializedObject(menu).FindProperty("_multiplayerButton").objectReferenceValue, Is.Not.Null);
        }
    }
}
