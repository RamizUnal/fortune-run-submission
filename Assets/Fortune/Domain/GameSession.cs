using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Vertigo.Fortune.Domain
{
    /// <summary>
    /// Owns one run. Presentation requests a spin, animates the selected slice, then resolves it.
    /// The outcome is selected once before animation and can only be resolved once.
    /// </summary>
    public sealed class GameSession
    {
        private readonly IWheelProvider wheelProvider;
        private readonly IRandomSource random;
        private readonly List<RewardStack> rewards = new List<RewardStack>();
        private readonly List<RewardStack> lostRewards = new List<RewardStack>();
        private readonly ReadOnlyCollection<RewardStack> rewardView;
        private IReadOnlyList<WheelSlice> slices;
        private int selectedIndex = -1;

        public int Zone { get; private set; }
        public ZoneKind Kind => ZoneRules.KindFor(Zone);
        public RunState State { get; private set; }
        public IReadOnlyList<WheelSlice> Slices => slices;
        public IReadOnlyList<RewardStack> Rewards => rewardView;
        public bool CanCashOut => (State == RunState.Ready || State == RunState.RewardPending)
                                  && ZoneRules.IsSafe(Zone) && rewards.Count > 0;

        public GameSession(IWheelProvider wheelProvider, IRandomSource random)
        {
            this.wheelProvider = wheelProvider ?? throw new ArgumentNullException(nameof(wheelProvider));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            rewardView = rewards.AsReadOnly();
            slices = LoadSlices(1);
            Zone = 1;
            State = RunState.Ready;
        }

        public int Spin()
        {
            RequireState(RunState.Ready, "A spin can only start when the wheel is ready.");
            var index = random.Next(slices.Count);
            if (index < 0 || index >= slices.Count)
                throw new InvalidOperationException("The random source returned an index outside the wheel.");
            selectedIndex = index;
            State = RunState.Spinning;
            return selectedIndex;
        }

        public WheelSlice ResolveSpin()
        {
            RequireState(RunState.Spinning, "Only a running spin can be resolved.");
            var result = slices[selectedIndex];
            if (result.IsBomb)
            {
                lostRewards.Clear();
                lostRewards.AddRange(rewards);
                rewards.Clear();
                State = RunState.Failed;
            }
            else
            {
                AddReward(result);
                State = RunState.RewardPending;
            }
            selectedIndex = -1;
            return result;
        }

        public void Advance()
        {
            RequireState(RunState.RewardPending, "Collect the current spin's reward before advancing.");
            if (Zone == int.MaxValue) throw new InvalidOperationException("The maximum zone has been reached.");
            // Validate external content before changing any session state.
            var nextZone = Zone + 1;
            var nextSlices = LoadSlices(nextZone);
            slices = nextSlices;
            Zone = nextZone;
            State = RunState.Ready;
        }

        public IReadOnlyList<RewardStack> CashOut()
        {
            if (!CanCashOut)
                throw new InvalidOperationException("Rewards can only be cashed out while idle in a safe or super zone.");
            var winnings = new List<RewardStack>(rewards).AsReadOnly();
            State = RunState.CashedOut;
            return winnings;
        }

        public void Revive()
        {
            RequireState(RunState.Failed, "Only a failed run can be revived.");
            if (Zone == int.MaxValue) throw new InvalidOperationException("The maximum zone has been reached.");
            var nextZone = Zone + 1;
            var nextSlices = LoadSlices(nextZone);
            slices = nextSlices;
            Zone = nextZone;
            rewards.AddRange(lostRewards);
            lostRewards.Clear();
            State = RunState.Ready;
        }

        public void Restart()
        {
            if (State != RunState.Failed && State != RunState.CashedOut)
                throw new InvalidOperationException("Only a completed or failed run can be restarted.");
            var firstSlices = LoadSlices(1);
            slices = firstSlices;
            rewards.Clear();
            lostRewards.Clear();
            Zone = 1;
            selectedIndex = -1;
            State = RunState.Ready;
        }

        private IReadOnlyList<WheelSlice> LoadSlices(int zone)
        {
            var authored = wheelProvider.GetSlices(zone);
            if (authored == null || authored.Count != ZoneRules.SliceCount)
                throw new InvalidOperationException("Each wheel must contain exactly eight slices.");
            var copy = new WheelSlice[authored.Count];
            var bombs = 0;
            for (var i = 0; i < authored.Count; i++)
            {
                var slice = authored[i];
                if (slice == null) throw new InvalidOperationException("Wheel slices cannot be null.");
                if (slice.IsBomb) bombs++;
                copy[i] = slice;
            }
            var expectedBombs = ZoneRules.IsSafe(zone) ? 0 : 1;
            if (bombs != expectedBombs)
                throw new InvalidOperationException("Bronze wheels require one bomb; safe and super wheels require none.");
            return Array.AsReadOnly(copy);
        }

        private void AddReward(WheelSlice result)
        {
            for (var i = 0; i < rewards.Count; i++)
            {
                if (!string.Equals(rewards[i].Definition.Id, result.Reward.Id, StringComparison.Ordinal)) continue;
                var total = checked(rewards[i].Amount + result.Amount);
                rewards[i] = new RewardStack(rewards[i].Definition, total);
                return;
            }
            rewards.Add(new RewardStack(result.Reward, result.Amount));
        }

        private void RequireState(RunState expected, string message)
        {
            if (State != expected) throw new InvalidOperationException(message);
        }
    }
}
