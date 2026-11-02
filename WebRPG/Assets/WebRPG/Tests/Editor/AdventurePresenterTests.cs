using System;
using System.Linq;
using NUnit.Framework;

namespace WebRPG.Tests
{
    // 驗證冒險操作事件、數值條、角色檢視與地圖畫面資料。
    public sealed class AdventurePresenterTests
    {
        [Test]
        public void BattleTicksReuseMapRelicsChoicesAndJournalUntilTheirContentChanges()
        {
            AdventureConfiguration configuration = AdventureBattleTestHelper.Configuration();
            AdventureModel model = AdventureBattleTestHelper.StartBattle(configuration);
            TestAdventureView view = new TestAdventureView();
            using (AdventurePresenter presenter = new AdventurePresenter(model, view))
            {
                view.ToggleMap();
                AdventureScreenModel before = view.Screen;
                float remaining = model.PlayerAction.RemainingCastTime;
                presenter.Tick(configuration.SimulationStep);
                Assert.That(model.PlayerAction.RemainingCastTime, Is.LessThan(remaining));
                Assert.That(view.Screen.MapRows, Is.SameAs(before.MapRows));
                Assert.That(view.Screen.Relics, Is.SameAs(before.Relics));
                Assert.That(view.Screen.Choices, Is.SameAs(before.Choices));
                Assert.That(view.Screen.Journal, Is.SameAs(before.Journal));
                view.ToggleMap();
                view.ToggleMap();
                Assert.That(view.Screen.MapRows, Is.SameAs(before.MapRows));
            }
        }

        [Test]
        public void ReadOnlyCollectionsAreStableAndReflectRunChanges()
        {
            AdventureModel model = new AdventureModel(AdventureModelScenarioHelper.LoadConfiguration(), 1);
            var skills = model.Skills;
            Assert.That(model.Skills, Is.SameAs(skills));
            Assert.That(model.Relics, Is.SameAs(model.Relics));
            Assert.That(model.Rewards, Is.SameAs(model.Rewards));
            model.StartRun();
            Assert.That(skills.Count, Is.EqualTo(model.Configuration.StartingSkills.Length));
            Assert.That(model.Journal, Is.SameAs(model.Journal));
        }

        [Test]
        public void ViewActionsReachModelAndDisposeUnsubscribes()
        {
            AdventureConfiguration configuration = AdventureModelScenarioHelper.LoadConfiguration();
            AdventureModel model = new AdventureModel(configuration, 1);
            TestAdventureView view = new TestAdventureView();
            AdventurePresenter presenter = new AdventurePresenter(model, view);
            Assert.That(view.Screen.CanStart, Is.True);
            view.Start();
            Assert.That(model.Phase, Is.EqualTo(AdventurePhase.Map));
            Assert.That(view.Screen.Skills.Length, Is.EqualTo(6));
            Assert.That(view.Screen.Choices.Length, Is.EqualTo(model.AvailableNodes.Count));
            view.Choose(0);
            Assert.That(model.Phase, Is.EqualTo(AdventurePhase.Battle));
            Assert.That(view.Screen.Choices, Is.Empty);
            view.Abandon();
            Assert.That(view.Screen.CanStart, Is.True);
            Assert.That(model.Phase, Is.EqualTo(AdventurePhase.Failed));
            presenter.Dispose();
            presenter.Dispose();
            view.Start();
            Assert.That(model.Phase, Is.EqualTo(AdventurePhase.Failed));
        }

        [Test]
        public void SummaryOrdersPlayerHealthGoldAndFloorWithoutCombatCounts()
        {
            AdventureConfiguration configuration = AdventureModelScenarioHelper.LoadConfiguration();
            TestAdventureView view = new TestAdventureView();
            using (AdventurePresenter presenter = new AdventurePresenter(new AdventureModel(configuration, 1), view))
            {
                string expected = string.Format("本機玩家 · 生命 {0} / {0} · 金幣 0 · 第 1 / {1} 層",
                    configuration.StartingHealth, configuration.Floors.Length);
                Assert.That(view.Screen.Summary, Is.EqualTo(expected));
                view.Start();
                Assert.That(view.Screen.Summary, Is.EqualTo(expected));
            }
        }

