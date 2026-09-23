using System;
using UnityEditor;
using UnityEngine;
using Vertigo.Fortune.Presentation;

namespace Vertigo.Fortune.Editor
{
    public static class FortuneAudioSetup
    {
        public const string BankPath = "Assets/Fortune/Audio/GameAudioClips.asset";
        const string ClipFolder = "Assets/Fortune/Audio/Sfx/";

        [MenuItem("Fortune/Import Game Audio")]
        public static void ImportAudio() => EnsureClips();

        public static GameAudioClips EnsureClips()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameAudioClips>(BankPath);
            if (existing != null) return existing;

            var bank = ScriptableObject.CreateInstance<GameAudioClips>();
            bank.SpinStart = Cue("revolver_siderelease", .68f, false);
            bank.Detent = Cue("revolver_hammer", .25f, false, 1.05f);
            bank.SpinStop = Cue("revolver_sideback", .62f, false, 1, .14f);
            ConfigureRewardCues(bank);
            bank.Flight = Cue("csgo_ui_page_scroll", .70f, false, 1.22f);
            bank.Arrival = Cue("item_sticker_select", .53f, false);
            bank.Bank = Cue("coin_pickup_01", .48f);
            bank.BombArm = Cue("pinpull", .62f, false, .94f);
            bank.BombImpact = Cue("explode5", .58f);
            bank.BombDebris = Cue("c4_exp_deb1", .16f);
            bank.Click = Cue("menu_accept", .68f, false);

            AssetDatabase.CreateAsset(bank, BankPath);
            AssetDatabase.SaveAssets();
            return bank;
        }

        public static void ConfigureRewardCues(GameAudioClips bank)
        {
            if (bank == null) throw new ArgumentNullException(nameof(bank));
            bank.Currency = Cue("coin_pickup_01", .60f, true, 1.08f);
            bank.Common = Cue("item_drop1_common", .85f);
            bank.Uncommon = Cue("item_drop2_uncommon", .52f);
            bank.Rare = Cue("item_drop3_rare", .42f);
            bank.Epic = Cue("item_drop4_mythical", .36f);
            bank.Legendary = Cue("item_drop5_legendary", .44f);
        }

        static GameAudioClips.Cue Cue(string filename, float gain, bool stereo = true,
            float pitch = 1, float startTime = 0)
        {
            var path = ClipFolder + filename + ".wav";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) throw new InvalidOperationException("Audio file is missing: " + path);

            importer.forceToMono = !stereo;
            importer.loadInBackground = false;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) throw new InvalidOperationException("Audio import failed: " + path);
            return new GameAudioClips.Cue(clip, gain, pitch, startTime);
        }
    }
}
