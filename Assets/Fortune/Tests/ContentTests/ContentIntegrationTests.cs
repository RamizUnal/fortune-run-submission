using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using Vertigo.Fortune.Domain;

namespace Vertigo.Fortune.Content.Tests
{
    /// <summary>Validates the actual delivered content, including original art and editable slices.</summary>
    public sealed class ContentIntegrationTests
    {
        private GameContent content;

        [SetUp]
        public void LoadDeliveredContent()
        {
            content = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Fortune/Content/GameContent.asset");
            Assert.That(content, Is.Not.Null,
                "Generate the delivered content with FortuneContentBuilder.EnsureContent before running integration tests.");
        }

        [Test]
        public void ThreeWheelsMaintainEightSlicesAndCorrectBombRulesThrough180Zones()
        {
            var provider = content.CreateProvider();
            for (var zone = 1; zone <= 180; zone++)
            {
                var slices = provider.GetSlices(zone);
                Assert.That(slices.Count, Is.EqualTo(8), "Zone " + zone);
                var kind = ZoneRules.KindFor(zone);
                Assert.That(content.GetWheel(zone).Kind, Is.EqualTo(kind), "Zone " + zone);
                Assert.That(slices.Count(s => s.IsBomb), Is.EqualTo(kind == ZoneKind.Bronze ? 1 : 0), "Zone " + zone);
                foreach (var slice in slices.Where(s => !s.IsBomb))
                {
                    Assert.That(slice.Reward, Is.Not.Null, "Zone " + zone);
                    Assert.That(slice.Amount, Is.GreaterThan(0), "Zone " + zone);
                    Assert.That(content.Catalog.GetById(slice.Reward.Id).Sprite, Is.Not.Null, "Zone " + zone);
                }
            }
        }

        [Test]
        public void All34RewardsHaveUniqueIdentitiesAndReachableWheelPools()
        {
            Assert.That(content.Catalog.Items.Count, Is.EqualTo(34));
            Assert.That(content.Catalog.Items.Select(r => r.Id).Distinct().Count(), Is.EqualTo(34));
            Assert.That(content.Catalog.Items.Select(r => r.AssetName).Distinct().Count(), Is.EqualTo(34));
            Assert.That(content.Catalog.Items.Count(r => r.Id == "pistol_points"), Is.EqualTo(1));
            Assert.That(content.Catalog.TryGet("elite_pistol_points", out _), Is.False,
                "The duplicate pistol illustration must not create a second reward.");
            var reachable = new HashSet<string>();
            foreach (var wheel in new[] { content.Bronze, content.Silver, content.Golden })
                foreach (var slot in wheel.Slots.Where(s => !s.IsBomb))
                {
                    reachable.Add(slot.RewardId);
                    foreach (var id in slot.ProgressionPool) reachable.Add(id);
                }
            Assert.That(reachable, Does.Not.Contain("elite_pistol_points"));
            foreach (var reward in content.Catalog.Items)
            {
                Assert.That(reward.Sprite, Is.Not.Null, reward.Id);
                Assert.That(content.GetSprite(reward.AssetName), Is.SameAs(reward.Sprite), reward.Id);
                Assert.That(reward.BaseAmount, Is.GreaterThan(0), reward.Id);
                Assert.That(reachable, Does.Contain(reward.Id), reward.Id);
                Assert.That(string.IsNullOrWhiteSpace(reward.Category), Is.False, reward.Id);
                Assert.That(string.IsNullOrWhiteSpace(reward.Rarity), Is.False, reward.Id);
            }
        }

        [Test]
        public void All58OriginalAssetsArePresentUnmodifiedAndIncludedInAtlas()
        {
            var art = Directory.GetFiles("Assets/Fortune/Art")
                .Where(p => p.EndsWith(".png") || p.EndsWith(".tga")).ToArray();
            Assert.That(art.Length, Is.EqualTo(58));
            Assert.That(content.Sprites.Count, Is.EqualTo(58));
            Assert.That(content.Sprites.Select(s => s.Name).Distinct().Count(), Is.EqualTo(58));
            Assert.That(content.Atlas, Is.Not.Null);
            var packed = new HashSet<UnityEngine.Object>(content.Atlas.GetPackables());
            Assert.That(packed.Count, Is.EqualTo(58));
            foreach (var named in content.Sprites)
            {
                Assert.That(named.Sprite, Is.Not.Null, named.Name);
                Assert.That(packed.Contains(named.Sprite), Is.True, named.Name);
            }
            foreach (var imported in art)
            {
                var original = Path.Combine("demo_content", Path.GetFileName(imported));
                Assert.That(File.Exists(original), Is.True, original);
                Assert.That(File.ReadAllBytes(imported), Is.EqualTo(File.ReadAllBytes(original)), original);
            }
        }

