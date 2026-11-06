using System.Linq;
using NUnit.Framework;

namespace WebRPG.Tests
{
    // 驗證鎖定目標、單一施放動作、投射物停表、敵人意圖與戰鬥速度。
    public sealed class AdventureBattleFlowTests
    {
        [Test]
        public void BattleStartsWithReadySkillsAndOneBasicAttackPerActor()
        {
            AdventureModel model = AdventureBattleTestHelper.StartBattle();
            Assert.That(model.Skills.All(skill => skill.RemainingCooldown == 0), Is.True);
            Assert.That(AdventureBattleTestHelper.Read<int>(model, "LockedEnemyIndex"), Is.Zero);
            Assert.That(AdventureBattleTestHelper.PlayerRemaining(model), Is.EqualTo(2));
            Assert.That(AdventureBattleTestHelper.Read<int>(
                AdventureBattleTestHelper.Read<object>(model, "PlayerAction"), "SkillIndex"), Is.EqualTo(-1));
            foreach (AdventureEnemyState enemy in model.Enemies)
            {
                Assert.That(AdventureBattleTestHelper.Read<float>(
                    AdventureBattleTestHelper.Read<object>(enemy, "Action"), "RemainingCastTime"), Is.EqualTo(10));
            }
        }

        [Test]
        public void LockPersistsUntilTargetDiesThenChoosesLowestCurrentHealth()
        {
            AdventureConfiguration configuration = AdventureBattleTestHelper.Configuration();
            configuration.Enemies[0].Health = 6;
            configuration.Enemies[1].Health = 12;
            configuration.Enemies[2].Health = 24;
            AdventureModel model = AdventureBattleTestHelper.StartBattle(configuration);
            AdventureBattleTestHelper.Invoke(model, "LockEnemy", 2);
            model.Tick(0.5f);
            Assert.That(AdventureBattleTestHelper.Read<int>(model, "LockedEnemyIndex"), Is.EqualTo(2));
            AdventureBattleTestHelper.Invoke(model, "LockEnemy", 0);
            model.Tick(2);
            Assert.That(model.Enemies[0].Health, Is.Zero);
            Assert.That(AdventureBattleTestHelper.Read<int>(model, "LockedEnemyIndex"), Is.EqualTo(1));
            AdventureBattleTestHelper.Invoke(model, "LockEnemy", 0);
            AdventureBattleTestHelper.Invoke(model, "LockEnemy", -1);
            AdventureBattleTestHelper.Invoke(model, "LockEnemy", 99);
            Assert.That(AdventureBattleTestHelper.Read<int>(model, "LockedEnemyIndex"), Is.EqualTo(1));
        }

        [Test]
        public void ChangingLockKeepsCastProgressAndUsesNewTargetAtLaunch()
        {
            AdventureModel model = AdventureBattleTestHelper.StartBattle();
            model.Tick(0.5f);
            AdventureBattleTestHelper.Invoke(model, "LockEnemy", 2);
            Assert.That(AdventureBattleTestHelper.PlayerRemaining(model), Is.EqualTo(1.5f));
            model.Tick(2);
            Assert.That(model.Enemies.Select(enemy => enemy.Health), Is.EqualTo(new[] { 100, 100, 94 }));
        }

        [Test]
        public void ProjectileKeepsItsTargetWhenLockChangesAfterLaunch()
        {
            AdventureModel model = AdventureBattleTestHelper.StartBattle();
            model.Tick(2);
            Assert.That(model.Enemies[0].Health, Is.EqualTo(100));
            AdventureBattleTestHelper.Invoke(model, "LockEnemy", 1);
            model.Tick(0.5f);
            Assert.That(model.Enemies.Select(enemy => enemy.Health), Is.EqualTo(new[] { 94, 100, 100 }));
        }

