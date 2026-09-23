using System;

namespace Vertigo.Fortune.Domain
{
    public sealed class WheelSlice
    {
        public RewardDefinition Reward { get; }
        public int Amount { get; }
        public bool IsBomb { get; }

        public WheelSlice(RewardDefinition reward, int amount, bool isBomb = false)
        {
            if (!isBomb && reward == null) throw new ArgumentNullException(nameof(reward));
            if (amount < 0 || (!isBomb && amount == 0))
                throw new ArgumentOutOfRangeException(nameof(amount), "A reward slice requires a positive amount.");
            Reward = reward;
            Amount = amount;
            IsBomb = isBomb;
        }
    }

    /// <summary>An immutable inventory entry. Equal reward ids accumulate into one stack.</summary>
    public sealed class RewardStack
    {
        public RewardDefinition Definition { get; }
        public int Amount { get; }

        public RewardStack(RewardDefinition definition, int amount)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (amount < 1) throw new ArgumentOutOfRangeException(nameof(amount));
            Definition = definition;
            Amount = amount;
        }
    }
}