        [Test]
        public void PanelsFramesAndButtonsAreSlicedAndArtRetainsOriginalDimensions()
        {
            foreach (var item in content.Sprites)
            {
                var path = AssetDatabase.GetAssetPath(item.Sprite);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), item.Name);
                Assert.That(importer.mipmapEnabled, Is.False, item.Name);
                Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None), item.Name);
                Assert.That(importer.alphaIsTransparency, Is.True, item.Name);
                var sliced = item.Name.StartsWith("UI_button_", StringComparison.Ordinal)
                    || item.Name.StartsWith("ui_card_panel_", StringComparison.Ordinal)
                    || item.Name.StartsWith("ui_card_frame_", StringComparison.Ordinal)
                    || item.Name == "ui_card_zone_map_frame";
                Assert.That(item.Sprite.border.sqrMagnitude > 0, Is.EqualTo(sliced), item.Name);
                importer.GetSourceTextureWidthAndHeight(out var width, out var height);
                Assert.That(item.Sprite.rect.width, Is.EqualTo(width), item.Name);
                Assert.That(item.Sprite.rect.height, Is.EqualTo(height), item.Name);
            }
        }

        [Test]
        public void EditingASlotChangesRewardAndQuantityWithoutChangingCode()
        {
            var copy = UnityEngine.Object.Instantiate(content);
            var wheelCopy = UnityEngine.Object.Instantiate(content.Bronze);
            copy.Bronze = wheelCopy;
            try
            {
                var slot = wheelCopy.Slots[0];
                slot.ProgressionPool.Clear();
                slot.RewardId = "gold";
                slot.AmountMultiplier = 3;
                var reward = copy.CreateProvider().GetSlices(1)[0];
                Assert.That(reward.Reward.Id, Is.EqualTo("gold"));
                Assert.That(reward.Amount, Is.EqualTo(content.Catalog.GetById("gold").BaseAmount
                    * 3 * wheelCopy.QuantityMultiplier));
                Assert.That(content.Bronze.Slots[0].RewardId, Is.EqualTo("cash"),
                    "The test must leave delivered assets unchanged.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(wheelCopy);
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }

        [Test]
        public void LaterZonesIncreaseQuantityForStableCurrencySlices()
        {
            var provider = content.CreateProvider();
            Assert.That(provider.GetSlices(2)[0].Amount, Is.GreaterThan(provider.GetSlices(1)[0].Amount));
            Assert.That(provider.GetSlices(10)[0].Amount, Is.GreaterThan(provider.GetSlices(5)[0].Amount));
            Assert.That(provider.GetSlices(60)[0].Amount, Is.GreaterThan(provider.GetSlices(30)[0].Amount));
        }

        [Test]
        public void MilestonesDoNotSkipATiersProgressionPoolEntries()
        {
            var copy = UnityEngine.Object.Instantiate(content);
            var bronzeCopy = UnityEngine.Object.Instantiate(content.Bronze);
            var silverCopy = UnityEngine.Object.Instantiate(content.Silver);
            copy.Bronze = bronzeCopy;
            copy.Silver = silverCopy;
            try
            {
                var pool = new List<string> { "cash", "gold", "small_crate", "bronze_crate", "silver_crate", "gold_crate", "super_crate" };
                bronzeCopy.Slots[0].ProgressionPool = new List<string>(pool);
                silverCopy.Slots[0].ProgressionPool = new List<string>(pool);
                bronzeCopy.PoolRotationInterval = silverCopy.PoolRotationInterval = 1;
                var provider = copy.CreateProvider();
                Assert.That(provider.GetSlices(6)[0].Reward.Id, Is.EqualTo("silver_crate"),
                    "Zone 6 is the fifth bronze appearance; the safe zone must not consume a bronze entry.");
                Assert.That(provider.GetSlices(35)[0].Reward.Id, Is.EqualTo("gold_crate"),
                    "Zone 35 is the sixth silver appearance; golden zone 30 must not consume a silver entry.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bronzeCopy);
                UnityEngine.Object.DestroyImmediate(silverCopy);
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }

        [Test]
        public void InvalidBombConfigurationFailsBeforePlayCanProceed()
        {
            var copy = UnityEngine.Object.Instantiate(content);
            var wheelCopy = UnityEngine.Object.Instantiate(content.Silver);
            copy.Silver = wheelCopy;
            try
            {
                wheelCopy.Slots[0].IsBomb = true;
                Assert.Throws<InvalidOperationException>(() => copy.CreateProvider().GetSlices(5));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(wheelCopy);
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }
    }
}
