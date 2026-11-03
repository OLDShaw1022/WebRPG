using System;
using NUnit.Framework;

namespace WebRPG.Tests
{
    // 驗證獨立戰鬥的結束結果、投射物序號與停止後的狀態保護。
    public sealed class AdventureBattleModelTests
    {
        [Test]
        public void SingleAndOneMemberPartyFollowTheSameSchedule()
        {
            AdventureConfiguration configuration = AdventureBattleTestHelper.Configuration();
            configuration.Enemies[0].CastTime = 1;
            AdventureSkillState soloSkill = new AdventureSkillState(configuration.Skills[0]);
            AdventureSkillState partySkill = new AdventureSkillState(configuration.Skills[0]);
            AdventureBattleModel solo = new AdventureBattleModel(configuration,
                new[] { soloSkill }, new[] { configuration.Enemies[0] }, 100, 100);
            AdventureBattleModel member = new AdventureBattleModel(configuration,
                new[] { partySkill }, new[] { configuration.Enemies[0] }, 100, 100);
            AdventurePartyBattleModel party = new AdventurePartyBattleModel(configuration,
                new[] { member }, new Random(17));
            Assert.That(solo.TryUseSkill(0), Is.True);
            Assert.That(member.TryUseSkill(0), Is.True);
            for (int step = 0; step < 160; step++)
            {
                solo.Tick(configuration.SimulationStep);
                party.Tick(configuration.SimulationStep);
                Assert.That(member.Outcome, Is.EqualTo(solo.Outcome));
                Assert.That(member.Health, Is.EqualTo(solo.Health));
                Assert.That(member.Enemies[0].Health, Is.EqualTo(solo.Enemies[0].Health));
                Assert.That(member.PlayerAction?.RemainingCastTime, Is.EqualTo(solo.PlayerAction?.RemainingCastTime));
                Assert.That(member.Projectile?.Id, Is.EqualTo(solo.Projectile?.Id));
                Assert.That(member.Projectile?.RemainingTime, Is.EqualTo(solo.Projectile?.RemainingTime));
                Assert.That(partySkill.RemainingCooldown, Is.EqualTo(soloSkill.RemainingCooldown));
            }
        }

        [Test]
        public void PartyFlightFreezesAllActorsAndDeadlineThenLaunchesNextReadyPlayer()
        {
            AdventureConfiguration configuration = AdventureBattleTestHelper.Configuration();
            configuration.Skills[0].CastTime = 2;
            configuration.BattleTimeLimit = 2.125f;
            AdventureSkillState skill = new AdventureSkillState(configuration.Skills[0]);
            AdventureBattleModel first = new AdventureBattleModel(configuration,
                new[] { skill }, new[] { configuration.Enemies[0] }, 100, 100);
            AdventureBattleModel second = new AdventureBattleModel(configuration,
                Array.Empty<AdventureSkillState>(), new[] { configuration.Enemies[0] }, 100, 100);
            AdventurePartyBattleModel party = new AdventurePartyBattleModel(configuration,
                new[] { first, second }, new Random(17));
            first.TryUseSkill(0);
            party.Tick(2);
            float enemyCast = first.Enemies[0].Action.RemainingCastTime;
            party.Tick(0.25f);
            Assert.That(first.PlayerAction.RemainingCastTime, Is.EqualTo(2));
            Assert.That(second.PlayerAction.RemainingCastTime, Is.Zero);
            Assert.That(skill.RemainingCooldown, Is.EqualTo(configuration.Skills[0].Cooldown));
            Assert.That(first.Enemies[0].Action.RemainingCastTime, Is.EqualTo(enemyCast));
            Assert.That(party.SourcePlayerIndex, Is.Zero);
            party.Tick(0.25f);
            Assert.That(party.SourcePlayerIndex, Is.EqualTo(1));
            Assert.That(party.Outcome, Is.EqualTo(AdventureBattleOutcome.Running));
            party.Tick(configuration.ProjectileTravelTime);
            party.Tick(configuration.SimulationStep);
            Assert.That(party.Outcome, Is.EqualTo(AdventureBattleOutcome.TimedOut));
            Assert.That(first.Projectile, Is.Null);
            Assert.That(second.PlayerAction, Is.Null);
            Assert.That(skill.RemainingCooldown, Is.Zero);
        }

