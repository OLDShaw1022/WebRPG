using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace WebRPG.Tests
{
    // 驗證開始挑戰後進入地圖，並套用設定的初始生命值。
    public sealed class AdventureStartupTests
    {
        [Test]
        public void StartingChallengeEntersMapWithConfiguredHealth()
        {
            Type modelType = Type.GetType("WebRPG.AdventureModel, WebRPG");
            Assert.That(modelType, Is.Not.Null, "Adventure model has not been implemented.");
            Type configurationType = Type.GetType("WebRPG.AdventureConfiguration, WebRPG");
            Assert.That(configurationType, Is.Not.Null, "Adventure configuration has not been implemented.");
            string json = File.ReadAllText(Path.Combine(Application.dataPath, "WebRPG/Data/AdventurePrototype.json"));
            object configuration = JsonConvert.DeserializeObject(json, configurationType);
            object model = Activator.CreateInstance(modelType, configuration, 17);
            modelType.GetMethod("StartRun", BindingFlags.Instance | BindingFlags.Public).Invoke(model, null);
            Assert.That(modelType.GetProperty("Phase").GetValue(model).ToString(), Is.EqualTo("Map"));
            Assert.That(modelType.GetProperty("Health").GetValue(model),
                Is.EqualTo(configurationType.GetProperty("StartingHealth").GetValue(configuration)));
        }
    }
}