        [Test]
        public void InitialScreenHasFullHealthAndIdleGaugesForAllSixSlots()
        {
            AdventureConfiguration configuration = AdventureModelScenarioHelper.LoadConfiguration();
            AdventureModel model = new AdventureModel(configuration, 10);
            TestAdventureView view = new TestAdventureView();
            using (AdventurePresenter presenter = new AdventurePresenter(model, view))
            {
                Assert.That(view.Screen.PlayerHealth.FillAmount, Is.EqualTo(1));
                Assert.That(view.Screen.PlayerHealth.Text, Does.Contain(configuration.StartingHealth.ToString()));
                Assert.That(view.Screen.BasicAttack.FillAmount, Is.Zero);
                Assert.That(view.Screen.BasicAttack.DisplayName, Is.EqualTo("普攻"));
                Assert.That(view.Screen.Skills[0].DisplayName, Is.EqualTo("空槽"));
                Assert.That(view.Screen.Skills.Skip(1).All(skill => skill.DisplayName == "空槽"), Is.True);
                Assert.That(view.Screen.BasicAttack.Text, Does.Contain("待戰"));
                Assert.That(view.Screen.ShowBattle, Is.False);
                Assert.That(view.Screen.Enemies, Is.Empty);
                Assert.That(view.Screen.Skills, Has.Length.EqualTo(6));
                Assert.That(view.Screen.Skills.All(skill => skill.FillAmount == 0), Is.True);
                view.Start();
                Assert.That(view.Screen.ShowMap, Is.True);
                Assert.That(view.Screen.Skills[0].Title, Does.Contain(configuration.Skills[0].Name));
                Assert.That(view.Screen.Skills[0].DisplayName, Is.EqualTo(configuration.Skills[0].Name));
                Assert.That(view.Screen.Skills.Skip(1).All(skill => skill.Text == "尚未裝備"), Is.True);
            }
        }

        [Test]
        public void CastBarsReadySkillsAndDelayedHealthTrackBattleState()
        {
            AdventureConfiguration configuration = AdventureBattleTestHelper.Configuration();
            AdventureModelScenarioHelper.SetSingleEnemy(configuration);
            configuration.Enemies[0].Damage = 7;
            configuration.Enemies[0].CastTime = 0.5f;
            AdventureModel model = AdventureBattleTestHelper.StartBattle(configuration);
            TestAdventureView view = new TestAdventureView();
            using (AdventurePresenter presenter = new AdventurePresenter(model, view))
            {
                Assert.That(view.Screen.Skills[0].FillAmount, Is.EqualTo(1));
                Assert.That(AdventureBattleTestHelper.Read<bool>(view.Screen.Skills[0], "CanUse"), Is.True);
                presenter.Tick(0.25f);
                Assert.That(view.Screen.BasicAttack.FillAmount, Is.EqualTo(0.125f));
                AdventureGaugeScreenModel enemyCast = AdventureBattleTestHelper.Read<AdventureGaugeScreenModel>(
                    view.Screen.Enemies[0], "Action");
                Assert.That(enemyCast.FillAmount, Is.EqualTo(0.5f));
                presenter.Tick(0.25f);
                Assert.That(model.Health, Is.EqualTo(configuration.StartingHealth));
                presenter.Tick(0.5f);
                Assert.That(view.Screen.PlayerHealth.FillAmount,
                    Is.EqualTo((float)(configuration.StartingHealth - 7) / configuration.StartingHealth));
                view.Abandon();
                Assert.That(view.Screen.ShowBattle, Is.False);
                Assert.That(view.Screen.Skills.All(skill => skill.FillAmount == 0), Is.True);
            }
        }

