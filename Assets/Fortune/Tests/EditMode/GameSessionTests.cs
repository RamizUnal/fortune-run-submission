using System;
using System.Collections.Generic;
using NUnit.Framework;
using Vertigo.Fortune.Domain;

namespace Vertigo.Fortune.Tests
{
    public sealed class GameSessionTests
    {
        private RewardDefinition coins;
        private RewardDefinition cash;
        private ConfigurableProvider provider;
        private DeterministicRandom random;
        private GameSession session;

        [SetUp]
        public void SetUp()
        {
            coins = new RewardDefinition("coins", "Gold", "icon_gold", "Currency", "Common", 100);
            cash = new RewardDefinition("cash", "Cash", "icon_cash", "Currency", "Rare", 10);
            provider = new ConfigurableProvider(CreateWheel);
            random = new DeterministicRandom();
            session = new GameSession(provider, random);
        }

        [Test]
        public void NewRun_StartsReadyAtFirstBronzeZoneWithNothingToLose()
        {
            Assert.That(session.Zone, Is.EqualTo(1));
            Assert.That(session.Kind, Is.EqualTo(ZoneKind.Bronze));
            Assert.That(session.State, Is.EqualTo(RunState.Ready));
            Assert.That(session.Rewards, Is.Empty);
            Assert.That(session.CanCashOut, Is.False);
            Assert.That(session.Slices.Count, Is.EqualTo(8));
        }

        [Test]
        public void Spin_ChoosesExactlyOnceAndDefersTheRewardUntilAnimationResolves()
        {
            random.Value = 2;
            Assert.That(session.Spin(), Is.EqualTo(2));
            Assert.That(random.LastExclusiveMaximum, Is.EqualTo(8));
            Assert.That(random.Calls, Is.EqualTo(1));
            Assert.That(session.State, Is.EqualTo(RunState.Spinning));
            Assert.That(session.Rewards, Is.Empty);
            random.Value = 7; // Animation completion must not reroll the result.
            Assert.That(session.ResolveSpin(), Is.SameAs(session.Slices[2]));
            Assert.That(random.Calls, Is.EqualTo(1));
            Assert.That(session.State, Is.EqualTo(RunState.RewardPending));
            Assert.That(session.Rewards[0].Amount, Is.EqualTo(100));
        }

        [Test]
        public void ResolvingAgain_DoesNotDuplicateAWinningsGrant()
        {
            WinAndResolve();
            Assert.Throws<InvalidOperationException>(() => session.ResolveSpin());
            Assert.That(session.Rewards[0].Amount, Is.EqualTo(100));
        }

        [Test]
        public void ResolvingBeforeASpin_IsRejectedWithoutChangingTheRun()
        {
            Assert.Throws<InvalidOperationException>(() => session.ResolveSpin());
            Assert.That(session.State, Is.EqualTo(RunState.Ready));
            Assert.That(session.Rewards, Is.Empty);
        }

        [Test]
        public void WhileSpinning_AllOtherPlayerActionsAreRejected()
        {
            session.Spin();
            Assert.Throws<InvalidOperationException>(() => session.Spin());
            Assert.Throws<InvalidOperationException>(() => session.Advance());
            Assert.Throws<InvalidOperationException>(() => session.CashOut());
            Assert.Throws<InvalidOperationException>(() => session.Restart());
            Assert.Throws<InvalidOperationException>(() => session.Revive());
            Assert.That(session.State, Is.EqualTo(RunState.Spinning));
            Assert.That(random.Calls, Is.EqualTo(1));
        }

        [Test]
        public void RewardPending_RequiresAcknowledgementBeforeAnotherSpin()
        {
            WinAndResolve();
            Assert.Throws<InvalidOperationException>(() => session.Spin());
            Assert.Throws<InvalidOperationException>(() => session.CashOut());
            Assert.Throws<InvalidOperationException>(() => session.Restart());
            session.Advance();
            Assert.That(session.State, Is.EqualTo(RunState.Ready));
            Assert.That(session.Zone, Is.EqualTo(2));
        }

        [Test]
        public void AdvanceBeforeAWin_DoesNotSkipTheRisk()
        {
            Assert.Throws<InvalidOperationException>(() => session.Advance());
            Assert.That(session.Zone, Is.EqualTo(1));
        }

