using System;
using System.Collections.Generic;
using UnityEngine;
using Vertigo.Fortune.Domain;

namespace Vertigo.Fortune.Content
{
    /// <summary>Presentation metadata and domain identity for one editable reward.</summary>
    [Serializable]
    public sealed class RewardAsset
    {
        public string Id;
        public string DisplayName;
        public string AssetName;
        public string Category;
        public string Rarity;
        [Min(1)] public int BaseAmount = 1;
        public Sprite Sprite;

        public RewardDefinition ToDefinition()
        {
            return new RewardDefinition(Id, DisplayName, AssetName, Category, Rarity,
                Math.Max(1, BaseAmount));
        }
    }

    [CreateAssetMenu(fileName = "RewardCatalog", menuName = "Fortune/Reward Catalog")]
    public sealed class RewardCatalog : ScriptableObject
    {
        [SerializeField] private List<RewardAsset> rewards = new List<RewardAsset>();
        public IReadOnlyList<RewardAsset> Items => rewards;

        public RewardAsset GetById(string id)
        {
            for (var i = 0; i < rewards.Count; i++)
                if (string.Equals(rewards[i].Id, id, StringComparison.Ordinal))
                    return rewards[i];
            throw new KeyNotFoundException("Reward catalog does not contain: " + id);
        }

        public bool TryGet(string id, out RewardAsset reward)
        {
            for (var i = 0; i < rewards.Count; i++)
                if (string.Equals(rewards[i].Id, id, StringComparison.Ordinal))
                {
                    reward = rewards[i];
                    return true;
                }
            reward = null;
            return false;
        }

        public void SetRewards(IEnumerable<RewardAsset> values)
        {
            rewards = new List<RewardAsset>(values);
        }
    }
}