        [Test]
        public void DefeatedEnemyHasEmptyGaugesAndExplicitDefeatText()
        {
            AdventureConfiguration configuration = AdventureModelScenarioHelper.LoadConfiguration();
            configuration.SimulationStep = 0.125f;
            configuration.BasicAttack.CastTime = 0.25f;
            configuration.ProjectileTravelTime = 0.125f;
            configuration.Skills[0].Cooldown = 100;
            configuration.Floors[0].Rows[0].NodePool[0].EnemyGroupPool = new[] { "patrol1" };
            foreach (EnemyDefinition enemy in configuration.Enemies)
            {
                enemy.Health = enemy.Id == "guard1" ? 1 : 10000;
                enemy.CastTime = 100;
                enemy.SpecialSkill = null;
            }

            AdventureModel model = new AdventureModel(configuration, 12);
            TestAdventureView view = new TestAdventureView();
            using (AdventurePresenter presenter = new AdventurePresenter(model, view))
            {
                view.Start();
                view.Choose(0);
                presenter.Tick(0.375f);
                Assert.That(view.Screen.ShowBattle, Is.True);
                Assert.That(view.Screen.Enemies, Has.Length.EqualTo(2));
                Assert.That(view.Screen.Enemies[0].Health.FillAmount, Is.Zero);
                Assert.That(view.Screen.Enemies[0].BasicAttack.FillAmount, Is.Zero);
                Assert.That(view.Screen.Enemies[0].BasicAttack.Text, Is.EqualTo("已擊倒"));
                Assert.That(view.Screen.Enemies[1].Health.FillAmount, Is.EqualTo(1));
            }
        }

        [Test]
        public void NewBattleStartsWithEmptyCastBarsAndReadySkillsAfterReward()
        {
            AdventureConfiguration configuration = AdventureModelScenarioHelper.LoadConfiguration();
            configuration.SimulationStep = 0.125f;
            configuration.BasicAttack.CastTime = 0.5f;
            configuration.ProjectileTravelTime = 0.125f;
            foreach (EnemyDefinition enemy in configuration.Enemies)
            {
                enemy.Health = 1;
                enemy.CastTime = 100;
                enemy.SpecialSkill = null;
            }

            foreach (EnemyGroupDefinition group in configuration.EnemyGroups)
            {
                group.EnemyIds = new[] { group.EnemyIds[0] };
            }

            AdventureModel model = new AdventureModel(configuration, 13);
            TestAdventureView view = new TestAdventureView();
            using (AdventurePresenter presenter = new AdventurePresenter(model, view))
            {
                view.Start();
                view.Choose(0);
                presenter.Tick(0.625f);
                Assert.That(model.Phase, Is.EqualTo(AdventurePhase.SkillReward));
                view.Choose(0);
                view.Choose(0);
                Assert.That(view.Screen.ShowBattle, Is.True);
                Assert.That(view.Screen.BasicAttack.FillAmount, Is.Zero);
                Assert.That(view.Screen.Skills.Take(model.Skills.Count).All(skill => skill.FillAmount == 1), Is.True);
                Assert.That(view.Screen.Skills.Skip(model.Skills.Count).All(skill => skill.FillAmount == 0), Is.True);
                Assert.That(view.Screen.Enemies.All(enemy => enemy.BasicAttack.FillAmount == 0), Is.True);
            }
        }

        [Test]
        public void ClickingEnemyLocksWithoutReplacingPlayerSkillBar()
        {
            AdventureModel model = AdventureBattleTestHelper.StartBattle();
            TestAdventureView view = new TestAdventureView();
            using (AdventurePresenter presenter = new AdventurePresenter(model, view))
            {
                string summary = view.Screen.Summary;
                view.Inspect("enemy:2");
                Assert.That(model.LockedEnemyIndex, Is.EqualTo(2));
                Assert.That(AdventureBattleTestHelper.Read<bool>(view.Screen.Enemies[2], "IsLocked"), Is.True);
                Assert.That(view.Screen.InspectedCharacterId, Is.EqualTo("local-player"));
                Assert.That(view.Screen.InspectedSkills, Has.Length.EqualTo(7));
                Assert.That(view.Screen.Summary, Is.EqualTo(summary));
                view.Inspect(null);
                view.Inspect("missing");
                Assert.That(model.LockedEnemyIndex, Is.EqualTo(2));
            }
        }