        [Test]
        public void PartyOwnsEnemyStatesAndParticipantRosterIndependently()
        {
            AdventureConfiguration configuration = AdventureBattleTestHelper.Configuration();
            AdventureBattleModel first = new AdventureBattleModel(configuration,
                Array.Empty<AdventureSkillState>(), new[] { configuration.Enemies[0] }, 100, 100);
            AdventureBattleModel second = new AdventureBattleModel(configuration,
                Array.Empty<AdventureSkillState>(), new[] { configuration.Enemies[0] }, 100, 100);
            AdventureEnemyState previousEnemy = first.Enemies[0];
            AdventureBattleModel[] members = { first, second };
            AdventurePartyBattleModel party = new AdventurePartyBattleModel(configuration, members, new Random(17));
            members[0] = null;
            Assert.That(first.Enemies[0], Is.Not.SameAs(previousEnemy));
            Assert.That(second.Enemies, Is.SameAs(first.Enemies));
            party.Tick(configuration.BasicAttack.CastTime);
            Assert.That(party.SourcePlayerIndex, Is.Zero);
            first.Stop();
            Assert.That(party.Outcome, Is.EqualTo(AdventureBattleOutcome.Stopped));
            Assert.That(second.Outcome, Is.EqualTo(AdventureBattleOutcome.Stopped));
            Assert.That(second.Projectile, Is.Null);
            Assert.That(second.PlayerAction, Is.Null);
        }

        [Test]
        public void PartyPlayerTickCannotAdvanceTheSharedBattle()
        {
            AdventureConfiguration configuration = AdventureBattleTestHelper.Configuration();
            AdventureBattleModel first = new AdventureBattleModel(configuration,
                Array.Empty<AdventureSkillState>(), new[] { configuration.Enemies[0] }, 100, 100);
            AdventureBattleModel second = new AdventureBattleModel(configuration,
                Array.Empty<AdventureSkillState>(), new[] { configuration.Enemies[0] }, 100, 100);
            AdventurePartyBattleModel party = new AdventurePartyBattleModel(configuration,
                new[] { first, second }, new Random(17));
            first.Tick(configuration.BasicAttack.CastTime);
            second.Tick(configuration.BasicAttack.CastTime);
            Assert.That(party.Projectile, Is.Null);
            Assert.That(first.Projectile, Is.Null);
            Assert.That(first.PlayerAction.RemainingCastTime, Is.EqualTo(configuration.BasicAttack.CastTime));
            party.Tick(configuration.BasicAttack.CastTime);
            Assert.That(party.Projectile, Is.SameAs(first.Projectile));
            Assert.That(second.Projectile, Is.SameAs(first.Projectile));
        }

        [Test]
        public void PartyProjectileSequenceIsUniqueAcrossPlayers()
        {
            AdventureConfiguration configuration = AdventureBattleTestHelper.Configuration();
            AdventureBattleModel first = new AdventureBattleModel(configuration,
                Array.Empty<AdventureSkillState>(), new[] { configuration.Enemies[0] }, 100, 100, 0, 41);
            AdventureBattleModel second = new AdventureBattleModel(configuration,
                Array.Empty<AdventureSkillState>(), new[] { configuration.Enemies[0] }, 100, 100, 0, 41);
            AdventurePartyBattleModel party = new AdventurePartyBattleModel(configuration,
                new[] { first, second }, new Random(17));
            party.Tick(configuration.BasicAttack.CastTime);
            Assert.That(party.Projectile.Id, Is.EqualTo(42));
            party.Tick(configuration.ProjectileTravelTime);
            Assert.That(party.SourcePlayerIndex, Is.EqualTo(1));
            Assert.That(party.Projectile.Id, Is.EqualTo(43));
            Assert.That(first.ProjectileSequence, Is.EqualTo(43));
            Assert.That(second.ProjectileSequence, Is.EqualTo(43));
        }

