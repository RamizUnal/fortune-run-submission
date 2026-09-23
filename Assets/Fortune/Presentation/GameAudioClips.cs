using System;
using UnityEngine;
using Vertigo.Fortune.Domain;

namespace Vertigo.Fortune.Presentation
{
    [CreateAssetMenu(menuName = "Fortune/Audio Clips", fileName = "GameAudioClips")]
    public sealed class GameAudioClips : ScriptableObject
    {
        [Serializable]
        public struct Cue
        {
            public AudioClip Clip;
            [Range(0, 1)] public float Gain;
            [Range(.5f, 2)] public float Pitch;
            [Min(0)] public float StartTime;

            public Cue(AudioClip clip, float gain, float pitch = 1, float startTime = 0)
            {
                Clip = clip;
                Gain = gain;
                Pitch = pitch;
                StartTime = startTime;
            }
        }

        [Range(0, 1)] public float MasterVolume = .72f;

        [Header("Revolver")]
        public Cue SpinStart;
        public Cue Detent;
        public Cue SpinStop;

        [Header("Rewards")]
        public Cue Currency;
        public Cue Common;
        public Cue Uncommon;
        public Cue Rare;
        public Cue Epic;
        public Cue Legendary;
        public Cue Flight;
        public Cue Arrival;
        public Cue Bank;

        [Header("Bomb")]
        public Cue BombArm;
        public Cue BombImpact;
        public Cue BombDebris;

        [Header("Interface")]
        public Cue Click;

        public Cue ForRarity(string rarity)
        {
            var cue = Common;
            if (string.Equals(rarity, "Legendary", StringComparison.OrdinalIgnoreCase)) cue = Legendary;
            else if (string.Equals(rarity, "Epic", StringComparison.OrdinalIgnoreCase)) cue = Epic;
            else if (string.Equals(rarity, "Rare", StringComparison.OrdinalIgnoreCase)) cue = Rare;
            else if (string.Equals(rarity, "Uncommon", StringComparison.OrdinalIgnoreCase)) cue = Uncommon;
            if (cue.Clip != null) return cue;
            if (Common.Clip != null) return Common;
            return Arrival.Clip != null ? Arrival : Click;
        }

        public Cue ForReward(RewardDefinition reward)
        {
            if (reward != null && string.Equals(reward.Category, "Currency", StringComparison.OrdinalIgnoreCase)
                && Currency.Clip != null) return Currency;
            return ForRarity(reward?.Rarity);
        }
    }
}
