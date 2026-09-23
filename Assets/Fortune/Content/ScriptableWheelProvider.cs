using System;
using System.Collections.Generic;
using Vertigo.Fortune.Domain;

namespace Vertigo.Fortune.Content
{
    /// <summary>Converts editable Unity content into immutable domain rewards.</summary>
    public sealed class ScriptableWheelProvider : IWheelProvider
    {
        private readonly GameContent content;

        public ScriptableWheelProvider(GameContent content)
        {
            this.content = content != null ? content : throw new ArgumentNullException(nameof(content));
        }

        public IReadOnlyList<WheelSlice> GetSlices(int zone)
        {
            if (zone < 1) throw new ArgumentOutOfRangeException(nameof(zone));
            var wheel = content.GetWheel(zone);
            if (wheel == null || wheel.Slots.Count != 8)
                throw new InvalidOperationException("Every wheel must have exactly eight slices.");

            // Count actual appearances of this tier: golden replaces silver, and
            // safe milestones do not consume an ordinary wheel's progression step.
            var appearance = wheel.Kind == ZoneKind.Golden ? zone / 30 - 1
                : wheel.Kind == ZoneKind.Silver ? zone / 5 - zone / 30 - 1
                : zone - 1 - (zone - 1) / 5;
            var poolStep = appearance / Math.Max(1, wheel.PoolRotationInterval);
            var slices = new WheelSlice[8];
            var bombs = 0;
            for (var i = 0; i < slices.Length; i++)
            {
                var slot = wheel.Slots[i];
                if (slot.IsBomb)
                {
                    bombs++;
                    slices[i] = new WheelSlice(null, 0, true);
                    continue;
                }

                var id = slot.ProgressionPool.Count > 0
                    ? slot.ProgressionPool[poolStep % slot.ProgressionPool.Count] : slot.RewardId;
                var reward = content.Catalog.GetById(id).ToDefinition();
                var amount = checked(reward.BaseAmount * zone * Math.Max(1, slot.AmountMultiplier)
                    * Math.Max(1, wheel.QuantityMultiplier));
                slices[i] = new WheelSlice(reward, amount);
            }

            var expectedBombs = ZoneRules.KindFor(zone) == ZoneKind.Bronze ? 1 : 0;
            if (bombs != expectedBombs)
                throw new InvalidOperationException("Bronze needs one bomb; silver and golden must have none.");
            return slices;
        }
    }
}
