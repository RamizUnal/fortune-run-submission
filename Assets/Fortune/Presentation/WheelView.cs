using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using Vertigo.Fortune.Content;
using Vertigo.Fortune.Domain;

namespace Vertigo.Fortune.Presentation
{
    public sealed class WheelView : MonoBehaviour
    {
        const float SliceAngle = 45;
        const float WindupDuration = .22f;
        const float SettleDuration = .24f;
        const float WindupAngle = 8;
        const float LandingOvershoot = 1.6f;

        public RectTransform Rotor, Pointer, HubMotion, ImpactMotion;
        public UnityEngine.UI.Image Base, Indicator, Halo, Flash;
        public WheelHubGraphic HubFinish;
        public UnityEngine.UI.Image[] Icons;
        public TMP_Text[] Amounts;
        public RectTransform[] Slots;

        [Header("Chamber layout")]
        [SerializeField] Vector2 artworkSize = new Vector2(64, 44);
        [SerializeField, Min(0)] float artworkLabelGap = 5;

        float angle;
        Tween spin;

        // A paused wheel still owns its outcome; disabling the view must not unlock another spin.
        public bool IsAnimating => spin != null && spin.IsActive();

        public void SetContent(GameContent content, int zone, IReadOnlyList<WheelSlice> slices)
        {
            spin?.Kill();
            spin = null;
            ResetMechanics();
            ClearHighlight();

            var wheel = content.GetWheel(zone);
            Base.sprite = wheel.WheelSprite;
            Indicator.sprite = wheel.IndicatorSprite;
            for (int i = 0; i < slices.Count; i++)
            {
                var slice = slices[i];
                var center = WheelArtLayout.Center(wheel.WheelSprite, i, Rotor.rect.size);
                Slots[i].anchoredPosition = new Vector2(center.x, -center.y);
                string assetName = slice.IsBomb ? "ui_card_icon_death" : slice.Reward.AssetName;
                Icons[i].sprite = content.GetSprite(assetName);
                Amounts[i].text = slice.IsBomb ? "BOMB" : "x" + Compact(slice.Amount);
                Amounts[i].color = slice.IsBomb ? UiPalette.Red : UiPalette.White;
                LayoutItem(i, assetName);
            }

            if (HubFinish != null)
            {
                var kind = ZoneRules.KindFor(zone);
                HubFinish.SetFinish(kind == ZoneKind.Golden ? UiPalette.Gold :
                    kind == ZoneKind.Silver ? UiPalette.Hex("A1B1B7") : UiPalette.Hex("A08457"));
            }
            SetAngle(0);
        }

        void LayoutItem(int index, string assetName)
        {
            var icon = Icons[index];
            if (icon.sprite == null) return;

            float maxWidth = artworkSize.x;
            float maxHeight = artworkSize.y;
            float gap = artworkLabelGap;
            float labelHeight = Amounts[index].rectTransform.rect.height;
            var visible = RewardArtLayout.VisibleSize(assetName, icon.sprite.rect.size);
            float scale = Mathf.Min(maxWidth / visible.x, maxHeight / visible.y);
            float artworkHeight = visible.y * scale;
            var slotSize = Slots[index].rect.size;
            float top = (slotSize.y - artworkHeight - gap - labelHeight) * .5f;
            var target = new Rect((slotSize.x - maxWidth) * .5f, top, maxWidth, artworkHeight);
            var fit = RewardArtLayout.Fit(assetName, icon.sprite.rect.size, target);

            // Transparent texture margins must not affect the visible item's size or center.
            var artRect = icon.rectTransform;
            artRect.anchorMin = artRect.anchorMax = artRect.pivot = new Vector2(0, 1);
            artRect.anchoredPosition = new Vector2(fit.x, -fit.y);
            artRect.sizeDelta = fit.size;
            icon.preserveAspect = true;

            var label = Amounts[index];
            var labelRect = label.rectTransform;
            labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot = new Vector2(0, 1);
            labelRect.anchoredPosition = new Vector2((slotSize.x - labelRect.rect.width) * .5f, -top - artworkHeight - gap);
        }

        public static string Compact(int amount) => amount >= 1000000 ? (amount / 1000000f).ToString("0.#") + "M" :
            amount >= 1000 ? (amount / 1000f).ToString("0.#") + "K" : amount.ToString();

        void SetAngle(float value)
        {
            angle = value;
            Rotor.localRotation = Quaternion.Euler(0, 0, value);
            foreach (var slot in Slots)
                slot.localRotation = Quaternion.Euler(0, 0, -value);
        }

