using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using Vertigo.Fortune.Content;
using Vertigo.Fortune.Domain;

namespace Vertigo.Fortune.Editor
{
    /// <summary>Idempotent first-run setup. Existing designer-edited rewards and slices are retained.</summary>
    public static class FortuneContentBuilder
    {
        private const string Root = "Assets/Fortune";
        private const string ArtPath = Root + "/Art";
        private const string ContentPath = Root + "/Content";

        [MenuItem("Fortune/Validate Content")]
        public static void ValidateContent()
        {
            var game = EnsureContent();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var reward in game.Catalog.Items)
            {
                if (string.IsNullOrWhiteSpace(reward.Id) || !ids.Add(reward.Id))
                    throw new InvalidOperationException("Reward IDs must be nonempty and unique: " + reward.Id);
                if (reward.Sprite == null)
                    throw new InvalidOperationException("Missing sprite for reward: " + reward.Id);
            }
            foreach (var wheel in new[] { game.Bronze, game.Silver, game.Golden })
            {
                foreach (var slot in wheel.Slots.Where(s => !s.IsBomb))
                {
                    game.Catalog.GetById(slot.RewardId);
                    foreach (var id in slot.ProgressionPool) game.Catalog.GetById(id);
                }
            }
            var provider = game.CreateProvider();
            for (var zone = 1; zone <= 180; zone++) provider.GetSlices(zone);
            if (game.Sprites.Count != 58)
                throw new InvalidOperationException("Expected all 58 supplied art assets.");
            Debug.Log("Fortune content validated: 58 sprites, " + game.Catalog.Items.Count
                + " rewards, eight slices per wheel, and bomb rules checked through zone 180.", game);
        }

        public static GameContent EnsureContent()
        {
            Directory.CreateDirectory(ArtPath);
            Directory.CreateDirectory(ContentPath);
            CopyMissingArt();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureSpriteImports();

            var sprites = Directory.GetFiles(ArtPath)
                .Where(IsArt).OrderBy(p => p, StringComparer.Ordinal)
                .Select(path => new NamedSprite
                {
                    Name = Path.GetFileNameWithoutExtension(path),
                    Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path.Replace('\\', '/'))
                }).ToList();