        [Test]
        public void VictoryResolvesProjectileAndPreservesSequenceAcrossBattles()
        {
            AdventureConfiguration configuration = AdventureBattleTestHelper.Configuration();
            EnemyDefinition enemy = configuration.Enemies[0];
            enemy.Health = configuration.BasicAttack.Damage;
            AdventureBattleModel battle = new AdventureBattleModel(configuration,
                Array.Empty<AdventureSkillState>(), new[] { enemy }, 100, 100, 0, 41);
            battle.Tick(configuration.BasicAttack.CastTime);
            Assert.That(battle.Projectile.Id, Is.EqualTo(42));
            Assert.That(battle.Enemies[0].Health, Is.EqualTo(enemy.Health));
            battle.Tick(configuration.ProjectileTravelTime);
            Assert.That(battle.Outcome, Is.EqualTo(AdventureBattleOutcome.Won));
            Assert.That(battle.Enemies[0].Health, Is.Zero);
            Assert.That(battle.PlayerAction, Is.Null);
            Assert.That(battle.Projectile, Is.Null);
            battle.Tick(100);
            Assert.That(battle.ProjectileSequence, Is.EqualTo(42));
        }

        [Test]
        public void StopClearsPendingEffectsAndCooldownAndPreventsFurtherCommands()
        {
            AdventureConfiguration configuration = AdventureBattleTestHelper.Configuration();
            AdventureSkillState skill = new AdventureSkillState(configuration.Skills[0]);
            AdventureBattleModel battle = new AdventureBattleModel(configuration,
                new[] { skill }, new[] { configuration.Enemies[0] }, 100, 100);
            Assert.That(battle.TryUseSkill(0), Is.True);
            battle.Tick(skill.Definition.CastTime);
            Assert.That(battle.Projectile, Is.Not.Null);
            Assert.That(skill.RemainingCooldown, Is.GreaterThan(0));
            battle.Stop();
            battle.Tick(100);
            battle.LockEnemy(0);
            Assert.That(battle.TryUseSkill(0), Is.False);
            Assert.That(battle.Outcome, Is.EqualTo(AdventureBattleOutcome.Stopped));
            Assert.That(battle.LockedEnemyIndex, Is.EqualTo(-1));
            Assert.That(battle.Projectile, Is.Null);
            Assert.That(battle.PlayerAction, Is.Null);
            Assert.That(skill.RemainingCooldown, Is.Zero);
            Assert.That(battle.Enemies[0].Health, Is.EqualTo(configuration.Enemies[0].Health));
        }

        [Test]
        public void DeadlineAndDefeatHaveDistinctOutcomes()
        {
            AdventureConfiguration configuration = AdventureBattleTestHelper.Configuration();
            configuration.BattleTimeLimit = configuration.SimulationStep;
            AdventureBattleModel timedOut = new AdventureBattleModel(configuration,
                Array.Empty<AdventureSkillState>(), new[] { configuration.Enemies[0] }, 10, 20);
            timedOut.Tick(configuration.SimulationStep);
            Assert.That(timedOut.Outcome, Is.EqualTo(AdventureBattleOutcome.TimedOut));
            Assert.That(timedOut.Health, Is.EqualTo(10));
            configuration.BattleTimeLimit = 120;
            configuration.Enemies[0].CastTime = configuration.SimulationStep;
            configuration.Enemies[0].Damage = 10;
            AdventureBattleModel defeated = new AdventureBattleModel(configuration,
                Array.Empty<AdventureSkillState>(), new[] { configuration.Enemies[0] }, 10, 20);
            defeated.Tick(configuration.SimulationStep + configuration.ProjectileTravelTime);
            Assert.That(defeated.Outcome, Is.EqualTo(AdventureBattleOutcome.Defeated));
            Assert.That(defeated.Health, Is.Zero);
        }
    }
}