        public void Play(int targetIndex, float duration, bool reducedMotion, Action tick, Action finished)
        {
            if (targetIndex < 0 || targetIndex >= Slots.Length)
                throw new ArgumentOutOfRangeException(nameof(targetIndex));
            if (finished == null) throw new ArgumentNullException(nameof(finished));

            spin?.Kill();
            ResetMechanics();
            ClearHighlight();

            float start = angle;
            float target = Mathf.Ceil(start / 360) * 360 + (reducedMotion ? 360 : 1800) + targetIndex * SliceAngle;
            float total = reducedMotion ? .9f : Mathf.Max(duration, 1.2f);
            float travelDuration = total - WindupDuration - SettleDuration;
            int lastSegment = Mathf.FloorToInt(start / SliceAngle);
            float lastDetent = -1;
            float lastTick = -1;

            spin = DOVirtual.Float(0, total, total, elapsed =>
            {
                if (reducedMotion)
                {
                    float progress = elapsed / total;
                    SetAngle(Mathf.LerpUnclamped(start, target, 1 - Mathf.Pow(1 - progress, 3)));
                    int reducedSegment = Mathf.FloorToInt(angle / SliceAngle);
                    if (reducedSegment > lastSegment && elapsed - lastTick >= .08f)
                    {
                        lastSegment = reducedSegment;
                        lastTick = elapsed;
                        tick?.Invoke();
                    }
                    return;
                }

                if (elapsed < WindupDuration)
                {
                    float progress = elapsed / WindupDuration;
                    float tension = Smooth(progress);
                    SetAngle(start - WindupAngle * tension);
                    HubMotion.localScale = Vector3.one * (1 - tension * .055f);
                    HubMotion.localRotation = Quaternion.Euler(0, 0, -4 * tension);
                    Pointer.localRotation = Quaternion.Euler(0, 0, -2 * tension);
                    return;
                }

                float travel = Mathf.Clamp01((elapsed - WindupDuration) / travelDuration);
                float release = Mathf.Clamp01((elapsed - WindupDuration) / .24f);
                if (travel < 1)
                {
                    float position = TravelProgress(travel);
                    SetAngle(Mathf.LerpUnclamped(start - WindupAngle, target + LandingOvershoot, position));
                    HubMotion.localScale = Vector3.one * (1 - .055f * (1 - EaseOut(release)));
                    HubMotion.localRotation = Quaternion.Euler(0, 0, -4 * (1 - EaseOut(release)));
                }
                else
                {
                    float settle = Mathf.Clamp01((elapsed - WindupDuration - travelDuration) / SettleDuration);
                    // The pawl catches just beyond the marker, then seats against the selected chamber.
                    SetAngle(target + LandingOvershoot * (1 - Smooth(settle)));
                    float pulse = Mathf.Sin(settle * Mathf.PI);
                    Halo.color = new Color(1, .75f, .38f, pulse * .20f);
                    HubMotion.localScale = Vector3.one * (1 + pulse * .018f);
                }

                int segment = Mathf.FloorToInt(angle / SliceAngle);
                if (segment > lastSegment)
                {
                    lastSegment = segment;
                    lastDetent = elapsed;
                    if (elapsed - lastTick >= .045f)
                    {
                        lastTick = elapsed;
                        tick?.Invoke();
                    }
                }

                // One damped spring, sampled by the spin timeline: no tween is created for each tooth.
                float age = elapsed - lastDetent;
                float pointerAngle = lastDetent < 0 ? 0 : -23 * Mathf.Exp(-age * 29) * Mathf.Cos(age * 43);
                Pointer.localRotation = Quaternion.Euler(0, 0, pointerAngle);
            }).SetEase(Ease.Linear).SetUpdate(true).SetTarget(this).OnComplete(() =>
            {
                spin = null;
                SetAngle(targetIndex * SliceAngle);
                ResetMechanics();
                ClearHighlight();
                finished();
            });
        }

        // Integral of a velocity profile: smooth acceleration, free spin, then a long cubic brake.
        // Normalizing the area preserves the exact selected angle for every spin duration and index.
        static float TravelProgress(float progress)
        {
            const float accelerate = .17f;
            const float coast = .20f;
            const float brake = 1 - accelerate - coast;
            const float area = accelerate * .5f + coast + brake * .25f;
            float distance;
            if (progress < accelerate)
            {
                float t = progress / accelerate;
                distance = accelerate * (t * t * t - .5f * t * t * t * t);
            }
            else if (progress < accelerate + coast)
            {
                distance = accelerate * .5f + progress - accelerate;
            }
            else
            {
                float t = (progress - accelerate - coast) / brake;
                distance = accelerate * .5f + coast + brake * (t - 1.5f * t * t + t * t * t - .25f * t * t * t * t);
            }
            return Mathf.Clamp01(distance / area);
        }

        static float Smooth(float value) => value * value * (3 - 2 * value);
        static float EaseOut(float value) => 1 - Mathf.Pow(1 - value, 3);

        void ClearHighlight()
        {
            if (Halo != null)
            {
                Halo.DOKill();
                Halo.color = new Color(1, .75f, .38f, 0);
            }
            if (Flash != null)
            {
                Flash.DOKill();
                Flash.color = Color.clear;
            }
        }

        void ResetMechanics()
        {
            if (Pointer != null) Pointer.localRotation = Quaternion.identity;
            if (HubMotion != null)
            {
                HubMotion.localScale = Vector3.one;
                HubMotion.localRotation = Quaternion.identity;
            }
        }

        void OnEnable() => spin?.Play();
        void OnDisable() => spin?.Pause();
        void OnDestroy()
        {
            spin?.Kill();
            ResetMechanics();
            ClearHighlight();
        }
    }
}
