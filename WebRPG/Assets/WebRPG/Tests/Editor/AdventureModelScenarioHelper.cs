using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace WebRPG.Tests
{
    // 提供冒險進度與獎勵回歸測試所需的設定和戰鬥情境。
    internal static class AdventureModelScenarioHelper
    {
        public static AdventureConfiguration LoadConfiguration()
        {
            return JsonConvert.DeserializeObject<AdventureConfiguration>(File.ReadAllText(
                Path.Combine(Application.dataPath, "WebRPG/Data/AdventurePrototype.json"), Encoding.UTF8));
        }

        public static AdventureConfiguration WinningConfiguration()
        {
            AdventureConfiguration configuration = LoadConfiguration();
            configuration.BasicAttack.CastTime = 0.125f;
            configuration.ProjectileTravelTime = 0.125f;
            foreach (EnemyDefinition enemy in configuration.Enemies)
            {
                enemy.Health = 1;
                enemy.Damage = 1;
                enemy.CastTime = 100;
                enemy.SpecialSkill = null;
            }
            return configuration;
        }

        public static AdventureModel WinFirstBattle(AdventureConfiguration configuration)
        {
            AdventureModel model = new AdventureModel(configuration, 11);
            model.StartRun();
            model.SelectNode(0);
            model.Tick(5);
            return model;
        }

        public static void SetSingleEnemy(AdventureConfiguration configuration)
        {
            foreach (EnemyGroupDefinition group in configuration.EnemyGroups)
            {
                group.EnemyIds = new[] { group.EnemyIds[0] };
            }
        }
    }
}
