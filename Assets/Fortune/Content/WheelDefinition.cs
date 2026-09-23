using System;
using System.Collections.Generic;
using UnityEngine;
using Vertigo.Fortune.Domain;

namespace Vertigo.Fortune.Content
{
    [Serializable]
    public sealed class WheelSlot
    {
        [Tooltip("Stable reward ID in the Reward Catalog. Ignored for a bomb.")]
        public string RewardId;
        [Tooltip("Each bronze wheel must have exactly one bomb. Safe wheels have none.")]
        public bool IsBomb;
        [Min(1)] public int AmountMultiplier = 1;
        [Tooltip("Optional reward IDs to cycle through as zones increase. Leave empty to always use RewardId.")]
        public List<string> ProgressionPool = new List<string>();
    }

    [CreateAssetMenu(fileName = "WheelDefinition", menuName = "Fortune/Wheel Definition")]
    public sealed class WheelDefinition : ScriptableObject
    {
        public string DisplayName;
        public ZoneKind Kind;
        public Sprite WheelSprite;
        public Sprite IndicatorSprite;
        [Tooltip("All wheel definitions contain exactly eight editor-configurable slices.")]
        public List<WheelSlot> Slots = new List<WheelSlot>(8);
        [Min(1)] public int QuantityMultiplier = 1;
        [Tooltip("Number of appearances of this wheel before rotating its optional reward pools.")]
        [Min(1)] public int PoolRotationInterval = 1;
    }
}
