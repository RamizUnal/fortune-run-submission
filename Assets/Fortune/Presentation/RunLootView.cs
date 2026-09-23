using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vertigo.Fortune.Content;
using Vertigo.Fortune.Domain;

namespace Vertigo.Fortune.Presentation
{
    public readonly struct RewardLandingTarget
    {
        public Vector3 WorldCenter { get; }
        public Vector3 WorldWidth { get; }

        public RewardLandingTarget(Vector3 worldCenter, Vector3 worldWidth)
        {
            WorldCenter = worldCenter;
            WorldWidth = worldWidth;
        }
    }

    public sealed class RunLootView : MonoBehaviour
    {
        const int VisibleRows = 4;

        [SerializeField] LootRow[] rows = new LootRow[VisibleRows];
        [SerializeField] RectTransform[] slots = new RectTransform[VisibleRows];
        [SerializeField] TMP_Text remainingCount;

        readonly List<string> rewardOrder = new List<string>();
        readonly List<string> visibleRewardIds = new List<string>();
        readonly List<Tween> animations = new List<Tween>();
        FortuneScreen screen;
        GameContent content;
        Sequence lossSequence;
        string incomingId;
        bool initialized;

        public IReadOnlyList<string> VisibleRewardIds => visibleRewardIds;
        public bool IsStaging { get; private set; }
        public bool IsLosing { get; private set; }

        public void Initialize(FortuneScreen view, GameContent gameContent)
        {
            if (initialized)
                throw new InvalidOperationException("Run loot has already been initialized.");
            screen = view != null ? view : throw new ArgumentNullException(nameof(view));
            content = gameContent != null ? gameContent : throw new ArgumentNullException(nameof(gameContent));
            ResolveReferences();
            foreach (var row in rows) row.CaptureLayout();
            initialized = true;
        }

        public void Refresh(IReadOnlyList<RewardStack> rewards)
        {
            if (!initialized) throw new InvalidOperationException("Initialize run loot before refreshing it.");
            if (rewards == null) throw new ArgumentNullException(nameof(rewards));

            KillAnimations();
            IsStaging = false;
            incomingId = null;

            for (int index = rewardOrder.Count - 1; index >= 0; index--)
                if (FindReward(rewards, rewardOrder[index]) == null) rewardOrder.RemoveAt(index);

            foreach (var reward in rewards)
                if (!rewardOrder.Contains(reward.Definition.Id)) rewardOrder.Insert(0, reward.Definition.Id);

            for (int index = 0; index < rows.Length; index++)
            {
                var reward = index < rewardOrder.Count ? FindReward(rewards, rewardOrder[index]) : null;
                BindRow(rows[index], reward, index);
            }

            bool empty = rewards.Count == 0;
            screen.LootIcon.gameObject.SetActive(empty);
            screen.LootEmpty.gameObject.SetActive(empty);
            remainingCount.gameObject.SetActive(rewards.Count > VisibleRows);
            remainingCount.color = UiPalette.Muted;
            remainingCount.text = "+ " + Math.Max(0, rewards.Count - VisibleRows) + " MORE REWARD TYPES";
            UpdateVisibleIds();
        }

        public void PlayLoss(bool reducedMotion, Action completed)
        {
            if (!initialized) throw new InvalidOperationException("Initialize run loot before presenting a loss.");
            if (IsLosing) throw new InvalidOperationException("Run loot is already being cleared.");

            KillAnimations();
            IsStaging = false;
            incomingId = null;
            IsLosing = true;
            lossSequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);

            int visibleIndex = 0;
            foreach (var row in rows)
            {
                if (row.RewardId == null || !row.Root.gameObject.activeSelf) continue;
                float delay = reducedMotion ? 0 : visibleIndex * .06f;
                if (reducedMotion)
                {
                    lossSequence.Insert(delay, row.Group.DOFade(0, .30f).SetEase(Ease.InOutQuad));
                }
                else
                {
                    lossSequence.Insert(delay, row.Art.DOColor(new Color(1, .83f, .70f), .12f));
                    lossSequence.Insert(delay, row.Name.DOColor(UiPalette.Hex("D7B9A8"), .12f));
                    lossSequence.Insert(delay, row.Amount.DOColor(UiPalette.Hex("BD967D"), .12f));
                    lossSequence.Insert(delay + .08f,
                        row.Motion.DOAnchorPos(row.Motion.anchoredPosition + Vector2.down * 14, .35f)
                            .SetEase(Ease.InQuad));
                    lossSequence.Insert(delay + .08f,
                        row.Motion.DOScale(row.MotionScale * .94f, .35f).SetEase(Ease.InQuad));
                    float tilt = visibleIndex % 2 == 0 ? -1.8f : 1.4f;
                    lossSequence.Insert(delay + .08f,
                        row.Motion.DOLocalRotate(row.MotionRotation.eulerAngles + new Vector3(0, 0, tilt), .35f)
                            .SetEase(Ease.InQuad));
                    lossSequence.Insert(delay + .08f,
                        row.Group.DOFade(0, .35f).SetEase(Ease.InQuad));
                }
                visibleIndex++;
            }

