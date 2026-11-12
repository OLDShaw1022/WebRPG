using System.Linq;
using NUnit.Framework;

namespace WebRPG.Tests
{
    // 驗證多人冒險的共用戰鬥、個人獎勵及進度屏障。
    public sealed class MultiplayerAdventureTests
    {
        [Test]
        public void PartySharesEnemiesAndWaitsForEveryReward()
        {
            AdventureConfiguration configuration = AdventureModelScenarioHelper.WinningConfiguration();
            configuration.BasicAttack.Damage = 1000;
            MultiplayerAdventureModel party = new MultiplayerAdventureModel(configuration, 123, new[] { "a", "b" });
            Assert.That(party.CanVote, Is.True);
            party.SelectNode(0);
            Assert.That(party.Players[0].Enemies[0], Is.SameAs(party.Players[1].Enemies[0]));
            party.Tick(20);
            Assert.That(party.Players.All(player => player.Phase == AdventurePhase.SkillReward), Is.True);
            party.SelectReward(0, 0);
            Assert.That(party.CanVote, Is.False);
            party.SelectReward(1, 0);
            Assert.That(party.CanVote, Is.True);
        }

        [Test]
        public void DefeatedPlayerReceivesRewardAndConfiguredRevival()
        {
            AdventureConfiguration configuration = AdventureModelScenarioHelper.WinningConfiguration();
            configuration.BasicAttack.Damage = 100000;
            MultiplayerAdventureModel party = new MultiplayerAdventureModel(configuration, 2, new[] { "a", "b" });
            party.SelectNode(0);
            party.Players[0].Battle.ReceivePartyDamage(configuration.StartingHealth);
            Assert.That(party.Players[0].Battle.Health, Is.Zero);
            party.Tick(30);
            Assert.That(party.Players[0].Phase, Is.EqualTo(AdventurePhase.SkillReward));
            Assert.That(party.Players[0].Health, Is.EqualTo(
                (int)System.Math.Ceiling(
                    configuration.StartingHealth * configuration.Multiplayer.RevivalHealthFraction)));
        }

        [Test]
        public void AllDefeatedOrDisconnectedEndsEveryRun()
        {
            AdventureConfiguration configuration = AdventureModelScenarioHelper.WinningConfiguration();
            MultiplayerAdventureModel party = new MultiplayerAdventureModel(configuration, 4, new[] { "a", "b" });
            party.SelectNode(0);
            foreach (AdventureModel player in party.Players)
                player.Battle.ReceivePartyDamage(configuration.StartingHealth);
            party.Tick(configuration.SimulationStep);
            Assert.That(party.Players.All(player => player.Phase == AdventurePhase.Failed), Is.True);
            MultiplayerAdventureModel disconnected = new MultiplayerAdventureModel(
                configuration, 4, new[] { "a", "b" });
            disconnected.Fail();
            Assert.That(disconnected.Players.All(player => player.Phase == AdventurePhase.Failed), Is.True);
        }

        [Test]
        public void PartyEnemyHealthScalesWithoutMutatingConfiguration()
        {
            AdventureConfiguration configuration = AdventureModelScenarioHelper.WinningConfiguration();
            int health = configuration.Enemies[0].Health;
            MultiplayerAdventureModel party = new MultiplayerAdventureModel(
                configuration, 4, new[] { "a", "b", "c", "d" });
            party.SelectNode(0);
            Assert.That(configuration.Enemies[0].Health, Is.EqualTo(health));
            Assert.That(party.Players[0].Enemies[0].Definition.Health, Is.EqualTo(health * 4));
        }

