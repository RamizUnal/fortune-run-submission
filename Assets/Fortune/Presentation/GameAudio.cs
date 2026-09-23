using UnityEngine;
using Vertigo.Fortune.Domain;

namespace Vertigo.Fortune.Presentation
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class GameAudio : MonoBehaviour
    {
        const string MutePreference = "fortune.muted";
        const float MinimumTickInterval = .055f;
        const int DetentVoiceCount = 3;

        public GameAudioClips Clips;
        public bool Muted { get; private set; }

        readonly AudioSource[] detents = new AudioSource[DetentVoiceCount];
        AudioSource mechanics;
        AudioSource interfaceSound;
        AudioSource reveal;
        AudioSource travel;
        AudioSource arrival;
        AudioSource impact;
        AudioSource debris;
        AudioSource[] sources;
        int nextDetent;
        float lastTickTime = float.NegativeInfinity;
        float revealFadeStart;
        float revealFadeTarget;
        float revealFadeElapsed;
        bool fadingReveal;

        void Awake()
        {
            Muted = PlayerPrefs.GetInt(MutePreference, 0) == 1;
            mechanics = Configure(GetComponent<AudioSource>(), 100);
            interfaceSound = CreateSource("Interface", 140);
            reveal = CreateSource("Reward", 70);
            travel = CreateSource("Reward Flight", 110);
            arrival = CreateSource("Reward Arrival", 90);
            impact = CreateSource("Bomb Impact", 60);
            debris = CreateSource("Bomb Debris", 120);
            for (var i = 0; i < detents.Length; i++)
                detents[i] = CreateSource("Revolver Detent " + (i + 1), 150);

            sources = GetComponentsInChildren<AudioSource>();
        }

        public void Toggle()
        {
            Muted = !Muted;
            foreach (var source in sources)
                source.mute = Muted;
            PlayerPrefs.SetInt(MutePreference, Muted ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void SpinStart()
        {
            if (Clips == null) return;
            StopAll();
            lastTickTime = float.NegativeInfinity;
            nextDetent = 0;
            Play(mechanics, Clips.SpinStart);
        }

        public void Tick()
        {
            if (Clips == null || Time.unscaledTime - lastTickTime < MinimumTickInterval) return;
            var interval = Time.unscaledTime - lastTickTime;
            lastTickTime = Time.unscaledTime;

            // Separate voices let the short metal tail decay without building an unbounded stack.
            // As the wheel slows, individual detents become lower and more substantial.
            var pace = 1 - Mathf.InverseLerp(.055f, .25f, interval);
            Play(detents[nextDetent], Clips.Detent, Mathf.Lerp(1, .68f, pace), Mathf.Lerp(.94f, 1.16f, pace));
            nextDetent = (nextDetent + 1) % detents.Length;
        }

        public void SpinStop()
        {
            if (Clips == null) return;
            foreach (var source in detents) source.Stop();
            Play(mechanics, Clips.SpinStop);
        }

        public void Reward() => Reward("Common");

        public void Reward(string rarity)
        {
            if (Clips == null) return;
            PlayReward(Clips.ForRarity(rarity));
        }

        public void Reward(RewardDefinition reward)
        {
            if (Clips == null) return;
            PlayReward(Clips.ForReward(reward));
        }

        void PlayReward(GameAudioClips.Cue cue)
        {
            fadingReveal = false;
            Play(reveal, cue);
        }

        public void Fly()
        {
            if (Clips == null) return;
            FadeReveal(.24f);
            Play(travel, Clips.Flight);
        }

        public void Land()
        {
            if (Clips == null) return;
            FadeReveal(.12f);
            Play(arrival, Clips.Arrival);
        }

        public void Bank()
        {
            if (Clips == null) return;
            Play(arrival, Clips.Bank);
        }

        public void BombArm()
        {
            if (Clips == null) return;
            Play(mechanics, Clips.BombArm);
        }

        public void Bomb()
        {
            if (Clips == null) return;
            StopAll();
            Play(impact, Clips.BombImpact);
        }

        public void BombAftermath()
        {
            if (Clips == null) return;
            Play(debris, Clips.BombDebris);
        }

        public void Click()
        {
            if (Clips == null) return;
            Play(interfaceSound, Clips.Click);
        }

        public void StopAll()
        {
            fadingReveal = false;
            if (sources == null) return;
            foreach (var source in sources) source.Stop();
        }

        void FadeReveal(float fraction)
        {
            if (!reveal.isPlaying) return;
            revealFadeStart = reveal.volume;
            revealFadeTarget = reveal.volume * fraction;
            revealFadeElapsed = 0;
            fadingReveal = true;
        }

        void Update()
        {
            if (!fadingReveal) return;
            revealFadeElapsed += Time.unscaledDeltaTime;
            var progress = Mathf.Clamp01(revealFadeElapsed / .22f);
            reveal.volume = Mathf.SmoothStep(revealFadeStart, revealFadeTarget, progress);
            if (progress >= 1) fadingReveal = false;
        }

        void Play(AudioSource source, GameAudioClips.Cue cue, float gain = 1, float pitch = 1)
        {
            if (cue.Clip == null || source == null) return;
            source.Stop();
            source.clip = cue.Clip;
            source.volume = Mathf.Clamp01(cue.Gain * gain * Clips.MasterVolume);
            source.pitch = Mathf.Clamp(cue.Pitch * pitch, .5f, 2);
            source.time = Mathf.Clamp(cue.StartTime, 0, Mathf.Max(0, cue.Clip.length - .01f));
            source.Play();
        }

        AudioSource CreateSource(string label, int priority)
        {
            var child = new GameObject(label);
            child.transform.SetParent(transform, false);
            return Configure(child.AddComponent<AudioSource>(), priority);
        }

        AudioSource Configure(AudioSource source, int priority)
        {
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0;
            source.priority = priority;
            source.dopplerLevel = 0;
            source.reverbZoneMix = 0;
            source.bypassEffects = true;
            source.bypassListenerEffects = true;
            source.bypassReverbZones = true;
            source.mute = Muted;
            return source;
        }

        void OnDisable() => StopAll();
    }
}
