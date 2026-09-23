using System;

namespace Vertigo.Fortune.Domain
{
    /// <summary>Immutable reward identity, independent of Unity assets and presentation.</summary>
    public sealed class RewardDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string AssetName { get; }
        public string Category { get; }
        public string Rarity { get; }
        public int BaseAmount { get; }

        public RewardDefinition(string id, string name, string assetName, string category, string rarity, int baseAmount)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A reward requires a stable id.", nameof(id));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A reward requires a display name.", nameof(name));
            if (baseAmount < 1) throw new ArgumentOutOfRangeException(nameof(baseAmount), "Reward amounts must be positive.");
            Id = id;
            Name = name;
            AssetName = assetName ?? string.Empty;
            Category = category ?? string.Empty;
            Rarity = rarity ?? string.Empty;
            BaseAmount = baseAmount;
        }
    }
}