        [Test]
        public void ReadySkillCancelsBasicAttackButCannotInterruptAnotherSkill()
        {
            AdventureConfiguration configuration = AdventureBattleTestHelper.Configuration();
            configuration.StartingSkills = configuration.Skills.Take(2).Select(skill => skill.Id).ToArray();
            AdventureModel model = AdventureBattleTestHelper.StartBattle(configuration);
            model.Tick(1);
            Assert.That((bool)AdventureBattleTestHelper.Invoke(model, "TryUseSkill", 0), Is.True);
            Assert.That(AdventureBattleTestHelper.PlayerRemaining(model), Is.EqualTo(3));
            model.Tick(1);
            Assert.That((bool)AdventureBattleTestHelper.Invoke(model, "TryUseSkill", 1), Is.False);
            Assert.That(AdventureBattleTestHelper.PlayerRemaining(model), Is.EqualTo(2));
            Assert.That(model.Enemies.All(enemy => enemy.Health == 100), Is.True);
            model.Tick(2.5f);
            Assert.That(model.Enemies[0].Health, Is.EqualTo(80));
            Assert.That(model.Skills[1].RemainingCooldown, Is.Zero);
        }

        [Test]
        public void SkillCompletionStartsCooldownAndReturnsToBasicAttack()
        {
            AdventureModel model = AdventureBattleTestHelper.StartBattle();
            Assert.That((bool)AdventureBattleTestHelper.Invoke(model, "TryUseSkill", 0), Is.True);
            model.Tick(3);
            Assert.That(model.Skills[0].RemainingCooldown, Is.EqualTo(4));
            Assert.That(AdventureBattleTestHelper.Read<int>(
                AdventureBattleTestHelper.Read<object>(model, "PlayerAction"), "SkillIndex"), Is.EqualTo(-1));
            Assert.That(AdventureBattleTestHelper.PlayerRemaining(model), Is.EqualTo(2));
            model.Tick(0.5f);
            Assert.That(model.Enemies[0].Health, Is.EqualTo(80));
            Assert.That((bool)AdventureBattleTestHelper.Invoke(model, "TryUseSkill", 0), Is.False);
            Assert.That(AdventureBattleTestHelper.PlayerRemaining(model), Is.EqualTo(2));
            model.Tick(0.125f);
            Assert.That(model.Skills[0].RemainingCooldown, Is.EqualTo(3.875f));
        }

        [Test]
        public void BasicAttackRepeatsWithoutCooldownAndSkillsNeverAutoCast()
        {
            AdventureModel model = AdventureBattleTestHelper.StartBattle();
            model.Tick(2.5f);
            Assert.That(model.Enemies[0].Health, Is.EqualTo(94));
            Assert.That(AdventureBattleTestHelper.PlayerRemaining(model), Is.EqualTo(2));
            model.Tick(2);
            Assert.That(model.Enemies[0].Health, Is.EqualTo(94));
            model.Tick(0.5f);
            Assert.That(model.Enemies[0].Health, Is.EqualTo(88));
            Assert.That(model.Skills[0].RemainingCooldown, Is.Zero);
        }

        [Test]
        public void ProjectileFreezesEveryCastCooldownAndBattleDeadlineUntilImpact()
        {
            AdventureConfiguration configuration = AdventureBattleTestHelper.Configuration();
            configuration.BattleTimeLimit = 3.125f;
            AdventureModel model = AdventureBattleTestHelper.StartBattle(configuration);
            AdventureBattleTestHelper.Invoke(model, "TryUseSkill", 0);
            model.Tick(3);
            float enemyRemaining = AdventureBattleTestHelper.Read<float>(
                AdventureBattleTestHelper.Read<object>(model.Enemies[0], "Action"), "RemainingCastTime");
            model.Tick(0.25f);
            Assert.That(AdventureBattleTestHelper.PlayerRemaining(model), Is.EqualTo(2));
            Assert.That(model.Skills[0].RemainingCooldown, Is.EqualTo(4));
            Assert.That(AdventureBattleTestHelper.Read<float>(
                AdventureBattleTestHelper.Read<object>(model.Enemies[0], "Action"), "RemainingCastTime"),
                Is.EqualTo(enemyRemaining));
            Assert.That(model.Enemies[0].Health, Is.EqualTo(100));
            model.Tick(0.25f);
            Assert.That(model.Phase, Is.EqualTo(AdventurePhase.Battle));
            Assert.That(model.Enemies[0].Health, Is.EqualTo(80));
            model.Tick(0.125f);
            Assert.That(model.Phase, Is.EqualTo(AdventurePhase.Failed));
        }

