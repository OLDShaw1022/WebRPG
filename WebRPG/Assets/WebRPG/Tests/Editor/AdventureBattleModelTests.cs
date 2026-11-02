using System;
using NUnit.Framework;

namespace WebRPG.Tests
{
    // 驗證獨立戰鬥的結束結果、投射物序號與停止後的狀態保護。
    public sealed class AdventureBattleModelTests
    {
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