        [Test]
        public void SameOrderedInputsProduceIdenticalPartyState()
        {
            AdventureConfiguration configuration = AdventureModelScenarioHelper.WinningConfiguration();
            MultiplayerAdventureModel first = new MultiplayerAdventureModel(configuration, 8, new[] { "a", "b" });
            MultiplayerAdventureModel second = new MultiplayerAdventureModel(configuration, 8, new[] { "a", "b" });
            first.SelectNode(0);
            second.SelectNode(0);
            for (int index = 0; index < 40; index++)
            {
                first.UseSkill(1, 0);
                second.UseSkill(1, 0);
                first.Tick(configuration.SimulationStep);
                second.Tick(configuration.SimulationStep);
                Assert.That(first.Players.Select(player => player.Health),
                    Is.EqualTo(second.Players.Select(player => player.Health)));
                Assert.That(first.Players[0].Enemies.Select(enemy => enemy.Health),
                    Is.EqualTo(second.Players[0].Enemies.Select(enemy => enemy.Health)));
            }
        }

        [Test]
        public void SharedEnemyActionDamagesOnlyOneLivingPlayer()
        {
            AdventureConfiguration configuration = AdventureModelScenarioHelper.WinningConfiguration();
            configuration.SimulationStep = 0.125f;
            configuration.ProjectileTravelTime = 0.125f;
            configuration.BasicAttack.CastTime = 10;
            foreach (EnemyDefinition enemy in configuration.Enemies)
            {
                enemy.CastTime = 0.125f;
                enemy.Damage = 1;
                enemy.SpecialSkill = null;
            }
            MultiplayerAdventureModel party = new MultiplayerAdventureModel(configuration, 7, new[] { "a", "b" });
            party.SelectNode(0);
            party.Tick(0.25f);
            Assert.That(party.Players.Sum(player => player.Health), Is.EqualTo(configuration.StartingHealth * 2 - 1));
            Assert.That(party.Players.Count(player => player.Health < configuration.StartingHealth), Is.EqualTo(1));
        }

        [Test]
        public void FourPlayersCanCompleteAllThreeFloorsWithIndividualChoices()
        {
            AdventureConfiguration configuration = AdventureModelScenarioHelper.WinningConfiguration();
            configuration.BasicAttack.Damage = 100000;
            configuration.StartingHealth = 100000;
            MultiplayerAdventureModel party = new MultiplayerAdventureModel(
                configuration, 19, new[] { "a", "b", "c", "d" });
            for (int action = 0; action < 1000
                && !party.Players.All(player => player.Phase == AdventurePhase.Complete); action++)
            {
                if (party.CanVote) party.SelectNode(0);
                party.Tick(30);
                for (int player = 0; player < party.Players.Count; player++)
                {
                    if (party.Players[player].Phase == AdventurePhase.SkillReward) party.SelectReward(player, 0);
                    if (party.Players[player].Phase == AdventurePhase.NodeChoice) party.ResolveNode(player, 0);
                }
            }
            Assert.That(party.Players.All(player => player.Phase == AdventurePhase.Complete), Is.True);
            Assert.That(party.Players.Select(player => player.GetFloorPath(2).Count).Distinct().Count(), Is.EqualTo(1));
        }

        [Test]
        public void EnemyRandomlyTargetsLivingMembersAndSkipsDefeatedMembers()
        {
            AdventureConfiguration configuration = AdventureModelScenarioHelper.WinningConfiguration();
            configuration.SimulationStep = 0.125f;
            configuration.ProjectileTravelTime = 0.125f;
            configuration.BasicAttack.CastTime = 100;
            foreach (EnemyDefinition enemy in configuration.Enemies)
            {
                enemy.CastTime = 0.125f;
                enemy.Damage = 1;
                enemy.SpecialSkill = null;
            }
            AdventureModelScenarioHelper.SetSingleEnemy(configuration);
            MultiplayerAdventureModel party = new MultiplayerAdventureModel(
                configuration, 29, new[] { "a", "b", "c", "d" });
            party.SelectNode(0);
            party.Players[0].Battle.ReceivePartyDamage(configuration.StartingHealth);
            party.Tick(8);
            Assert.That(party.Players[0].Health, Is.Zero);
            Assert.That(
                party.Players.Skip(1).Count(player => player.Health < configuration.StartingHealth), Is.GreaterThan(1));
        }
    }
}
