using System.Collections.Generic;
using System.Reflection;
using WebRPGShared;
using NUnit.Framework;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace WebRPG.Tests
{
    // 驗證正式伺服器設定與不需輸入網址的選單。
    public sealed class LobbyServerSelectionTests
    {
        private const string ConfigurationJson =
            "{\"DefaultServerId\":\"aws-ec2\",\"Servers\":[{\"Id\":\"aws-ec2\",\"Name\":\"AWS EC2\"," +
            "\"Url\":\"wss://ws.oldshaw-webrpg.com/ws?channel=lobby\"}],\"RequestTimeoutSeconds\":10}";

        [Test]
        public void ServerCatalogConfigurationIsAccepted()
        {
            LobbyClientConfiguration configuration = JsonConvert.DeserializeObject<LobbyClientConfiguration>(
                ConfigurationJson);
            Assert.DoesNotThrow(configuration.Validate);
        }

        [Test]
        public void PlayerBuildHidesEditorServersAndRejectsInsecureProduction()
        {
            LobbyClientConfiguration configuration = JsonConvert.DeserializeObject<LobbyClientConfiguration>(
                ConfigurationJson);
            configuration.Servers = new[]
            {
                configuration.Servers[0],
                new LobbyServerConfiguration { Id = "local", Name = "本機測試",
                    Url = "ws://localhost:8080/ws?channel=lobby", EditorOnly = true }
            };
            LobbyServerConfiguration[] playerServers = configuration.GetAvailableServers(false);
            Assert.That(playerServers.Length, Is.EqualTo(1));
            Assert.That(playerServers[0].Id, Is.EqualTo(configuration.DefaultServerId));
            Assert.That(configuration.GetAvailableServers(true).Length, Is.EqualTo(2));
            configuration.Servers[0].Url = "ws://example.com/ws?channel=lobby";
            Assert.Throws<System.InvalidOperationException>(configuration.Validate);
        }

        [TestCase("other")]
        [TestCase("")]
        [TestCase(null)]
        public void UnrelatedErrorKeepsLobbyRequestPending(string requestId)
        {
            LobbyModel model = new LobbyModel(
                JsonConvert.DeserializeObject<LobbyClientConfiguration>(ConfigurationJson));
            typeof(LobbyModel).GetField("_pendingId", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(model, "lobby");
            typeof(LobbyModel).GetProperty(nameof(LobbyModel.IsBusy)).SetValue(model, true);
            string status = model.Status;
            int changes = 0;
            model.Changed += () => changes++;
            typeof(LobbyModel).GetMethod("HandlePacket", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(model,
                new object[] {
                    JsonConvert.SerializeObject(
                    new LobbyPacket { Type = LobbyPacketType.Error, RequestId = requestId, Error = "rejected" }) });
            Assert.That(model.IsBusy, Is.True);
            Assert.That(model.Status, Is.EqualTo(status));
            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void MatchingErrorCompletesLobbyRequest()
        {
            LobbyModel model = new LobbyModel(
                JsonConvert.DeserializeObject<LobbyClientConfiguration>(ConfigurationJson));
            typeof(LobbyModel).GetField("_pendingId", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(model, "lobby");
            typeof(LobbyModel).GetProperty(nameof(LobbyModel.IsBusy)).SetValue(model, true);
            typeof(LobbyModel).GetMethod("HandlePacket", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(model,
                new object[] {
                    JsonConvert.SerializeObject(
                    new LobbyPacket { Type = LobbyPacketType.Error, RequestId = "lobby", Error = "rejected" }) });
            Assert.That(model.IsBusy, Is.False);
            Assert.That(model.Status, Is.EqualTo("rejected"));
        }

        [TestCase("self", 0)]
        [TestCase("peer", 1)]
        public void AdventureReplyOnlyCompletesSendingPlayersRequest(string playerId, int remaining)
        {
            LobbyModel model = new LobbyModel(
                JsonConvert.DeserializeObject<LobbyClientConfiguration>(ConfigurationJson));
            typeof(LobbyModel).GetProperty(nameof(LobbyModel.PlayerId)).SetValue(model, "self");
            var requests = (Dictionary<string, string>)typeof(LobbyModel)
                .GetField("_adventureRequests", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(model);
            requests.Add("1", "room");
            int received = 0;
            model.AdventureReceived += packet => received++;
            typeof(LobbyModel).GetMethod("HandlePacket", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(model,
                new object[] { JsonConvert.SerializeObject(new LobbyPacket
                {
                    Type = LobbyPacketType.Adventure,
                    Adventure = new AdventurePacket
                    {
                        Type = AdventurePacketType.Command, PlayerId = playerId, RequestId = "1", RoomId = "room"
                    }
                }) });
            Assert.That(requests.Count, Is.EqualTo(remaining));
            Assert.That(received, Is.EqualTo(1));
        }

        [Test]
        public void AdventureErrorIsRoutedOnceWithoutCompletingLobbyRequest()
        {
            LobbyModel model = new LobbyModel(
                JsonConvert.DeserializeObject<LobbyClientConfiguration>(ConfigurationJson));
            typeof(LobbyModel).GetField("_pendingId", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(model, "lobby");
            typeof(LobbyModel).GetProperty(nameof(LobbyModel.IsBusy)).SetValue(model, true);
            var requests = (Dictionary<string, string>)typeof(LobbyModel)
                .GetField("_adventureRequests", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(model);
            requests.Add("adventure", "original-room");
            string status = model.Status;
            int changes = 0;
            int received = 0;
            AdventurePacket error = null;
            model.Changed += () => changes++;
            model.AdventureReceived += packet => { error = packet; received++; };
            string json = JsonConvert.SerializeObject(new LobbyPacket
            {
                Type = LobbyPacketType.Error, RequestId = "adventure", Error = "rejected"
            });
            var handle = typeof(LobbyModel).GetMethod("HandlePacket", BindingFlags.Instance | BindingFlags.NonPublic);
            handle.Invoke(model, new object[] { json });
            handle.Invoke(model, new object[] { json });
            Assert.That(model.IsBusy, Is.True);
            Assert.That(model.Status, Is.EqualTo(status));
            Assert.That(changes, Is.Zero);
            Assert.That(received, Is.EqualTo(1));
            Assert.That(requests, Is.Empty);
            Assert.That(error.Type, Is.EqualTo(AdventurePacketType.Error));
            Assert.That(error.RequestId, Is.EqualTo("adventure"));
            Assert.That(error.RoomId, Is.EqualTo("original-room"));
            Assert.That(error.Error, Is.EqualTo("rejected"));
        }

        [Test]
        public void LobbyOmitsManualConnectionControls()
        {
            LobbyView view = AssetDatabase.LoadAssetAtPath<LobbyView>("Assets/WebRPG/Prefabs/LobbyCanvas.prefab");
            Transform browsePage = view.transform.Find("Lobby/BrowsePage");
            Assert.That(browsePage, Is.Not.Null);
            Assert.That(browsePage.Find("ServerDropdown"), Is.Null);
            Assert.That(browsePage.Find("ServerLabel"), Is.Null);
            Assert.That(browsePage.Find("Connect"), Is.Null);
        }
    }
}
