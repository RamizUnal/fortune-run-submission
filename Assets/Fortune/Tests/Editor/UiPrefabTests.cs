#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Vertigo.Fortune.Content;
using Vertigo.Fortune.Domain;
using Vertigo.Fortune.Presentation;

namespace Vertigo.Fortune.Tests
{
    public sealed class UiPrefabTests
    {
        const string MainPrefab = "Assets/Fortune/Prefabs/FortuneScreen.prefab";
        const string UiFolder = "Assets/Fortune/Prefabs/UI/";
        const string ContentPath = "Assets/Fortune/Content/GameContent.asset";

        [Test]
        public void MainScreenKeepsItsReusablePrefabsConnected()
        {
            var root = PrefabUtility.LoadPrefabContents(MainPrefab);
            try
            {
                AssertNestedCount(root, "HelpPopup", 1);
                AssertNestedCount(root, "FailurePopup", 1);
                AssertNestedCount(root, "BankedPopup", 1);
                AssertNestedCount(root, "CollectionPopup", 1);
                AssertNestedCount(root, "RewardReveal", 1);
                AssertNestedCount(root, "BombReveal", 1);
                AssertNestedCount(root, "RunLoot", 1);
                AssertNestedCount(root, "CollectionCard", 12);
                AssertNestedCount(root, "LootRow", 4);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void MainScreenHasAllViewReferencesAndCodeBoundButtons()
        {
            var root = PrefabUtility.LoadPrefabContents(MainPrefab);
            try
            {
                var screen = root.GetComponentInChildren<FortuneScreen>(true);
                Assert.That(screen, Is.Not.Null);
                Assert.That(root.GetComponentsInChildren<PopupView>(true).Length, Is.EqualTo(4));
                Assert.That(root.GetComponentsInChildren<CollectionView>(true).Length, Is.EqualTo(1));
                Assert.That(root.GetComponentsInChildren<RunLootView>(true).Length, Is.EqualTo(1));

                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject),
                        Is.Zero, child.name + " has a missing script.");

                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component is FortuneScreen || component is WheelView || component is GameDialogs
                        || component is PopupView || component is CollectionView || component is RunLootView
                        || component is RewardRevealView || component is BombRevealView || component is ActionButton)
                        AssertReferencesAssigned(component);
                }