        [Test]
        public void MapInspectionPreservesBattleAndShowsActualChosenPath()
        {
            AdventureConfiguration configuration = AdventureModelScenarioHelper.LoadConfiguration();
            foreach (EnemyDefinition enemy in configuration.Enemies)
            {
                enemy.Health = 10000;
                enemy.CastTime = 100;
                enemy.SpecialSkill = null;
            }

            AdventureModel model = new AdventureModel(configuration, 31);
            TestAdventureView view = new TestAdventureView();
            AdventurePresenter presenter = new AdventurePresenter(model, view);
            view.Start();
            Assert.That(view.Screen.ShowMap, Is.True);
            Assert.That(view.Screen.CanToggleMap, Is.False);
            Assert.That(view.Screen.MapRows[0].Nodes.All(node => node.CanSelect), Is.True);
            Assert.That(view.Screen.MapRows.Skip(1).SelectMany(row => row.Nodes).Any(node => node.CanSelect), Is.False);
            view.Choose(0);
            view.Inspect("enemy:0");
            view.ToggleMap();
            Assert.That(view.Screen.ShowMap, Is.True);
            Assert.That(view.Screen.InspectedCharacterId, Is.EqualTo("local-player"));
            Assert.That(view.Screen.MapRows[0].Nodes[0].IsChosen, Is.True);
            Assert.That(view.Screen.MapRows.SelectMany(row => row.Nodes).Any(node => node.CanSelect), Is.False);
            float cooldown = model.PlayerAction.RemainingCastTime;
            presenter.Tick(0.25f);
            Assert.That(model.Phase, Is.EqualTo(AdventurePhase.Battle));
            Assert.That(model.PlayerAction.RemainingCastTime, Is.LessThan(cooldown));
            view.ToggleMap();
            Assert.That(view.Screen.ShowMap, Is.False);
            AdventureScreenModel last = view.Screen;
            presenter.Dispose();
            view.Inspect("enemy:0");
            view.ToggleMap();
            Assert.That(view.Screen, Is.SameAs(last));
        }

        [Test]
        public void SkillAndSpeedCommandsReachModelAndDisposeUnsubscribes()
        {
            AdventureModel model = AdventureBattleTestHelper.StartBattle();
            TestAdventureView view = new TestAdventureView();
            AdventurePresenter presenter = new AdventurePresenter(model, view);
            view.UseSkill(0);
            Assert.That(model.PlayerAction.SkillIndex, Is.Zero);
            view.SetSpeed(2);
            presenter.Tick(0.25f);
            Assert.That(model.PlayerAction.RemainingCastTime, Is.EqualTo(2.5f));
            presenter.SetPaused(true);
            presenter.Tick(5);
            Assert.That(model.PlayerAction.RemainingCastTime, Is.EqualTo(2.5f));
            presenter.SetPaused(false);
            view.SetSpeed(0);
            presenter.Tick(5);
            Assert.That(model.PlayerAction.RemainingCastTime, Is.EqualTo(2.5f));
            view.SetSpeed(1);
            presenter.Dispose();
            view.SetSpeed(2);
            view.Inspect("enemy:2");
            Assert.That(model.BattleSpeed, Is.EqualTo(1));
            Assert.That(model.LockedEnemyIndex, Is.Zero);
        }

