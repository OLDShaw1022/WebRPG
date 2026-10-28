using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace WebRPG.Tests
{
    public sealed class AdventureModelTests
    {
        [Test]
        public void DamagePersistsAndCooldownRestartsBetweenBattles()
        {
            AdventureConfiguration configuration = LoadConfiguration();
            configuration.SimulationStep = 0.125f;
            configuration.Skills[0].Cooldown = 1;
            configuration.Skills[0].Effects[0].Amount = 1000;
            foreach (EnemyDefinition enemy in configuration.Enemies)
            {
                enemy.Health = 20;
                enemy.Damage = 7;
                enemy.Cooldown = 0.5f;
            }

            SetSingleEnemy(configuration);
            AdventureModel model = new AdventureModel(configuration, 1);
            model.StartRun();
            model.SelectNode(0);
            model.Tick(1);
            Assert.That(model.Health, Is.EqualTo(configuration.StartingHealth - 7));
            Assert.That(model.Phase, Is.EqualTo(AdventurePhase.SkillReward));
            Assert.That(model.Skills[0].RemainingCooldown, Is.EqualTo(1));
            model.SelectReward(0);
            int health = model.Health;
            model.SelectNode(0);
            Assert.That(model.Health, Is.EqualTo(health));
            model.Tick(0.5f);
            Assert.That(model.Enemies[0].Health, Is.EqualTo(20));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(8)]
        public void RewardsAreUniqueAndUseOnlyAvailableCandidates(int available)
        {
            AdventureConfiguration configuration = WinningConfiguration();
            configuration.Skills = configuration.Skills.Take(available).ToArray();
            AdventureModel model = WinFirstBattle(configuration);
            Assert.That(model.Rewards.Count, Is.EqualTo(Math.Min(available, 3)));
            Assert.That(model.Rewards.Select(skill => skill.Id).Distinct().Count(), Is.EqualTo(model.Rewards.Count));
        }

        [Test]
        public void OwnedSkillUpgradesWithoutTakingAnotherSlot()
        {
            AdventureConfiguration configuration = WinningConfiguration();
            configuration.Skills = configuration.Skills.Take(1).ToArray();
            configuration.Skills[0].MaximumLevel = 2;
            AdventureModel model = WinFirstBattle(configuration);
            model.SelectReward(0);
            Assert.That(model.Skills.Count, Is.EqualTo(1));
            Assert.That(model.Skills[0].Level, Is.EqualTo(2));
            model.SelectNode(0);
            model.Tick(5);
            Assert.That(model.Phase, Is.EqualTo(AdventurePhase.Map));
            Assert.That(model.Rewards, Is.Empty);
        }

        [Test]
        public void FullSlotsOnlyOfferOwnedSkillsAndExcludeMaximumLevels()
        {
            AdventureConfiguration configuration = WinningConfiguration();
            configuration.StartingSkills = configuration.Skills.Take(6).Select(skill => skill.Id).ToArray();
            configuration.Skills[0].MaximumLevel = 1;
            AdventureModel model = WinFirstBattle(configuration);
            Assert.That(model.Rewards, Has.Count.EqualTo(3));
            Assert.That(model.Rewards.All(reward => configuration.StartingSkills.Contains(reward.Id)), Is.True);
            Assert.That(model.Rewards.Any(reward => reward.Id == configuration.Skills[0].Id), Is.False);
        }

        [Test]
        public void NewSkillOccupiesNextSlotAndMaximumDeckSkipsReward()
        {
            AdventureConfiguration configuration = WinningConfiguration();
            AdventureModel model = WinFirstBattle(configuration);
            int rewardIndex = model.Rewards.ToList().FindIndex(skill => skill.Id != configuration.StartingSkills[0]);
            Assert.That(rewardIndex, Is.GreaterThanOrEqualTo(0));
            string selected = model.Rewards[rewardIndex].Id;
            model.SelectReward(rewardIndex);
            Assert.That(model.Skills[1].Definition.Id, Is.EqualTo(selected));
            configuration = WinningConfiguration();
            configuration.StartingSkills = configuration.Skills.Take(6).Select(skill => skill.Id).ToArray();
            foreach (SkillDefinition skill in configuration.Skills)
            {
                skill.MaximumLevel = 1;
            }

            model = WinFirstBattle(configuration);
            Assert.That(model.Phase, Is.EqualTo(AdventurePhase.Map));
            Assert.That(model.Rewards, Is.Empty);
        }

        [Test]
        public void ChestAppliesPassiveHealthAndCampNeverExceedsMaximum()
        {
            AdventureConfiguration configuration = WinningConfiguration();
            configuration.Relics = configuration.Relics.Take(1).ToArray();
            AdventureModel model = WinFirstBattle(configuration);
            model.SelectReward(0);
            model.SelectNode(1);
            model.ResolveNode(0);
            Assert.That(model.Relics.Count, Is.EqualTo(1));
            Assert.That(model.MaximumHealth, Is.EqualTo(configuration.StartingHealth + 30));
            Assert.That(model.Health, Is.EqualTo(model.MaximumHealth));
            model.SelectNode(1);
            model.ResolveNode(0);
            Assert.That(model.Health, Is.EqualTo(model.MaximumHealth));
        }

        [Test]
        public void EventCostCanFailChallengeWithoutGrantingGold()
        {
            AdventureConfiguration configuration = WinningConfiguration();
            configuration.StartingHealth = 10;
            AdventureModel model = WinFirstBattle(configuration);
            model.SelectReward(0);
            model.SelectNode(0);
            model.Tick(5);
            model.SelectReward(0);
            model.SelectNode(1);
            model.ResolveNode(1);
            int gold = model.Gold;
            model.SelectNode(0);
            model.ResolveNode(0);
            Assert.That(model.Phase, Is.EqualTo(AdventurePhase.Failed));
            Assert.That(model.Health, Is.Zero);
            Assert.That(model.Gold, Is.EqualTo(gold));
        }

        [Test]
        public void ThreeBossesFinishRunAndReplayClearsProgress()
        {
            AdventureModel model = new AdventureModel(WinningConfiguration(), 3);
            model.StartRun();
            for (int action = 0; action < 100 && model.Phase != AdventurePhase.Complete; action++)
            {
                switch (model.Phase)
                {
                    case AdventurePhase.Map: model.SelectNode(0); break;
                    case AdventurePhase.Battle: model.Tick(5); break;
                    case AdventurePhase.SkillReward: model.SelectReward(0); break;
                    case AdventurePhase.NodeChoice:
                        model.ResolveNode(model.CurrentNode.Type == AdventureNodeType.Chest ? 0 : 1);
                        break;
                    default: Assert.Fail("Run failed unexpectedly."); break;
                }
            }

            Assert.That(model.Phase, Is.EqualTo(AdventurePhase.Complete));
            Assert.That(model.FloorIndex, Is.EqualTo(3));
            Assert.That(model.Gold, Is.GreaterThan(0));
            model.StartRun();
            Assert.That(model.Phase, Is.EqualTo(AdventurePhase.Map));
            Assert.That(model.FloorIndex, Is.Zero);
            Assert.That(model.Gold, Is.Zero);
            Assert.That(model.Relics, Is.Empty);
            Assert.That(model.Health, Is.EqualTo(model.Configuration.StartingHealth));
            Assert.That(model.Skills.Count, Is.EqualTo(1));
        }

        [Test]
        public void DeathTimeoutAndAbandonAreTerminalUntilRestart()
        {
            AdventureConfiguration configuration = WinningConfiguration();
            foreach (EnemyDefinition enemy in configuration.Enemies)
            {
                enemy.Health = 10000;
                enemy.Damage = 1000;
                enemy.Cooldown = 0.125f;
            }

            AdventureModel model = new AdventureModel(configuration, 4);
            model.StartRun();
            model.SelectNode(0);
            model.Tick(1);
            Assert.That(model.Phase, Is.EqualTo(AdventurePhase.Failed));
            Assert.That(model.Health, Is.Zero);
            model.SelectReward(0);
            model.SelectNode(0);
            Assert.That(model.Phase, Is.EqualTo(AdventurePhase.Failed));
            model.StartRun();
            model.AbandonRun();
            Assert.That(model.Phase, Is.EqualTo(AdventurePhase.Failed));
            configuration = WinningConfiguration();
            configuration.StartingSkills = new[] { "mend" };
            configuration.BasicAttack.Cooldown = 100;
            configuration.BattleTimeLimit = 2;
            model = new AdventureModel(configuration, 4);
            model.StartRun();
            model.SelectNode(0);
            model.Tick(3);
            Assert.That(model.Phase, Is.EqualTo(AdventurePhase.Failed));
        }

        [Test]
        public void FixedStepDoesNotDependOnFramePartitionAndIgnoresInvalidCommands()
        {
            AdventureConfiguration configuration = WinningConfiguration();
            AdventureModel first = new AdventureModel(configuration, 9);
            AdventureModel second = new AdventureModel(configuration, 9);
            first.StartRun();
            second.StartRun();
            first.SelectNode(-1);
            first.SelectNode(100);
            first.Tick(float.NaN);
            Assert.That(first.Phase, Is.EqualTo(AdventurePhase.Map));
            first.SelectNode(0);
            second.SelectNode(0);
            first.Tick(5);
            for (int frame = 0; frame < 100; frame++)
            {
                second.Tick(0.05f);
            }

            Assert.That(first.Health, Is.EqualTo(second.Health));
            Assert.That(first.Phase, Is.EqualTo(second.Phase));
            Assert.That(first.Rewards.Select(skill => skill.Id), Is.EqualTo(second.Rewards.Select(skill => skill.Id)));
        }

        [Test]
        public void InvalidConfigurationFailsBeforeStartingRun()
        {
            AdventureConfiguration configuration = LoadConfiguration();
            configuration.Skills[0].Cooldown = 0;
            Assert.Throws<ArgumentException>(() => new AdventureModel(configuration, 1));
            configuration = LoadConfiguration();
            configuration.Floors[0].Rows[0].NodePool[0].EnemyGroupPool = new[] { "missing" };
            Assert.Throws<ArgumentException>(() => new AdventureModel(configuration, 1));
            configuration = LoadConfiguration();
            configuration.Floors[0].Rows.Last().NodePool[0].Type = AdventureNodeType.Camp;
            Assert.Throws<ArgumentException>(() => new AdventureModel(configuration, 1));
        }

        [Test]
        public void BasicAttackUsesOwnCooldownAndDoesNotOccupySkillSlot()
        {
            AdventureConfiguration configuration = WinningConfiguration();
            configuration.SimulationStep = 0.125f;
            configuration.BasicAttack.Damage = 6;
            configuration.BasicAttack.Cooldown = 0.5f;
            configuration.Skills[0].Cooldown = 5;
            foreach (EnemyDefinition enemy in configuration.Enemies)
            {
                enemy.Health = 100;
            }

            SetSingleEnemy(configuration);
            AdventureModel model = new AdventureModel(configuration, 21);
            model.StartRun();
            model.SelectNode(0);
            model.Tick(0.375f);
            Assert.That(model.Enemies[0].Health, Is.EqualTo(100));
            model.Tick(0.125f);
            Assert.That(model.Enemies[0].Health, Is.EqualTo(94));
            Assert.That(model.RemainingBasicAttackCooldown, Is.EqualTo(0.5f));
            Assert.That(model.Skills[0].RemainingCooldown, Is.EqualTo(4.5f));
            Assert.That(model.Skills.Count, Is.EqualTo(1));
            model.Tick(0.5f);
            Assert.That(model.Enemies[0].Health, Is.EqualTo(88));
        }

        [Test]
        public void BasicAttackWinsBeforeEnemyInSameStepAndResetsForNextBattle()
        {
            AdventureConfiguration configuration = WinningConfiguration();
            configuration.SimulationStep = 0.125f;
            configuration.BasicAttack.Cooldown = 0.5f;
            configuration.Skills[0].Cooldown = 5;
            foreach (EnemyDefinition enemy in configuration.Enemies)
            {
                enemy.Cooldown = 0.5f;
                enemy.Damage = 1000;
            }

            SetSingleEnemy(configuration);
            AdventureModel model = new AdventureModel(configuration, 22);
            model.StartRun();
            model.SelectNode(0);
            model.Tick(0.5f);
            Assert.That(model.Phase, Is.EqualTo(AdventurePhase.SkillReward));
            Assert.That(model.Health, Is.EqualTo(configuration.StartingHealth));
            Assert.That(model.RemainingBasicAttackCooldown, Is.EqualTo(0.5f));
            model.SelectReward(0);
            model.SelectNode(0);
            model.Tick(0.375f);
            Assert.That(model.Enemies[0].Health, Is.EqualTo(1));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(7)]
        public void RowCountAndGeneratedNodeTypesFollowEachFloorConfiguration(int rows)
        {
            AdventureConfiguration configuration = WinningConfiguration();
            MapRowDefinition boss = configuration.Floors[0].Rows.Last();
            MapRowDefinition branch = new MapRowDefinition
            {
                NodeCount = 2,
                NodePool = new[]
                {
                    new NodeDefinition { Name = "測試營火", Type = AdventureNodeType.Camp, Healing = 5 },
                    new NodeDefinition { Name = "測試寶箱", Type = AdventureNodeType.Chest },
                    new NodeDefinition { Name = "測試事件", Type = AdventureNodeType.Event, Healing = 5 }
                }
            };
            configuration.Floors[0].Rows = Enumerable.Repeat(branch, rows - 1).Concat(new[] { boss }).ToArray();
            AdventureModel model = new AdventureModel(configuration, 23);
            model.StartRun();
            Assert.That(model.CurrentFloorRows.Count, Is.EqualTo(rows));
            foreach (IReadOnlyList<NodeDefinition> generated in model.CurrentFloorRows.Take(rows - 1))
            {
                Assert.That(generated.Count, Is.EqualTo(2));
                Assert.That(generated.Select(node => node.Name).Distinct().Count(), Is.EqualTo(2));
                Assert.That(generated.All(node => branch.NodePool.Contains(node)), Is.True);
                Assert.That(generated.All(node => !node.IsBattle), Is.True);
            }

            Assert.That(model.CurrentFloorRows.Last().All(node => node.Type == AdventureNodeType.Boss), Is.True);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void EncounterUsesEntireConfiguredGroupIncludingOrderAndDuplicates(int count)
        {
            AdventureConfiguration configuration = WinningConfiguration();
            string[] members = new[] { "guard1", "beast1", "guard1", "beast1" }.Take(count).ToArray();
            configuration.EnemyGroups = configuration.EnemyGroups.Concat(new[]
            {
                new EnemyGroupDefinition { Id = "testGroup", EnemyIds = members }
            }).ToArray();
            configuration.Floors[0].Rows[0].NodePool[0].EnemyGroupPool = new[] { "testGroup" };
            AdventureModel model = new AdventureModel(configuration, 24);
            model.StartRun();
            model.SelectNode(0);
            Assert.That(model.CurrentEnemyGroup.Id, Is.EqualTo("testGroup"));
            Assert.That(model.Enemies.Select(enemy => enemy.Definition.Id), Is.EqualTo(members));
            Assert.That(model.Enemies.Count, Is.EqualTo(count));
        }

        [Test]
        public void RandomEncounterNeverCombinesMembersFromDifferentGroups()
        {
            AdventureConfiguration configuration = WinningConfiguration();
            configuration.EnemyGroups = configuration.EnemyGroups.Concat(new[]
            {
                new EnemyGroupDefinition { Id = "pair", EnemyIds = new[] { "guard1", "guard1" } },
                new EnemyGroupDefinition { Id = "trio", EnemyIds = new[] { "beast1", "guard1", "beast1" } }
            }).ToArray();
            configuration.Floors[0].Rows[0].NodePool[0].EnemyGroupPool = new[] { "pair", "trio" };
            HashSet<string> selected = new HashSet<string>();
            for (int seed = 0; seed < 32; seed++)
            {
                AdventureModel model = new AdventureModel(configuration, seed);
                model.StartRun();
                model.SelectNode(0);
                selected.Add(model.CurrentEnemyGroup.Id);
                Assert.That(model.Enemies.Select(enemy => enemy.Definition.Id),
                    Is.EqualTo(model.CurrentEnemyGroup.EnemyIds));
            }

            Assert.That(selected, Is.EquivalentTo(new[] { "pair", "trio" }));
        }

        [Test]
        public void DamageRelicAlsoAppliesToBasicAttack()
        {
            AdventureConfiguration configuration = WinningConfiguration();
            configuration.BasicAttack.Cooldown = 0.5f;
            configuration.SimulationStep = 0.125f;
            configuration.Relics = configuration.Relics.Where(relic => relic.Id == "fang").ToArray();
            configuration.Enemies.First(enemy => enemy.Id == "beast1").Health = 100;
            configuration.EnemyGroups = configuration.EnemyGroups.Concat(new[]
            {
                new EnemyGroupDefinition { Id = "weak", EnemyIds = new[] { "guard1" } },
                new EnemyGroupDefinition { Id = "strong", EnemyIds = new[] { "beast1" } }
            }).ToArray();
            configuration.Floors[0].Rows[0].NodePool[0].EnemyGroupPool = new[] { "weak" };
            configuration.Floors[0].Rows[2].NodePool[0].EnemyGroupPool = new[] { "strong" };
            AdventureModel model = WinFirstBattle(configuration);
            model.SelectReward(0);
            model.SelectNode(1);
            model.ResolveNode(0);
            model.SelectNode(0);
            model.Tick(0.5f);
            Assert.That(model.Enemies[0].Health, Is.EqualTo(90));
            Assert.That(model.BasicAttackDamage, Is.EqualTo(10));
        }

        [Test]
        public void InvalidRowsGroupsAndBasicAttackAreRejected()
        {
            AdventureConfiguration configuration = LoadConfiguration();
            configuration.Floors[0].Rows[0].NodeCount = 2;
            Assert.Throws<ArgumentException>(() => new AdventureModel(configuration, 1));
            configuration = LoadConfiguration();
            configuration.EnemyGroups[0].EnemyIds = Array.Empty<string>();
            Assert.Throws<ArgumentException>(() => new AdventureModel(configuration, 1));
            configuration.EnemyGroups[0].EnemyIds = Enumerable.Repeat("guard1", 5).ToArray();
            Assert.Throws<ArgumentException>(() => new AdventureModel(configuration, 1));
            configuration.EnemyGroups[0].EnemyIds = new[] { "missing" };
            Assert.Throws<ArgumentException>(() => new AdventureModel(configuration, 1));
            configuration = LoadConfiguration();
            configuration.BasicAttack.Cooldown = 0;
            Assert.Throws<ArgumentException>(() => new AdventureModel(configuration, 1));
        }

        [Test]
        public void ChosenMapPathRecordsOnlyValidSelectionsAndResetsOnReplay()
        {
            AdventureModel model = new AdventureModel(WinningConfiguration(), 32);
            Assert.That(model.GetFloorRows(0), Is.Empty);
            Assert.That(model.GetFloorPath(0), Is.Empty);
            model.StartRun();
            Assert.That(model.GetFloorRows(0).Count, Is.EqualTo(model.Configuration.Floors[0].Rows.Length));
            Assert.That(model.GetFloorRows(-1), Is.Empty);
            Assert.That(model.GetFloorRows(100), Is.Empty);
            model.SelectNode(-1);
            Assert.That(model.GetFloorPath(0), Is.Empty);
            model.SelectNode(0);
            model.SelectNode(0);
            Assert.That(model.GetFloorPath(0), Is.EqualTo(new[] { 0 }));
            model.Tick(5);
            model.SelectReward(0);
            model.SelectNode(1);
            Assert.That(model.GetFloorPath(0), Is.EqualTo(new[] { 0, 1 }));
            model.ResolveNode(0);
            Assert.That(model.GetFloorPath(0), Is.EqualTo(new[] { 0, 1 }));
            model.AbandonRun();
            model.StartRun();
            Assert.That(model.GetFloorPath(0), Is.Empty);
            Assert.That(model.GetFloorPath(-1), Is.Empty);
            Assert.That(model.GetFloorPath(100), Is.Empty);
            Assert.That(model.BasicAttackDamage, Is.EqualTo(model.Configuration.BasicAttack.Damage));
        }

        private static AdventureConfiguration LoadConfiguration()
        {
            return JsonConvert.DeserializeObject<AdventureConfiguration>(File.ReadAllText(
                Path.Combine(Application.dataPath, "WebRPG/Data/AdventurePrototype.json")));
        }

        private static AdventureConfiguration WinningConfiguration()
        {
            AdventureConfiguration configuration = LoadConfiguration();
            foreach (EnemyDefinition enemy in configuration.Enemies)
            {
                enemy.Health = 1;
                enemy.Damage = 1;
                enemy.Cooldown = 100;
            }

            return configuration;
        }

        private static AdventureModel WinFirstBattle(AdventureConfiguration configuration)
        {
            AdventureModel model = new AdventureModel(configuration, 11);
            model.StartRun();
            model.SelectNode(0);
            model.Tick(5);
            return model;
        }

        private static void SetSingleEnemy(AdventureConfiguration configuration)
        {
            foreach (EnemyGroupDefinition group in configuration.EnemyGroups)
            {
                group.EnemyIds = new[] { group.EnemyIds[0] };
            }
        }
    }
}
