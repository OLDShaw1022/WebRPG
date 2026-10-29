using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace WebRPG.Tests
{
    // 建立固定戰鬥情境，並以反射讓未實作的新介面產生測試失敗而非編譯錯誤。
    internal static class AdventureBattleTestHelper
    {
        public static AdventureConfiguration Configuration()
        {
            AdventureConfiguration configuration = JsonConvert.DeserializeObject<AdventureConfiguration>(
                File.ReadAllText(Path.Combine(Application.dataPath, "WebRPG/Data/AdventurePrototype.json"),
                    Encoding.UTF8));
            configuration.StartingHealth = 1000;
            configuration.SimulationStep = 0.125f;
            configuration.BattleTimeLimit = 120;
            configuration.BasicAttack.Damage = 6;
            Populate(configuration, "{'ProjectileTravelTime':0.5}");
            Populate(configuration.BasicAttack, "{'CastTime':2}");
            foreach (SkillDefinition skill in configuration.Skills)
            {
                skill.Cooldown = 4;
                skill.Effects = new[]
                {
                    new EffectDefinition { Type = AdventureEffectType.Damage, Amount = 20 }
                };
                Populate(skill, "{'CastTime':3}");
            }
            foreach (EnemyDefinition enemy in configuration.Enemies)
            {
                enemy.Health = 100;
                enemy.Damage = 3;
                Populate(enemy, "{'CastTime':10,'SpecialSkill':null}");
            }
            EnemyGroupDefinition group = new EnemyGroupDefinition
            {
                Id = "battle-flow-test",
                EnemyIds = configuration.Enemies.Take(3).Select(enemy => enemy.Id).ToArray()
            };
            configuration.EnemyGroups = configuration.EnemyGroups.Concat(new[] { group }).ToArray();
            configuration.Floors[0].Rows[0].NodePool[0].EnemyGroupPool = new[] { group.Id };
            return configuration;
        }

        public static AdventureModel StartBattle(AdventureConfiguration configuration = null)
        {
            AdventureModel model = new AdventureModel(configuration ?? Configuration(), 17);
            model.StartRun();
            model.SelectNode(0);
            Assert.That(model.Phase, Is.EqualTo(AdventurePhase.Battle));
            return model;
        }

        public static void Populate(object target, string json)
        {
            JsonConvert.PopulateObject(json, target);
        }

        public static T Read<T>(object target, string name)
        {
            Assert.That(target, Is.Not.Null, name + " 的來源物件不應為空。");
            PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, "尚未實作公開屬性：" + name);
            return (T)property.GetValue(target);
        }

        public static object Invoke(object target, string name, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public,
                null, arguments.Select(argument => argument.GetType()).ToArray(), null);
            Assert.That(method, Is.Not.Null, "尚未實作公開方法：" + name);
            return method.Invoke(target, arguments);
        }

        public static float PlayerRemaining(AdventureModel model)
        {
            return Read<float>(Read<object>(model, "PlayerAction"), "RemainingCastTime");
        }
    }
}