        [Test]
        public void ProjectileSnapshotTracksFlightPauseAndImpact()
        {
            AdventureModel model = AdventureBattleTestHelper.StartBattle();
            TestAdventureView view = new TestAdventureView();
            using (AdventurePresenter presenter = new AdventurePresenter(model, view))
            {
                presenter.Tick(2);
                object projectile = AdventureBattleTestHelper.Read<object>(view.Screen, "Projectile");
                Assert.That(projectile, Is.Not.Null);
                Assert.That(AdventureBattleTestHelper.Read<string>(projectile, "SourceCharacterId"),
                    Is.EqualTo("local-player"));
                Assert.That(AdventureBattleTestHelper.Read<string>(projectile, "TargetCharacterId"),
                    Is.EqualTo("enemy:0"));
                presenter.Tick(0.25f);
                projectile = AdventureBattleTestHelper.Read<object>(view.Screen, "Projectile");
                Assert.That(AdventureBattleTestHelper.Read<float>(projectile, "Progress"), Is.EqualTo(0.5f));
                view.SetSpeed(0);
                presenter.Tick(10);
                projectile = AdventureBattleTestHelper.Read<object>(view.Screen, "Projectile");
                Assert.That(AdventureBattleTestHelper.Read<float>(projectile, "Progress"), Is.EqualTo(0.5f));
                view.SetSpeed(2);
                presenter.Tick(0.125f);
                Assert.That(AdventureBattleTestHelper.Read<object>(view.Screen, "Projectile"), Is.Null);
                Assert.That(model.Enemies[0].Health, Is.EqualTo(94));
            }
        }

        [Test]
        public void EnemyActionAndUpcomingSpecialAreSeparateTooltipSections()
        {
            AdventureConfiguration configuration = AdventureBattleTestHelper.Configuration();
            configuration.Enemies[0].SpecialSkill = new EnemySkillDefinition
            {
                Id = "test-heal", Name = "骨骼修補", CastTime = 3, Cooldown = 8,
                BasicAttacksBeforeUse = 2,
                Effects = new[] { new EffectDefinition { Type = AdventureEffectType.Heal, Amount = 8 } }
            };
            AdventureModel model = AdventureBattleTestHelper.StartBattle(configuration);
            TestAdventureView view = new TestAdventureView();
            using (AdventurePresenter presenter = new AdventurePresenter(model, view))
            {
                string action = AdventureBattleTestHelper.Read<string>(view.Screen.Enemies[0], "ActionDescription");
                string intent = AdventureBattleTestHelper.Read<string>(view.Screen.Enemies[0], "SpecialIntent");
                Assert.That(action, Does.Contain("普攻"));
                Assert.That(intent, Does.Contain("----------------"));
                Assert.That(intent, Does.Contain("2 次普攻"));
                Assert.That(intent, Does.Contain("骨骼修補"));
            }
        }

        // 模擬冒險畫面操作並保存呈現資料，供 Presenter 測試使用。
        private sealed class TestAdventureView : IAdventureView
        {
            public event Action StartRequested;
            public event Action AbandonRequested;
            public event Action<int> ChoiceRequested;
            public event Action<string> CharacterFocusRequested;
            public event Action MapToggleRequested;
            public event Action<int> SkillRequested;
            public event Action<float> BattleSpeedRequested;
            public AdventureScreenModel Screen { get; private set; }

            public void Render(AdventureScreenModel screen)
            {
                Screen = screen;
            }

            public void Start()
            {
                StartRequested?.Invoke();
            }

            public void Abandon()
            {
                AbandonRequested?.Invoke();
            }

            public void Inspect(string id)
            {
                CharacterFocusRequested?.Invoke(id);
            }

            public void ToggleMap()
            {
                MapToggleRequested?.Invoke();
            }

            public void UseSkill(int index)
            {
                SkillRequested?.Invoke(index);
            }

            public void SetSpeed(float speed)
            {
                BattleSpeedRequested?.Invoke(speed);
            }

            public void Choose(int index)
            {
                ChoiceRequested?.Invoke(index);
            }
        }
    }
}