            if (remainingCount.gameObject.activeSelf)
                lossSequence.Insert(0, remainingCount.DOFade(0, .30f));
            if (visibleIndex == 0)
                lossSequence.InsertCallback(.30f, () => { });

            lossSequence.OnComplete(() =>
            {
                lossSequence = null;
                IsLosing = false;
                foreach (var row in rows)
                {
                    row.RewardId = null;
                    row.Root.gameObject.SetActive(false);
                }
                visibleRewardIds.Clear();
                rewardOrder.Clear();
                remainingCount.gameObject.SetActive(false);
                completed?.Invoke();
            });
        }

        public RewardLandingTarget PrepareArrival(WheelSlice reward, bool reducedMotion)
        {
            if (!initialized) throw new InvalidOperationException("Initialize run loot before preparing an arrival.");
            if (reward == null || reward.IsBomb) throw new ArgumentException("An arriving item must be a reward.", nameof(reward));
            if (IsStaging) throw new InvalidOperationException("Another reward is already arriving.");
            if (IsLosing) throw new InvalidOperationException("Run loot is still being cleared.");

            KillAnimations();
            incomingId = reward.Reward.Id;
            IsStaging = true;

            for (int index = 0; index < rows.Length; index++)
            {
                if (rows[index].RewardId == incomingId) return LandingTarget(index, reward);
            }

            screen.LootIcon.gameObject.SetActive(false);
            screen.LootEmpty.gameObject.SetActive(false);

            // Reuse the last row after it fades. The reserved slot stays empty until arrival.
            var reserved = rows[VisibleRows - 1];
            for (int index = rows.Length - 1; index > 0; index--)
            {
                rows[index] = rows[index - 1];
                MoveToSlot(rows[index], index, reducedMotion);
            }
            rows[0] = reserved;
            reserved.RewardId = null;

            if (reserved.Root.gameObject.activeSelf && !reducedMotion)
            {
                Own(reserved.Group.DOFade(0, .12f).SetEase(Ease.OutQuad)
                    .OnComplete(() => reserved.Root.gameObject.SetActive(false)));
            }
            else
            {
                reserved.Root.gameObject.SetActive(false);
            }

            UpdateVisibleIds();
            return LandingTarget(0, reward);
        }

        public void CompleteArrival(IReadOnlyList<RewardStack> rewards, string rewardId, bool reducedMotion)
        {
            if (!initialized) throw new InvalidOperationException("Initialize run loot before completing an arrival.");
            if (rewards == null) throw new ArgumentNullException(nameof(rewards));
            if (FindReward(rewards, rewardId) == null)
                throw new InvalidOperationException("The arriving reward must be committed before it is shown.");
            if (IsStaging && incomingId != rewardId)
                throw new InvalidOperationException("The committed reward does not match the incoming item.");

            bool alreadyVisible = visibleRewardIds.Contains(rewardId);
            if (!alreadyVisible)
            {
                rewardOrder.Remove(rewardId);
                rewardOrder.Insert(0, rewardId);
            }

            Refresh(rewards);
            if (reducedMotion) return;

            foreach (var row in rows)
            {
                if (row.RewardId != rewardId) continue;
                row.Motion.localScale = row.MotionScale * 1.035f;
                Own(row.Motion.DOScale(row.MotionScale, .24f).SetEase(Ease.OutCubic));
                break;
            }
        }

        void BindRow(LootRow row, RewardStack reward, int index)
        {
            row.PlaceIn(slots[index]);
            row.ResetMotion();
            row.Art.color = row.ArtColor;
            row.Name.color = row.NameColor;
            row.Amount.color = row.AmountColor;
            row.RewardId = reward?.Definition.Id;
            row.Root.gameObject.SetActive(reward != null);
            if (reward == null) return;

            row.Name.text = reward.Definition.Name;
            row.Amount.text = "x" + WheelView.Compact(reward.Amount);
            row.Art.sprite = content.GetSprite(reward.Definition.AssetName);
            row.Art.enabled = row.Art.sprite != null;
            if (row.Art.sprite != null)
                RewardArtLayout.FitInto(reward.Definition.AssetName, row.Art, row.ArtBounds);
        }

        void MoveToSlot(LootRow row, int index, bool reducedMotion)
        {
            Vector3 position = row.Motion.position;
            row.PlaceIn(slots[index]);
            row.Motion.localScale = row.MotionScale;
            row.Motion.localRotation = row.MotionRotation;
            if (reducedMotion || !row.Root.gameObject.activeSelf)
                row.Motion.anchoredPosition = row.MotionPosition;
            else
            {
                row.Motion.position = position;
                Own(row.Motion.DOAnchorPos(row.MotionPosition, .24f).SetEase(Ease.OutCubic));
            }
        }

        RewardLandingTarget LandingTarget(int index, WheelSlice reward)
            => rows[index].GetLandingTarget(slots[index], reward.Reward.AssetName,
                content.GetSprite(reward.Reward.AssetName));

