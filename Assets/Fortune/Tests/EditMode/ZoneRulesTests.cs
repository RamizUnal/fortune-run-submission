using System;
using NUnit.Framework;
using Vertigo.Fortune.Domain;

namespace Vertigo.Fortune.Tests
{
    public sealed class ZoneRulesTests
    {
        [TestCase(1, ZoneKind.Bronze)]
        [TestCase(4, ZoneKind.Bronze)]
        [TestCase(5, ZoneKind.Silver)]
        [TestCase(10, ZoneKind.Silver)]
        [TestCase(29, ZoneKind.Bronze)]
        [TestCase(30, ZoneKind.Golden)]
        [TestCase(31, ZoneKind.Bronze)]
        [TestCase(35, ZoneKind.Silver)]
        [TestCase(60, ZoneKind.Golden)]
        public void ZoneKindsRespectSafeAndSuperIntervals(int zone, ZoneKind expected)
        {
            Assert.That(ZoneRules.KindFor(zone), Is.EqualTo(expected));
            Assert.That(ZoneRules.IsSafe(zone), Is.EqualTo(expected != ZoneKind.Bronze));
        }

        [TestCase(1, 5)]
        [TestCase(4, 5)]
        [TestCase(5, 10)]
        [TestCase(29, 30)]
        [TestCase(30, 35)]
        public void NextSafeZoneAlwaysLooksAhead(int current, int expected)
        {
            Assert.That(ZoneRules.NextSafeZone(current), Is.EqualTo(expected));
        }

        [Test]
        public void BaselineRewardsIncreaseAtEveryZone()
        {
            for (var zone = 1; zone < 100; zone++)
                Assert.That(ZoneRules.RewardMultiplier(zone + 1), Is.GreaterThan(ZoneRules.RewardMultiplier(zone)));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void InvalidZonesAreRejectedConsistently(int zone)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ZoneRules.KindFor(zone));
            Assert.Throws<ArgumentOutOfRangeException>(() => ZoneRules.IsSafe(zone));
            Assert.Throws<ArgumentOutOfRangeException>(() => ZoneRules.NextSafeZone(zone));
            Assert.Throws<ArgumentOutOfRangeException>(() => ZoneRules.RewardMultiplier(zone));
        }

        [Test]
        public void RewardSliceCannotBeEmptyOrHaveANonPositiveAmount()
        {
            var reward = new RewardDefinition("gold", "Gold", "gold", "Currency", "Common", 100);
            Assert.Throws<ArgumentNullException>(() => new WheelSlice(null, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WheelSlice(reward, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WheelSlice(reward, -1));
            Assert.DoesNotThrow(() => new WheelSlice(null, 0, true));
        }

        [Test]
        public void RewardIdentityAndBaseAmountAreValidated()
        {
            Assert.Throws<ArgumentException>(() => new RewardDefinition("", "Gold", "gold", "Currency", "Common", 100));
            Assert.Throws<ArgumentException>(() => new RewardDefinition("gold", " ", "gold", "Currency", "Common", 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RewardDefinition("gold", "Gold", "gold", "Currency", "Common", 0));
        }
    }
}
