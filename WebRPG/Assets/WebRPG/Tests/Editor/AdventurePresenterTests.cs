using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace WebRPG.Tests
{
    public sealed class AdventurePresenterTests
    {
        [Test]
        public void ViewActionsReachModelAndDisposeUnsubscribes()
        {
            AdventureConfiguration configuration = JsonConvert.DeserializeObject<AdventureConfiguration>(File.ReadAllText(
                Path.Combine(Application.dataPath, "WebRPG/Data/AdventurePrototype.json")));
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
        public void InitialScreenHasFullHealthAndIdleGaugesForAllSixSlots()
        {
            AdventureConfiguration configuration = LoadConfiguration();
            AdventureModel model = new AdventureModel(configuration, 10);
            TestAdventureView view = new TestAdventureView();
            using (AdventurePresenter presenter = new AdventurePresenter(model, view))
            {
                Assert.That(view.Screen.PlayerHealth.FillAmount, Is.EqualTo(1));
                Assert.That(view.Screen.PlayerHealth.Text, Does.Contain(configuration.StartingHealth.ToString()));
                Assert.That(view.Screen.BasicAttack.FillAmount, Is.Zero);
                Assert.That(view.Screen.BasicAttack.Text, Does.Contain("待戰"));
                Assert.That(view.Screen.ShowBattle, Is.False);
                Assert.That(view.Screen.Enemies, Is.Empty);
                Assert.That(view.Screen.Skills, Has.Length.EqualTo(6));
                Assert.That(view.Screen.Skills.All(skill => skill.FillAmount == 0), Is.True);
                view.Start();
                Assert.That(view.Screen.ShowMap, Is.True);
                Assert.That(view.Screen.Skills[0].Title, Does.Contain(configuration.Skills[0].Name));
                Assert.That(view.Screen.Skills.Skip(1).All(skill => skill.Text == "尚未裝備"), Is.True);
            }
        }

        [Test]
        public void HealthAndIndependentCooldownGaugesTrackBattleState()
        {
            AdventureConfiguration configuration = LoadConfiguration();
            configuration.SimulationStep = 0.125f;
            configuration.BasicAttack.Cooldown = 2;
            configuration.Skills[0].Cooldown = 1;
            foreach (EnemyGroupDefinition group in configuration.EnemyGroups)
            {
                group.EnemyIds = new[] { group.EnemyIds[0] };
            }

            foreach (EnemyDefinition enemy in configuration.Enemies)
            {
                enemy.Health = 10000;
                enemy.Damage = 7;
                enemy.Cooldown = 0.5f;
            }

            AdventureModel model = new AdventureModel(configuration, 11);
            TestAdventureView view = new TestAdventureView();
            using (AdventurePresenter presenter = new AdventurePresenter(model, view))
            {
                view.Start();
                view.Choose(0);
                Assert.That(view.Screen.ShowBattle, Is.True);
                Assert.That(view.Screen.ShowMap, Is.False);
                Assert.That(view.Screen.BasicAttack.FillAmount, Is.Zero);
                Assert.That(view.Screen.Enemies[0].BasicAttack.FillAmount, Is.Zero);
                presenter.Tick(0.25f);
                Assert.That(view.Screen.BasicAttack.FillAmount, Is.EqualTo(0.125f));
                Assert.That(view.Screen.Skills[0].FillAmount, Is.EqualTo(0.25f));
                Assert.That(view.Screen.Enemies[0].BasicAttack.FillAmount, Is.EqualTo(0.5f));
                Assert.That(view.Screen.BasicAttack.Text, Does.Contain("秒"));
                presenter.Tick(0.25f);
                Assert.That(view.Screen.PlayerHealth.FillAmount,
                    Is.EqualTo((float)(configuration.StartingHealth - 7) / configuration.StartingHealth).Within(0.0001f));
                Assert.That(view.Screen.Enemies[0].BasicAttack.FillAmount, Is.Zero);
                view.Abandon();
                Assert.That(view.Screen.ShowBattle, Is.False);
                Assert.That(view.Screen.Enemies, Is.Empty);
                Assert.That(view.Screen.BasicAttack.FillAmount, Is.Zero);
                Assert.That(view.Screen.Skills.All(skill => skill.FillAmount == 0), Is.True);
            }
        }

        [Test]
        public void DefeatedEnemyHasEmptyGaugesAndExplicitDefeatText()
        {
            AdventureConfiguration configuration = LoadConfiguration();
            configuration.SimulationStep = 0.125f;
            configuration.BasicAttack.Cooldown = 0.25f;
            configuration.Skills[0].Cooldown = 100;
            configuration.Floors[0].Rows[0].NodePool[0].EnemyGroupPool = new[] { "patrol1" };
            foreach (EnemyDefinition enemy in configuration.Enemies)
            {
                enemy.Health = enemy.Id == "guard1" ? 1 : 10000;
                enemy.Cooldown = 100;
            }

            AdventureModel model = new AdventureModel(configuration, 12);
            TestAdventureView view = new TestAdventureView();
            using (AdventurePresenter presenter = new AdventurePresenter(model, view))
            {
                view.Start();
                view.Choose(0);
                presenter.Tick(0.25f);
                Assert.That(view.Screen.ShowBattle, Is.True);
                Assert.That(view.Screen.Enemies, Has.Length.EqualTo(2));
                Assert.That(view.Screen.Enemies[0].Health.FillAmount, Is.Zero);
                Assert.That(view.Screen.Enemies[0].BasicAttack.FillAmount, Is.Zero);
                Assert.That(view.Screen.Enemies[0].BasicAttack.Text, Is.EqualTo("已擊倒"));
                Assert.That(view.Screen.Enemies[1].Health.FillAmount, Is.EqualTo(1));
            }
        }

        [Test]
        public void NewBattleStartsWithEmptyCooldownProgressAfterReward()
        {
            AdventureConfiguration configuration = LoadConfiguration();
            configuration.SimulationStep = 0.125f;
            configuration.BasicAttack.Cooldown = 0.5f;
            foreach (EnemyDefinition enemy in configuration.Enemies)
            {
                enemy.Health = 1;
                enemy.Cooldown = 100;
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
                presenter.Tick(0.5f);
                Assert.That(model.Phase, Is.EqualTo(AdventurePhase.SkillReward));
                view.Choose(0);
                view.Choose(0);
                Assert.That(view.Screen.ShowBattle, Is.True);
                Assert.That(view.Screen.BasicAttack.FillAmount, Is.Zero);
                Assert.That(view.Screen.Skills.All(skill => skill.FillAmount == 0), Is.True);
                Assert.That(view.Screen.Enemies.All(enemy => enemy.BasicAttack.FillAmount == 0), Is.True);
            }
        }

        [Test]
        public void InspectingEnemyChangesSkillsWhileHeaderRemainsLocal()
        {
            AdventureConfiguration configuration = LoadConfiguration();
            configuration.SimulationStep = 0.125f;
            foreach (EnemyDefinition enemy in configuration.Enemies)
            {
                enemy.Health = 10000;
                enemy.Cooldown = 100;
            }

            AdventureModel model = new AdventureModel(configuration, 30);
            TestAdventureView view = new TestAdventureView();
            using (AdventurePresenter presenter = new AdventurePresenter(model, view))
            {
                view.Start();
                view.Choose(0);
                string summary = view.Screen.Summary;
                string localHealth = view.Screen.PlayerHealth.Text;
                view.Inspect("enemy:0");
                Assert.That(view.Screen.InspectedCharacterName, Is.EqualTo(model.Enemies[0].Definition.Name));
                Assert.That(view.Screen.InspectedSkills, Has.Length.EqualTo(1));
                Assert.That(view.Screen.Summary, Is.EqualTo(summary));
                Assert.That(view.Screen.PlayerHealth.Text, Is.EqualTo(localHealth));
                Assert.That(view.Screen.BasicAttack.Title, Does.Contain(model.BasicAttackDamage.ToString()));
                presenter.Tick(0.25f);
                Assert.That(view.Screen.InspectedSkills[0].Text, Is.EqualTo(view.Screen.Enemies[0].BasicAttack.Text));
                view.Inspect(null);
                Assert.That(view.Screen.InspectedCharacterId, Is.EqualTo("local-player"));
                Assert.That(view.Screen.InspectedSkills, Has.Length.EqualTo(7));
                view.Inspect("missing");
                Assert.That(view.Screen.InspectedCharacterId, Is.EqualTo("local-player"));
            }
        }

        [Test]
        public void MapInspectionPreservesBattleAndShowsActualChosenPath()
        {
            AdventureConfiguration configuration = LoadConfiguration();
            foreach (EnemyDefinition enemy in configuration.Enemies)
            {
                enemy.Health = 10000;
                enemy.Cooldown = 100;
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
            float cooldown = model.RemainingBasicAttackCooldown;
            presenter.Tick(0.25f);
            Assert.That(model.Phase, Is.EqualTo(AdventurePhase.Battle));
            Assert.That(model.RemainingBasicAttackCooldown, Is.LessThan(cooldown));
            view.ToggleMap();
            Assert.That(view.Screen.ShowMap, Is.False);
            AdventureScreenModel last = view.Screen;
            presenter.Dispose();
            view.Inspect("enemy:0");
            view.ToggleMap();
            Assert.That(view.Screen, Is.SameAs(last));
        }

        private static AdventureConfiguration LoadConfiguration()
        {
            return JsonConvert.DeserializeObject<AdventureConfiguration>(File.ReadAllText(
                Path.Combine(Application.dataPath, "WebRPG/Data/AdventurePrototype.json")));
        }

        private sealed class TestAdventureView : IAdventureView
        {
            public event Action StartRequested;
            public event Action AbandonRequested;
            public event Action<int> ChoiceRequested;
            public event Action<string> CharacterFocusRequested;
            public event Action MapToggleRequested;
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

            public void Choose(int index)
            {
                ChoiceRequested?.Invoke(index);
            }
        }
    }
}