        void UpdateVisibleIds()
        {
            visibleRewardIds.Clear();
            foreach (var row in rows)
                if (row.RewardId != null) visibleRewardIds.Add(row.RewardId);
        }

        static RewardStack FindReward(IReadOnlyList<RewardStack> rewards, string id)
        {
            foreach (var reward in rewards)
                if (reward.Definition.Id == id) return reward;
            return null;
        }

        void Own(Tween animation) => animations.Add(animation.SetTarget(this));

        void KillAnimations()
        {
            lossSequence?.Kill();
            lossSequence = null;
            IsLosing = false;
            foreach (var animation in animations) animation?.Kill();
            animations.Clear();
        }

        void Awake() => ResolveReferences();
        void OnValidate() => ResolveReferences();

        void ResolveReferences()
        {
            if (rows == null || rows.Length != VisibleRows) Array.Resize(ref rows, VisibleRows);
            if (slots == null || slots.Length != VisibleRows) Array.Resize(ref slots, VisibleRows);
            for (int index = 0; index < rows.Length; index++)
            {
                if (slots[index] == null) slots[index] = transform.Find("ui_loot_slot_" + index) as RectTransform;
                if (rows[index] == null) rows[index] = new LootRow();
                rows[index].Resolve(slots[index], index);
            }
            if (remainingCount == null) remainingCount = Find<TMP_Text>(transform, "ui_text_loot_more_value");
        }

        void OnEnable() => lossSequence?.Play();

        void OnDisable()
        {
            if (IsLosing)
            {
                lossSequence?.Pause();
                return;
            }
            KillAnimations();
            IsStaging = false;
            incomingId = null;
            if (!initialized) return;
            foreach (var row in rows)
            {
                row.ResetMotion();
                row.Root.gameObject.SetActive(row.RewardId != null);
            }
        }

        void OnDestroy() => KillAnimations();

        static T Find<T>(Transform parent, string name) where T : Component
        {
            var child = parent != null ? parent.Find(name) : null;
            return child != null ? child.GetComponent<T>() : null;
        }

        [Serializable]
        sealed class LootRow
        {
            public RectTransform Root;
            public RectTransform Motion;
            public CanvasGroup Group;
            public Image Art;
            public RectTransform ArtBounds;
            public TMP_Text Name;
            public TMP_Text Amount;

            [NonSerialized] public string RewardId;
            [NonSerialized] public Vector2 MotionPosition;
            [NonSerialized] public Vector3 MotionScale;
            [NonSerialized] public Quaternion MotionRotation;
            [NonSerialized] public Color ArtColor;
            [NonSerialized] public Color NameColor;
            [NonSerialized] public Color AmountColor;
            Vector2 rootPosition;
            float opacity;
            Matrix4x4 artBoundsToSlot;
            Rect restingArtBounds;

            public void Resolve(Transform slot, int index)
            {
                if (Root == null && slot != null) Root = slot.Find("ui_loot_row_" + index + "_value") as RectTransform;
                if (Motion == null && Root != null) Motion = Root.Find("ui_loot_reward_" + index + "_motion") as RectTransform;
                if (Group == null && Motion != null) Group = Motion.GetComponent<CanvasGroup>();
                if (ArtBounds == null && Motion != null) ArtBounds = Motion.Find("ui_reward_art_bounds") as RectTransform;
                if (Art == null) Art = Find<Image>(ArtBounds, "ui_image_reward_value");
                if (Name == null) Name = Find<TMP_Text>(Motion, "ui_text_reward_name_value");
                if (Amount == null) Amount = Find<TMP_Text>(Motion, "ui_text_reward_amount_value");
            }

            public void CaptureLayout()
            {
                rootPosition = Root.anchoredPosition;
                MotionPosition = Motion.anchoredPosition;
                MotionScale = Motion.localScale;
                MotionRotation = Motion.localRotation;
                opacity = Group.alpha;
                ArtColor = Art.color;
                NameColor = Name.color;
                AmountColor = Amount.color;
                artBoundsToSlot = Root.parent.worldToLocalMatrix * ArtBounds.localToWorldMatrix;
                restingArtBounds = ArtBounds.rect;
            }

            public RewardLandingTarget GetLandingTarget(RectTransform slot, string assetName, Sprite sprite)
            {
                // Project the authored resting layout; this row may still be fading in its old slot.
                var toWorld = slot.localToWorldMatrix * artBoundsToSlot;
                var fit = RewardArtLayout.Fit(assetName, sprite.rect.size, new Rect(Vector2.zero, restingArtBounds.size));
                return new RewardLandingTarget(
                    toWorld.MultiplyPoint3x4(restingArtBounds.center),
                    toWorld.MultiplyVector(Vector3.right * fit.width));
            }

            public void PlaceIn(RectTransform slot)
            {
                Root.SetParent(slot, false);
                Root.anchoredPosition = rootPosition;
            }

            public void ResetMotion()
            {
                Motion.anchoredPosition = MotionPosition;
                Motion.localScale = MotionScale;
                Motion.localRotation = MotionRotation;
                Group.alpha = opacity;
            }
        }
    }
}