        [Test]
        public void RepeatedRewards_StackByStableIdentityAcrossZones()
        {
            WinAndResolve();
            coins = new RewardDefinition("coins", "Gold", "icon_gold", "Currency", "Common", 100);
            session.Advance();
            WinAndResolve();
            Assert.That(session.Rewards.Count, Is.EqualTo(1));
            Assert.That(session.Rewards[0].Definition.Id, Is.EqualTo("coins"));
            Assert.That(session.Rewards[0].Amount, Is.EqualTo(300));
        }

        [Test]
        public void DifferentRewards_KeepTheirOwnStacks()
        {
            WinAndResolve();
            session.Advance();
            random.Value = 1;
            session.Spin();
            session.ResolveSpin();
            Assert.That(session.Rewards.Count, Is.EqualTo(2));
            Assert.That(session.Rewards[0].Definition.Id, Is.EqualTo("coins"));
            Assert.That(session.Rewards[0].Amount, Is.EqualTo(100));
            Assert.That(session.Rewards[1].Definition.Id, Is.EqualTo("cash"));
            Assert.That(session.Rewards[1].Amount, Is.EqualTo(20));
        }

        [Test]
        public void Bomb_LosesEveryPreviouslyCollectedReward()
        {
            ReachZone(3);
            Assert.That(session.Rewards[0].Amount, Is.EqualTo(300));
            random.Value = 7;
            session.Spin();
            var result = session.ResolveSpin();
            Assert.That(result.IsBomb, Is.True);
            Assert.That(session.State, Is.EqualTo(RunState.Failed));
            Assert.That(session.Rewards, Is.Empty);
            Assert.That(session.CanCashOut, Is.False);
        }

        [Test]
        public void FailedRun_RejectsPlayUntilRestartedOrRevived()
        {
            random.Value = 7;
            session.Spin();
            session.ResolveSpin();
            Assert.Throws<InvalidOperationException>(() => session.Spin());
            Assert.Throws<InvalidOperationException>(() => session.ResolveSpin());
            Assert.Throws<InvalidOperationException>(() => session.Advance());
            Assert.Throws<InvalidOperationException>(() => session.CashOut());
            session.Restart();
            Assert.That(session.State, Is.EqualTo(RunState.Ready));
            Assert.That(session.Zone, Is.EqualTo(1));
            Assert.That(session.Rewards, Is.Empty);
        }

        [Test]
        public void Revive_RestoresAllLostStacksAndContinuesAtTheNextZone()
        {
            WinAndResolve();
            session.Advance();
            random.Value = 1;
            WinAndResolve();
            session.Advance();
            LoseAndResolve();
            var failedWheel = session.Slices;

            session.Revive();

            Assert.That(session.State, Is.EqualTo(RunState.Ready));
            Assert.That(session.Zone, Is.EqualTo(4));
            Assert.That(session.Slices, Is.Not.SameAs(failedWheel));
            Assert.That(session.Rewards.Count, Is.EqualTo(2));
            Assert.That(session.Rewards[0].Definition.Id, Is.EqualTo("coins"));
            Assert.That(session.Rewards[0].Amount, Is.EqualTo(100));
            Assert.That(session.Rewards[1].Definition.Id, Is.EqualTo("cash"));
            Assert.That(session.Rewards[1].Amount, Is.EqualTo(20));
            Assert.Throws<InvalidOperationException>(() => session.Revive());
            Assert.That(session.Rewards.Count, Is.EqualTo(2));
        }

        [Test]
        public void Revive_AtTheFirstZoneNeedsNoLootOrCurrency()
        {
            LoseAndResolve();
            session.Revive();

            Assert.That(session.Zone, Is.EqualTo(2));
            Assert.That(session.State, Is.EqualTo(RunState.Ready));
            Assert.That(session.Rewards, Is.Empty);
            random.Value = 0;
            WinAndResolve();
            Assert.That(session.Rewards[0].Amount, Is.EqualTo(200));
        }