            var game = AssetDatabase.LoadAssetAtPath<GameContent>(ContentPath + "/GameContent.asset");
            if (game == null)
            {
                game = ScriptableObject.CreateInstance<GameContent>();
                AssetDatabase.CreateAsset(game, ContentPath + "/GameContent.asset");
            }
            game.SetSprites(sprites);
            if (game.Catalog == null) game.Catalog = EnsureCatalog(game);
            if (game.Bronze == null) game.Bronze = EnsureWheel(game, ZoneKind.Bronze);
            if (game.Silver == null) game.Silver = EnsureWheel(game, ZoneKind.Silver);
            if (game.Golden == null) game.Golden = EnsureWheel(game, ZoneKind.Golden);
            if (game.Atlas == null) game.Atlas = EnsureAtlas(sprites);
            EditorUtility.SetDirty(game);
            AssetDatabase.SaveAssets();
            return game;
        }

        private static bool IsArt(string path)
        {
            return path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".tga", StringComparison.OrdinalIgnoreCase);
        }

        private static void CopyMissingArt()
        {
            const string source = "demo_content";
            if (!Directory.Exists(source)) return;
            foreach (var path in Directory.GetFiles(source).Where(IsArt))
            {
                var destination = ArtPath + "/" + Path.GetFileName(path);
                if (!File.Exists(destination)) File.Copy(path, destination);
            }
        }

        private static void ConfigureSpriteImports()
        {
            foreach (var path in Directory.GetFiles(ArtPath).Where(IsArt))
            {
                var importer = AssetImporter.GetAtPath(path.Replace('\\', '/')) as TextureImporter;
                if (importer == null) continue;
                var name = Path.GetFileNameWithoutExtension(path);
                var border = BorderFor(name);
                var changed = importer.textureType != TextureImporterType.Sprite
                    || importer.spriteImportMode != SpriteImportMode.Single
                    || importer.spriteBorder != border
                    || importer.mipmapEnabled || !importer.alphaIsTransparency
                    || importer.textureCompression != TextureImporterCompression.Uncompressed
                    || importer.npotScale != TextureImporterNPOTScale.None;
                if (!changed) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = border;
                importer.spritePixelsPerUnit = 100;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 2048;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }

        private static Vector4 BorderFor(string name)
        {
            if (name.StartsWith("UI_button_", StringComparison.Ordinal)) return new Vector4(14, 14, 14, 14);
            if (name == "ui_card_frame_12px_neutral") return new Vector4(32, 32, 32, 32);
            if (name == "ui_card_frame_4px_zone") return new Vector4(16, 16, 16, 16);
            if (name == "ui_card_frame_gardient") return new Vector4(16, 16, 16, 16);
            if (name == "ui_card_zone_map_frame") return new Vector4(24, 24, 24, 24);
            if (name.StartsWith("ui_card_panel_", StringComparison.Ordinal)) return new Vector4(12, 12, 12, 12);
            return Vector4.zero;
        }

        private static RewardCatalog EnsureCatalog(GameContent game)
        {
            var path = ContentPath + "/RewardCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<RewardCatalog>(path);
            if (catalog != null) return catalog;
            catalog = ScriptableObject.CreateInstance<RewardCatalog>();
            var rewards = new List<RewardAsset>();
            Add(rewards, game, "cash", "CASH", "UI_icon_cash", "Currency", "Common", 100);
            Add(rewards, game, "gold", "GOLD", "UI_icon_gold", "Currency", "Rare", 5);
            Add(rewards, game, "small_crate", "FIELD CRATE", "UI_icon_chest_small_noligt", "Crate", "Common");
            Add(rewards, game, "bronze_crate", "BRONZE CRATE", "UI_icon_chest_Bronze_nolight", "Crate", "Uncommon");
            Add(rewards, game, "supply_crate", "SUPPLY CRATE", "UI_icon_chest_standart_nolight", "Crate", "Rare");
            Add(rewards, game, "assault_crate", "ASSAULT CRATE", "UI_icon_chest_big_nolight", "Crate", "Rare");
            Add(rewards, game, "silver_crate", "SILVER CRATE", "UI_icon_chest_silver_nolight", "Crate", "Epic");
            Add(rewards, game, "gold_crate", "GOLD CRATE", "UI_icon_chest_gold_nolight", "Crate", "Legendary");
            Add(rewards, game, "super_crate", "SUPER CRATE", "UI_icon_chest_super_nolight", "Crate", "Legendary");
            Add(rewards, game, "armor_points", "ARMOR POINTS", "UI_Icons_Armor_Points", "Upgrade", "Uncommon", 5);
            Add(rewards, game, "knife_points", "KNIFE POINTS", "UI_Icons_Knife_Points", "Upgrade", "Uncommon", 5);
            Add(rewards, game, "pistol_points", "PISTOL POINTS", "UI_Icons_Pistol_Points", "Upgrade", "Common", 5);
            Add(rewards, game, "rifle_points", "RIFLE POINTS", "UI_Icons_Rifle_Points", "Upgrade", "Rare", 5);
            Add(rewards, game, "smg_points", "SMG POINTS", "UI_Icons_SMG_Points", "Upgrade", "Uncommon", 5);
            Add(rewards, game, "shotgun_points", "SHOTGUN POINTS", "UI_Icons_Shotgun_Points", "Upgrade", "Uncommon", 5);
            Add(rewards, game, "sniper_points", "SNIPER POINTS", "UI_Icons_Sniper_Points", "Upgrade", "Rare", 5);
            Add(rewards, game, "submachine_points", "ELITE SMG POINTS", "UI_Icons_Submachine_Points", "Upgrade", "Rare", 5);
            Add(rewards, game, "vest_points", "VEST POINTS", "UI_Icons_Vest_Points", "Upgrade", "Uncommon", 5);
            Add(rewards, game, "tactical_shotgun", "SANDSTORM", "UI_Icon_Renders_tier1_shotgun", "Weapon", "Rare");
            Add(rewards, game, "twilight_blade", "TWILIGHT BLADE", "UI_Icon_Renders_tier2_mle", "Weapon", "Epic");
            Add(rewards, game, "neon_rifle", "NEON CIRCUIT", "UI_Icon_Renders_tier2_rifle", "Weapon", "Epic");
            Add(rewards, game, "bloom_shotgun", "CHERRY BLOSSOM", "UI_Icon_Renders_tier3_shotgun", "Weapon", "Legendary");
            Add(rewards, game, "royal_smg", "ROYAL STANDARD", "UI_Icon_Renders_tier3_smg", "Weapon", "Legendary");
            Add(rewards, game, "electric_sniper", "ELECTRIC HAZE", "UI_Icon_Renders_tier3_sniper", "Weapon", "Legendary");
            Add(rewards, game, "easter_bayonet", "EASTER EDGE", "ui_icon_mle_bayonet_easter_time", "Weapon", "Epic");
            Add(rewards, game, "summer_bayonet", "SUMMER VICE", "ui_icon_mle_bayonet_summer_vice", "Weapon", "Legendary");
            Add(rewards, game, "aviator_glasses", "EASTER AVIATORS", "ui_icon_aviator_glasses_easter", "Equipment", "Epic");
            Add(rewards, game, "baseball_cap", "PASTEL CAP", "ui_icon_baseball_cap_easter", "Equipment", "Rare");
            Add(rewards, game, "pumpkin_helmet", "PUMPKIN HELMET", "ui_icon_helmet_pumpkin", "Equipment", "Epic");
            Add(rewards, game, "grenade_m26", "M26 GRENADE", "ui_icon_render_cons_grenade_m26", "Supply", "Common");
            Add(rewards, game, "grenade_m67", "M67 GRENADE", "ui_icon_render_cons_grenade_m67", "Supply", "Uncommon");
            Add(rewards, game, "neurostim", "NEUROSTIM", "ui_icon_render_cons_healthshot_2_neurostim", "Supply", "Rare");
            Add(rewards, game, "regenerator", "REGENERATOR", "ui_icon_render_cons_healthshot_2_regenerator", "Supply", "Rare");
            Add(rewards, game, "molotov", "MOLOTOV", "ui_icon_render_t_cons_molotov", "Supply", "Uncommon");
            catalog.SetRewards(rewards);
            AssetDatabase.CreateAsset(catalog, path);
            return catalog;
        }

        private static void Add(List<RewardAsset> rewards, GameContent game, string id,
            string name, string assetName, string category, string rarity, int amount = 1)
        {
            rewards.Add(new RewardAsset
            {
                Id = id, DisplayName = name, AssetName = assetName, Category = category,
                Rarity = rarity, BaseAmount = amount, Sprite = game.GetSprite(assetName)
            });
        }

        private static WheelDefinition EnsureWheel(GameContent game, ZoneKind kind)
        {
            var path = ContentPath + "/" + kind + "Wheel.asset";
            var wheel = AssetDatabase.LoadAssetAtPath<WheelDefinition>(path);
            if (wheel != null) return wheel;
            wheel = ScriptableObject.CreateInstance<WheelDefinition>();
            wheel.Kind = kind;
            var key = kind.ToString().ToLowerInvariant();
            wheel.WheelSprite = game.GetSprite("ui_spin_" + key + "_base");
            wheel.IndicatorSprite = game.GetSprite("ui_spin_" + key + "_indicator");
            switch (kind)
            {
                case ZoneKind.Bronze:
                    wheel.DisplayName = "BRONZE SPIN";
                    wheel.QuantityMultiplier = 1;
                    wheel.Slots = new List<WheelSlot>
                    {
                        Slot("cash", 1),
                        Slot("rifle_points", 1, "rifle_points", "pistol_points", "smg_points", "knife_points", "shotgun_points"),
                        Slot("gold", 1),
                        Slot("bronze_crate", 1, "bronze_crate", "small_crate", "supply_crate", "assault_crate"),
                        new WheelSlot { IsBomb = true },
                        Slot("grenade_m26", 1, "grenade_m26", "grenade_m67", "molotov", "neurostim", "regenerator"),
                        Slot("tactical_shotgun", 1, "tactical_shotgun", "neon_rifle", "twilight_blade"),
                        Slot("armor_points", 1, "armor_points", "vest_points", "sniper_points", "pistol_points", "submachine_points")
                    };
                    break;
                case ZoneKind.Silver:
                    wheel.DisplayName = "SILVER SPIN";
                    wheel.QuantityMultiplier = 2;
                    wheel.Slots = new List<WheelSlot>
                    {
                        Slot("cash", 2), Slot("gold", 1), Slot("silver_crate", 1),
                        Slot("neon_rifle", 1, "neon_rifle", "twilight_blade", "tactical_shotgun"),
                        Slot("sniper_points", 2, "sniper_points", "pistol_points", "submachine_points", "rifle_points"),
                        Slot("baseball_cap", 1, "baseball_cap", "aviator_glasses", "pumpkin_helmet"),
                        Slot("regenerator", 1, "regenerator", "neurostim", "molotov", "grenade_m67"),
                        Slot("assault_crate", 1, "assault_crate", "supply_crate", "bronze_crate", "small_crate")
                    };
                    break;
                default:
                    wheel.DisplayName = "GOLDEN SPIN";
                    wheel.QuantityMultiplier = 5;
                    wheel.Slots = new List<WheelSlot>
                    {
                        Slot("gold", 2), Slot("gold_crate", 1), Slot("electric_sniper", 1),
                        Slot("summer_bayonet", 1, "summer_bayonet", "easter_bayonet"),
                        Slot("super_crate", 1), Slot("bloom_shotgun", 1),
                        Slot("royal_smg", 1),
                        Slot("pumpkin_helmet", 1, "pumpkin_helmet", "aviator_glasses", "baseball_cap")
                    };
                    break;
            }
            AssetDatabase.CreateAsset(wheel, path);
            return wheel;
        }

        private static WheelSlot Slot(string id, int multiplier, params string[] pool)
        {
            return new WheelSlot
            {
                RewardId = id, AmountMultiplier = multiplier,
                ProgressionPool = new List<string>(pool)
            };
        }

        private static SpriteAtlas EnsureAtlas(IReadOnlyList<NamedSprite> sprites)
        {
            var path = ContentPath + "/FortuneArt.spriteatlas";
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
            if (atlas != null) return atlas;
            atlas = new SpriteAtlas();
            atlas.SetPackingSettings(new SpriteAtlasPackingSettings
            {
                enableRotation = false, enableTightPacking = false, padding = 4
            });
            atlas.SetTextureSettings(new SpriteAtlasTextureSettings
            {
                readable = false, generateMipMaps = false, sRGB = true,
                filterMode = FilterMode.Bilinear
            });
            var settings = atlas.GetPlatformSettings("DefaultTexturePlatform");
            settings.maxTextureSize = 4096;
            settings.textureCompression = TextureImporterCompression.Uncompressed;
            atlas.SetPlatformSettings(settings);
            atlas.SetIncludeInBuild(true);
            atlas.Add(sprites.Where(s => s.Sprite != null).Select(s => (UnityEngine.Object)s.Sprite).ToArray());
            AssetDatabase.CreateAsset(atlas, path);
            return atlas;
        }
    }
}