        [Test]
        public void PauseAndSpeedApplyToCastingAndProjectileFlight()
        {
            AdventureModel model = AdventureBattleTestHelper.StartBattle();
            model.Tick(0.5f);
            AdventureBattleTestHelper.Invoke(model, "SetBattleSpeed", 0f);
            model.Tick(10);
            Assert.That(AdventureBattleTestHelper.PlayerRemaining(model), Is.EqualTo(1.5f));
            AdventureBattleTestHelper.Invoke(model, "SetBattleSpeed", 2f);
            model.Tick(0.75f);
            Assert.That(model.Enemies[0].Health, Is.EqualTo(100));
            AdventureBattleTestHelper.Invoke(model, "SetBattleSpeed", 0f);
            model.Tick(10);
            Assert.That(model.Enemies[0].Health, Is.EqualTo(100));
            AdventureBattleTestHelper.Invoke(model, "SetBattleSpeed", 2f);
            model.Tick(0.125f);
            Assert.That(model.Enemies[0].Health, Is.EqualTo(100));
            AdventureBattleTestHelper.Invoke(model, "SetBattleSpeed", 1f);
            model.Tick(0.25f);
            Assert.That(model.Enemies[0].Health, Is.EqualTo(94));
        }

        [Test]
        public void EnemyTelegraphsSpecialAfterConfiguredBasicAttacksAndHealsItself()
        {
            AdventureConfiguration configuration = AdventureBattleTestHelper.Configuration();
            foreach (EnemyDefinition enemy in configuration.Enemies)
            {
                AdventureBattleTestHelper.Populate(enemy, "{\'CastTime\':100}");
            }
            AdventureBattleTestHelper.Populate(configuration.Enemies[0],
                "{'CastTime':4,'SpecialSkill':{'Id':'guard-heal','Name':'守衛回復'," +
                "'CastTime':3,'Cooldown':8,'BasicAttacksBeforeUse':2," +
                "'Effects':[{'Type':'Heal','Amount':20,'PerLevel':0}]}}");
            AdventureBattleTestHelper.Populate(configuration.Skills[0], "{'CastTime':100}");
            AdventureModel model = AdventureBattleTestHelper.StartBattle(configuration);
            Assert.That(AdventureBattleTestHelper.Read<int>(
                model.Enemies[0], "BasicAttacksUntilSpecial"), Is.EqualTo(2));
            model.Tick(2.5f);
            Assert.That(model.Enemies[0].Health, Is.EqualTo(94));
            Assert.That((bool)AdventureBattleTestHelper.Invoke(model, "TryUseSkill", 0), Is.True);
            for (int step = 0; step < 256 && AdventureBattleTestHelper.Read<string>(
                AdventureBattleTestHelper.Read<object>(model.Enemies[0], "Action"), "Name") != "守衛回復"; step++)
            {
                model.Tick(0.125f);
            }
            Assert.That(AdventureBattleTestHelper.Read<string>(
                AdventureBattleTestHelper.Read<object>(model.Enemies[0], "Action"), "Name"), Is.EqualTo("守衛回復"));
            Assert.That(model.Enemies[0].Health, Is.EqualTo(94));
            model.Tick(3.5f);
            Assert.That(model.Enemies[0].Health, Is.EqualTo(100));
            Assert.That(AdventureBattleTestHelper.Read<int>(
                model.Enemies[0], "BasicAttacksUntilSpecial"), Is.EqualTo(2));
        }

        [Test]
        public void BattleResultDoesNotDependOnFramePartition()
        {
            AdventureModel first = AdventureBattleTestHelper.StartBattle();
            AdventureModel second = AdventureBattleTestHelper.StartBattle();
            AdventureBattleTestHelper.Invoke(first, "TryUseSkill", 0);
            AdventureBattleTestHelper.Invoke(second, "TryUseSkill", 0);
            first.Tick(12);
            for (int step = 0; step < 96; step++)
            {
                second.Tick(0.125f);
            }
            Assert.That(first.Phase, Is.EqualTo(second.Phase));
            Assert.That(first.Health, Is.EqualTo(second.Health));
            Assert.That(first.Enemies.Select(enemy => enemy.Health),
                Is.EqualTo(second.Enemies.Select(enemy => enemy.Health)));
            Assert.That(first.Skills[0].RemainingCooldown, Is.EqualTo(second.Skills[0].RemainingCooldown));
            Assert.That(AdventureBattleTestHelper.PlayerRemaining(first),
                Is.EqualTo(AdventureBattleTestHelper.PlayerRemaining(second)));
        }
    }
}