        [Test]
        public void Revive_RepeatedLossesRestoreTheLatestLootWithoutDuplicatingIt()
        {
            ReachZone(2);
            LoseAndResolve();
            session.Revive();
            random.Value = 0;
            WinAndResolve();
            session.Advance();
            LoseAndResolve();
            session.Revive();

            Assert.That(session.Zone, Is.EqualTo(5));
            Assert.That(session.State, Is.EqualTo(RunState.Ready));
            Assert.That(session.Rewards.Count, Is.EqualTo(1));
            Assert.That(session.Rewards[0].Amount, Is.EqualTo(400));
            Assert.That(session.CanCashOut, Is.True);
            Assert.That(session.CashOut()[0].Amount, Is.EqualTo(400));
        }

        [Test]
        public void Revive_IsRejectedWhileReadyRewardPendingOrCashedOut()
        {
            Assert.Throws<InvalidOperationException>(() => session.Revive());
            Assert.That(session.Zone, Is.EqualTo(1));
            Assert.That(session.Rewards, Is.Empty);
            WinAndResolve();
            Assert.Throws<InvalidOperationException>(() => session.Revive());
            Assert.That(session.State, Is.EqualTo(RunState.RewardPending));
            Assert.That(session.Rewards[0].Amount, Is.EqualTo(100));
            session.Advance();
            ReachZone(5);
            session.CashOut();
            Assert.Throws<InvalidOperationException>(() => session.Revive());
            Assert.That(session.State, Is.EqualTo(RunState.CashedOut));
            Assert.That(session.Rewards[0].Amount, Is.EqualTo(1000));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Revive_WhenNextWheelFailsValidationLeavesTheRunAndRecoveryIntact(bool providerThrows)
        {
            ReachZone(3);
            LoseAndResolve();
            var failedWheel = session.Slices;
            provider.Factory = zone => providerThrows
                ? throw new InvalidOperationException("Content unavailable.")
                : new WheelSlice[0];

            Assert.Throws<InvalidOperationException>(() => session.Revive());
            Assert.That(session.State, Is.EqualTo(RunState.Failed));
            Assert.That(session.Zone, Is.EqualTo(3));
            Assert.That(session.Slices, Is.SameAs(failedWheel));
            Assert.That(session.Rewards, Is.Empty);

            provider.Factory = CreateWheel;
            session.Revive();
            Assert.That(session.Zone, Is.EqualTo(4));
            Assert.That(session.Rewards.Count, Is.EqualTo(1));
            Assert.That(session.Rewards[0].Amount, Is.EqualTo(300));
        }

        [Test]
        public void Restart_DiscardsLostLootBeforeTheNextRevive()
        {
            ReachZone(3);
            LoseAndResolve();
            session.Restart();
            LoseAndResolve();
            session.Revive();

            Assert.That(session.Zone, Is.EqualTo(2));
            Assert.That(session.State, Is.EqualTo(RunState.Ready));
            Assert.That(session.Rewards, Is.Empty);
        }

        [Test]
        public void RiskyZone_DoesNotPermitCashOutEvenWithRewards()
        {
            ReachZone(4);
            Assert.That(session.Rewards, Is.Not.Empty);
            Assert.That(session.CanCashOut, Is.False);
            Assert.Throws<InvalidOperationException>(() => session.CashOut());
        }

        [Test]
        public void SilverZone_OffersCashOutBeforeItsSpin()
        {
            ReachZone(5);
            Assert.That(session.Kind, Is.EqualTo(ZoneKind.Silver));
            Assert.That(session.CanCashOut, Is.True);
            var winnings = session.CashOut();
            Assert.That(session.State, Is.EqualTo(RunState.CashedOut));
            Assert.That(winnings.Count, Is.EqualTo(1));
            Assert.That(winnings[0].Amount, Is.EqualTo(1000));
            Assert.That(session.CanCashOut, Is.False);
        }

        [Test]
        public void GoldenZone_OverridesSilverAndPermitsCashOut()
        {
            ReachZone(30);
            Assert.That(session.Kind, Is.EqualTo(ZoneKind.Golden));
            Assert.That(session.CanCashOut, Is.True);
            Assert.That(session.CashOut()[0].Amount, Is.EqualTo(43500));
        }

        [Test]
        public void SafeZone_CashOutIsLockedDuringAnimationAndUnlockedWhenTheWheelStops()
        {
            ReachZone(5);
            Assert.That(session.CanCashOut, Is.True);
            session.Spin();
            Assert.That(session.CanCashOut, Is.False);
            Assert.Throws<InvalidOperationException>(() => session.CashOut());
            session.ResolveSpin();
            Assert.That(session.CanCashOut, Is.True);
            session.Advance();
            Assert.That(session.Zone, Is.EqualTo(6));
            Assert.That(session.CanCashOut, Is.False);
        }

        [TestCase(5, 1500)]
        [TestCase(30, 46500)]
        public void IdleSafeOrGoldenZone_CanCashOutIncludingItsJustWonReward(int zone, int expectedWinnings)
        {
            ReachZone(zone);
            WinAndResolve();
            Assert.That(session.State, Is.EqualTo(RunState.RewardPending));
            Assert.That(session.CanCashOut, Is.True);
            var winnings = session.CashOut();
            Assert.That(session.State, Is.EqualTo(RunState.CashedOut));
            Assert.That(winnings[0].Amount, Is.EqualTo(expectedWinnings));
        }

        [TestCase(5)]
        [TestCase(30)]
        public void SafeWheels_AwardARewardInTheUsualBombPosition(int zone)
        {
            ReachZone(zone);
            random.Value = 7;
            session.Spin();
            Assert.That(session.ResolveSpin().IsBomb, Is.False);
            Assert.That(session.State, Is.EqualTo(RunState.RewardPending));
            Assert.That(session.Rewards, Is.Not.Empty);
        }

        [Test]
        public void CashedOutRun_CannotPayOutTwiceOrContinueSpinning()
        {
            ReachZone(5);
            session.CashOut();
            Assert.Throws<InvalidOperationException>(() => session.CashOut());
            Assert.Throws<InvalidOperationException>(() => session.Spin());
            Assert.Throws<InvalidOperationException>(() => session.ResolveSpin());
            Assert.Throws<InvalidOperationException>(() => session.Advance());
        }

        [Test]
        public void RestartAfterCashOut_ResetsTheRunButKeepsThePayoutSnapshot()
        {
            ReachZone(5);
            var winnings = session.CashOut();
            session.Restart();
            Assert.That(session.Zone, Is.EqualTo(1));
            Assert.That(session.State, Is.EqualTo(RunState.Ready));
            Assert.That(session.Rewards, Is.Empty);
            Assert.That(winnings[0].Amount, Is.EqualTo(1000));
            WinAndResolve();
            Assert.That(winnings[0].Amount, Is.EqualTo(1000));
        }

        [Test]
        public void RestartInAnUnfinishedRun_IsRejected()
        {
            Assert.Throws<InvalidOperationException>(() => session.Restart());
            ReachZone(5);
            Assert.Throws<InvalidOperationException>(() => session.Restart());
            Assert.That(session.Rewards[0].Amount, Is.EqualTo(1000));
        }

        [TestCase(-1)]
        [TestCase(8)]
        public void InvalidRandomResult_DoesNotStartASpin(int value)
        {
            random.Value = value;
            Assert.Throws<InvalidOperationException>(() => session.Spin());
            Assert.That(session.State, Is.EqualTo(RunState.Ready));
            Assert.That(session.Rewards, Is.Empty);
        }

        [Test]
        public void ProviderCannotChangeAnAlreadySelectedWheel()
        {
            var authored = CreateWheel(1);
            provider.Factory = zone => authored;
            session = new GameSession(provider, random);
            session.Spin();
            authored[0] = new WheelSlice(null, 0, true);
            Assert.That(session.ResolveSpin().IsBomb, Is.False);
            Assert.That(session.State, Is.EqualTo(RunState.RewardPending));
        }

        [Test]
        public void RewardAndSliceCollectionsCannotBeMutatedByPresentation()
        {
            WinAndResolve();
            var rewardCollection = (IList<RewardStack>)session.Rewards;
            var sliceCollection = (IList<WheelSlice>)session.Slices;
            Assert.Throws<NotSupportedException>(() => rewardCollection.Clear());
            Assert.Throws<NotSupportedException>(() => sliceCollection[0] = new WheelSlice(null, 0, true));
        }

        [Test]
        public void InvalidNextWheel_DoesNotPartiallyAdvanceTheRun()
        {
            WinAndResolve();
            var firstWheel = session.Slices;
            provider.Factory = zone => new WheelSlice[0];
            Assert.Throws<InvalidOperationException>(() => session.Advance());
            Assert.That(session.State, Is.EqualTo(RunState.RewardPending));
            Assert.That(session.Zone, Is.EqualTo(1));
            Assert.That(session.Slices, Is.SameAs(firstWheel));
            Assert.That(session.Rewards[0].Amount, Is.EqualTo(100));
            provider.Factory = CreateWheel;
            session.Advance();
            Assert.That(session.Zone, Is.EqualTo(2));
        }

        [TestCase(0)]
        [TestCase(7)]
        [TestCase(9)]
        public void WheelWithWrongSliceCount_IsRejected(int count)
        {
            provider.Factory = zone => new WheelSlice[count];
            Assert.Throws<InvalidOperationException>(() => new GameSession(provider, random));
        }

        [Test]
        public void NullWheel_IsRejected()
        {
            provider.Factory = zone => null;
            Assert.Throws<InvalidOperationException>(() => new GameSession(provider, random));
        }

        [Test]
        public void NullSlice_IsRejected()
        {
            var wheel = CreateWheel(1);
            wheel[2] = null;
            provider.Factory = zone => wheel;
            Assert.Throws<InvalidOperationException>(() => new GameSession(provider, random));
        }

        [TestCase(0)]
        [TestCase(2)]
        public void BronzeWheelWithoutExactlyOneBomb_IsRejected(int bombCount)
        {
            var wheel = CreateWheel(5);
            for (var i = 0; i < bombCount; i++) wheel[i] = new WheelSlice(null, 0, true);
            provider.Factory = zone => wheel;
            Assert.Throws<InvalidOperationException>(() => new GameSession(provider, random));
        }

        [TestCase(5)]
        [TestCase(30)]
        public void BombInSafeOrGoldenWheel_IsRejectedBeforeEnteringTheZone(int zone)
        {
            ReachZone(zone - 1);
            WinAndResolve();
            provider.Factory = requested => CreateWheel(1);
            Assert.Throws<InvalidOperationException>(() => session.Advance());
            Assert.That(session.Zone, Is.EqualTo(zone - 1));
            Assert.That(session.State, Is.EqualTo(RunState.RewardPending));
        }

        [Test]
        public void NullDependencies_AreRejectedImmediately()
        {
            Assert.Throws<ArgumentNullException>(() => new GameSession(null, random));
            Assert.Throws<ArgumentNullException>(() => new GameSession(provider, null));
        }

        private void ReachZone(int target)
        {
            random.Value = 0;
            while (session.Zone < target)
            {
                WinAndResolve();
                session.Advance();
            }
        }

        private void WinAndResolve()
        {
            session.Spin();
            session.ResolveSpin();
        }

        private void LoseAndResolve()
        {
            random.Value = 7;
            session.Spin();
            Assert.That(session.ResolveSpin().IsBomb, Is.True);
        }

        private WheelSlice[] CreateWheel(int zone)
        {
            var wheel = new WheelSlice[8];
            for (var i = 0; i < wheel.Length; i++)
            {
                var definition = i == 1 ? cash : coins;
                wheel[i] = new WheelSlice(definition, definition.BaseAmount * zone);
            }
            if (!ZoneRules.IsSafe(zone)) wheel[7] = new WheelSlice(null, 0, true);
            return wheel;
        }

        private sealed class ConfigurableProvider : IWheelProvider
        {
            public Func<int, IReadOnlyList<WheelSlice>> Factory;
            public ConfigurableProvider(Func<int, IReadOnlyList<WheelSlice>> factory) { Factory = factory; }
            public IReadOnlyList<WheelSlice> GetSlices(int zone) { return Factory(zone); }
        }

        private sealed class DeterministicRandom : IRandomSource
        {
            public int Value;
            public int Calls;
            public int LastExclusiveMaximum;
            public int Next(int maxExclusive)
            {
                Calls++;
                LastExclusiveMaximum = maxExclusive;
                return Value;
            }
        }
    }
}