                foreach (var button in root.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                    Assert.That(button.onClick.GetPersistentEventCount(), Is.Zero,
                        button.name + " must bind its actions in code.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void CollectionPreservesAuthoredGridPlacementAcrossPagesAndReopening()
        {
            WithCollection((view, root, content) =>
            {
                var grid = Find<RectTransform>(root, "ui_collection_grid_motion");
                var authoredPosition = grid.anchoredPosition + new Vector2(17, -23);
                grid.anchoredPosition = authoredPosition;
                var wallet = new EmptyWallet();

                view.Initialize(content, wallet, true, () => { });
                Assert.That(grid.anchoredPosition, Is.EqualTo(authoredPosition));

                view.RenderPage(1, false);
                Assert.That(view.CurrentPage, Is.EqualTo(1));
                Assert.That(grid.anchoredPosition, Is.EqualTo(authoredPosition));

                root.SetActive(false);
                root.SetActive(true);
                view.Initialize(content, wallet, true, () => { });
                Assert.That(view.CurrentPage, Is.Zero);
                Assert.That(view.IsTransitioning, Is.False);
                Assert.That(grid.anchoredPosition, Is.EqualTo(authoredPosition));
            });
        }

        [Test]
        public void ReinitializingCollectionDoesNotDuplicateNavigationHandlers()
        {
            WithCollection((view, root, content) =>
            {
                var wallet = new EmptyWallet();
                for (int opening = 0; opening < 4; opening++)
                    view.Initialize(content, wallet, true, () => { });

                root.SetActive(true);
                var next = Find<ActionButton>(root, "ui_button_collection_next");
                // EditMode does not run this component's lifecycle. Bind the same Button listener
                // Awake installs in a player; unloading the prefab lets OnDestroy remove it.
                typeof(ActionButton).GetMethod("Awake", System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic).Invoke(next, null);
                next.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();

                Assert.That(view.CurrentPage, Is.EqualTo(1),
                    "One click must advance one page after the collection has been reopened.");
            });
        }

        [Test]
        public void CollectionFitsArtworkToTheAuthoredCardBounds()
        {
            WithCollection((view, root, content) =>
            {
                var card = Find<RectTransform>(root, "ui_collection_card_0_value");
                var bounds = card.Find("ui_reward_art_bounds") as RectTransform;
                Assert.That(bounds, Is.Not.Null);
                bounds.anchoredPosition += new Vector2(12, -9);
                bounds.sizeDelta = new Vector2(126, 48);
                var authoredPosition = bounds.anchoredPosition;
                var authoredSize = bounds.sizeDelta;

                view.Initialize(content, new EmptyWallet(), true, () => { });
                var image = bounds.GetComponentInChildren<UnityEngine.UI.Image>(true);
                var reward = content.Catalog.Items[0];
                var visibleCenter = RewardArtLayout.VisibleCenter(reward.AssetName, image.rectTransform);
                var targetCenter = bounds.TransformPoint(bounds.rect.center);
                var visibleSize = RewardArtLayout.VisibleSize(reward.AssetName, image.rectTransform.rect.size);

                Assert.That(Vector3.Distance(visibleCenter, targetCenter), Is.LessThan(.01f));
                Assert.That(visibleSize.x, Is.LessThanOrEqualTo(bounds.rect.width + .01f));
                Assert.That(visibleSize.y, Is.LessThanOrEqualTo(bounds.rect.height + .01f));
                Assert.That(bounds.anchoredPosition, Is.EqualTo(authoredPosition));
                Assert.That(bounds.sizeDelta, Is.EqualTo(authoredSize));
                Assert.That(image.preserveAspect, Is.True);
            });
        }

        [Test]
        public void LootArrivalTracksEditedArtworkBoundsInsteadOfTheOutgoingRowPose()
        {
            var root = PrefabUtility.LoadPrefabContents(MainPrefab);
            try
            {
                var screen = root.GetComponentInChildren<FortuneScreen>(true);
                var content = AssetDatabase.LoadAssetAtPath<GameContent>(ContentPath);
                var loot = screen.RunLoot;
                // Runtime rows are free to move. Release the temporary Editor instance's
                // prefab lock so this check can exercise the same reparenting behavior.
                PrefabUtility.UnpackPrefabInstance(loot.gameObject, PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
                using (var serialized = new SerializedObject(loot))
                {
                    var row = serialized.FindProperty("rows").GetArrayElementAtIndex(3);
                    var bounds = (RectTransform)row.FindPropertyRelative("ArtBounds").objectReferenceValue;
                    var motion = (RectTransform)row.FindPropertyRelative("Motion").objectReferenceValue;
                    var artwork = (UnityEngine.UI.Image)row.FindPropertyRelative("Art").objectReferenceValue;
                    bounds.anchoredPosition += new Vector2(23, -11);
                    bounds.sizeDelta = new Vector2(96, 38);
                    bounds.localRotation = Quaternion.Euler(0, 0, 7);
                    loot.Initialize(screen, content);
                    var rewards = new List<RewardStack>();
                    for (int index = 0; index < 4; index++)
                        rewards.Add(new RewardStack(content.Catalog.Items[index].ToDefinition(), index + 1));
                    loot.Refresh(rewards);

                    motion.anchoredPosition += new Vector2(-19, 34);
                    motion.localScale *= .73f;
                    var incoming = new WheelSlice(content.Catalog.Items[4].ToDefinition(), 7);
                    var target = loot.PrepareArrival(incoming, true);
                    Assert.That(loot.VisibleRewardIds, Does.Not.Contain(incoming.Reward.Id));
                    rewards.Add(new RewardStack(incoming.Reward, incoming.Amount));
                    loot.CompleteArrival(rewards, incoming.Reward.Id, true);

                    var actualCenter = RewardArtLayout.VisibleCenter(incoming.Reward.AssetName, artwork.rectTransform);
                    var actualWidth = artwork.rectTransform.TransformVector(Vector3.right * artwork.rectTransform.rect.width);
                    Assert.That(Vector3.Distance(actualCenter, target.WorldCenter), Is.LessThan(.01f));
                    Assert.That(Vector3.Distance(actualWidth, target.WorldWidth), Is.LessThan(.01f));
                    Assert.That(loot.VisibleRewardIds[0], Is.EqualTo(incoming.Reward.Id));
                    Assert.That(rewards.Count, Is.EqualTo(5));
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void WithCollection(Action<CollectionView, GameObject, GameContent> test)
        {
            var root = PrefabUtility.LoadPrefabContents(UiFolder + "CollectionPopup.prefab");
            try
            {
                var view = root.GetComponentInChildren<CollectionView>(true);
                var content = AssetDatabase.LoadAssetAtPath<GameContent>(ContentPath);
                Assert.That(view, Is.Not.Null);
                Assert.That(content, Is.Not.Null);
                test(view, root, content);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void AssertNestedCount(GameObject root, string name, int expected)
        {
            string path = UiFolder + name + ".prefab";
            int count = 0;
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject)) continue;
                if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject) != path) continue;
                Assert.That(PrefabUtility.GetPrefabInstanceStatus(child.gameObject),
                    Is.EqualTo(PrefabInstanceStatus.Connected), path);
                count++;
            }
            Assert.That(count, Is.EqualTo(expected), path);
        }

        static void AssertReferencesAssigned(MonoBehaviour view)
        {
            using (var serialized = new SerializedObject(view))
            {
                var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyPath.StartsWith("m_", StringComparison.Ordinal)) continue;
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    // The revolver hub is an icon-only button.
                    if (view is ActionButton && property.propertyPath == "label") continue;
                    Assert.That(property.objectReferenceValue, Is.Not.Null,
                        view.name + " / " + view.GetType().Name + "." + property.propertyPath);
                }
            }
        }

        static T Find<T>(GameObject root, string name) where T : Component
        {
            foreach (var component in root.GetComponentsInChildren<T>(true))
                if (component.name == name) return component;
            throw new InvalidOperationException(root.name + " is missing " + name + ".");
        }

        sealed class EmptyWallet : IRewardWallet
        {
            public int Extractions => 0;
            public int BestZone => 0;
            public int GetAmount(string id) => 0;
            public void Deposit(IReadOnlyList<RewardStack> rewards, int zone)
                => throw new InvalidOperationException("UI tests must not write rewards.");
        }
    }
}
#endif
