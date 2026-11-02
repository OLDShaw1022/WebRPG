using NUnit.Framework;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace WebRPG.Tests
{
    // 驗證正式伺服器設定與不需輸入網址的選單。
    public sealed class LobbyServerSelectionTests
    {
        private const string ConfigurationJson = "{\"DefaultServerId\":\"aws-ec2\",\"Servers\":[{\"Id\":\"aws-ec2\",\"Name\":\"AWS EC2\",\"Url\":\"wss://ws.oldshaw-webrpg.com/ws?channel=lobby\"}],\"RequestTimeoutSeconds\":10}";

        [Test]
        public void ServerCatalogConfigurationIsAccepted()
        {
            LobbyClientConfiguration configuration = JsonConvert.DeserializeObject<LobbyClientConfiguration>(ConfigurationJson);
            Assert.DoesNotThrow(configuration.Validate);
        }

        [Test]
        public void PlayerBuildHidesEditorServersAndRejectsInsecureProduction()
        {
            LobbyClientConfiguration configuration = JsonConvert.DeserializeObject<LobbyClientConfiguration>(ConfigurationJson);
            configuration.Servers = new[]
            {
                configuration.Servers[0],
                new LobbyServerConfiguration { Id = "local", Name = "本機測試", Url = "ws://localhost:8080/ws?channel=lobby", EditorOnly = true }
            };
            LobbyModel player = new LobbyModel(configuration);
            LobbyModel editor = new LobbyModel(configuration, true);
            Assert.That(player.ServerNames, Is.EqualTo(new[] { "AWS EC2" }));
            Assert.That(editor.ServerNames.Length, Is.EqualTo(2));
            Assert.That(player.DefaultServerIndex, Is.Zero);
            configuration.Servers[0].Url = "ws://example.com/ws?channel=lobby";
            Assert.Throws<System.InvalidOperationException>(configuration.Validate);
        }

        [Test]
        public void LobbyUsesServerDropdownInsteadOfAddressInput()
        {
            LobbyView view = AssetDatabase.LoadAssetAtPath<LobbyView>("Assets/WebRPG/Prefabs/LobbyCanvas.prefab");
            SerializedObject serialized = new SerializedObject(view);
            SerializedProperty dropdown = serialized.FindProperty("_serverDropdown");
            Assert.That(dropdown, Is.Not.Null, "尚未實作伺服器選擇清單");
            Assert.That(dropdown.objectReferenceValue, Is.InstanceOf<TMP_Dropdown>());
            Assert.That(serialized.FindProperty("_serverUrl"), Is.Null);
            Assert.That(view.transform.Find("Lobby/ServerUrl"), Is.Null);
        }
    }
}
