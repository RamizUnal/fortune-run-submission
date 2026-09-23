using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using Vertigo.Fortune.Content;
using Vertigo.Fortune.Domain;

namespace Vertigo.Fortune.Presentation
{
    /// <summary>Stages the reward and transfers it to the reserved loot slot before committing it.</summary>
    public sealed class RewardRevealView : MonoBehaviour
    {
        public enum RevealPhase { Idle, Revealing, Flying, Landed }

        [SerializeField] RectTransform itemMotion;
        [SerializeField] UnityEngine.UI.Image item;
        [SerializeField] UnityEngine.UI.Image shade;
        [SerializeField] CanvasGroup itemGroup;
        [SerializeField] CanvasGroup captionGroup;
        [SerializeField] RectTransform captionMotion;
        [SerializeField] TMP_Text itemName;
        [SerializeField] TMP_Text amount;
        [SerializeField] UnityEngine.UI.Image backlight;
        [SerializeField] UnityEngine.UI.Image rays;
        [SerializeField] UnityEngine.UI.Image glint;
        [SerializeField] UnityEngine.UI.Image[] sparks;

        [Header("Layout")]
        [SerializeField] RectTransform heroAnchor;
        [SerializeField] RectTransform artBounds;

        [Header("Animation")]
        [SerializeField, Min(.9f)] float revealDuration = 1.58f;
        [SerializeField, Min(.9f)] float legendaryRevealDuration = 1.90f;
        [SerializeField, Min(.1f)] float transferDuration = .82f;
        [SerializeField, Min(0)] float transferArcHeight = 145;

        Sequence animation;
        Color accent;
        float intensity;
        Vector2 heroPosition;
        Vector2 captionRestingPosition;

        public RevealPhase Phase { get; private set; }
        public bool IsPlaying => Phase != RevealPhase.Idle;

        void Awake()
        {
            captionRestingPosition = captionMotion.anchoredPosition;
        }

        public void Play(GameContent content, WheelSlice reward, Vector3 sourceWorldPosition,
            bool reducedMotion, Func<RewardLandingTarget> prepareDestination, Action arrived, Action completed,
            Action revealed = null)
        {
            if (IsPlaying) throw new InvalidOperationException("A reward is already being presented.");
            if (reward == null || reward.IsBomb) throw new ArgumentException("A reveal requires a reward.", nameof(reward));
            if (prepareDestination == null) throw new ArgumentNullException(nameof(prepareDestination));
            if (arrived == null) throw new ArgumentNullException(nameof(arrived));

            ResetEffects();
            heroPosition = ToLocal(heroAnchor.position);
            if (backlight != null) backlight.rectTransform.anchoredPosition = heroPosition;
            if (rays != null) rays.rectTransform.anchoredPosition = heroPosition;
            item.sprite = content.GetSprite(reward.Reward.AssetName);
            RewardArtLayout.FitInto(reward.Reward.AssetName, item, artBounds);
            if (glint != null)
            {
                glint.sprite = item.sprite;
                glint.rectTransform.anchorMin = item.rectTransform.anchorMin;
                glint.rectTransform.anchorMax = item.rectTransform.anchorMax;
                glint.rectTransform.pivot = item.rectTransform.pivot;
                glint.rectTransform.anchoredPosition = item.rectTransform.anchoredPosition;
                glint.rectTransform.sizeDelta = item.rectTransform.sizeDelta;
            }
            Vector3 artworkCenter = itemMotion.InverseTransformPoint(
                RewardArtLayout.VisibleCenter(reward.Reward.AssetName, item.rectTransform));
            Vector3 artworkWidth = itemMotion.InverseTransformVector(
                item.rectTransform.TransformVector(Vector3.right * item.rectTransform.rect.width));
            float landingScale = 1;
            float landingAngle = 0;
            bool legendary = reward.Reward.Rarity == "Legendary";
            bool epic = reward.Reward.Rarity == "Epic";
            bool rare = reward.Reward.Rarity == "Rare";
            accent = legendary ? UiPalette.Gold : epic ? new Color(.77f, .59f, 1) :
                rare ? new Color(.48f, .76f, 1) : UiPalette.Teal;
            intensity = legendary ? 1 : epic ? .86f : rare ? .72f : .48f;
            itemName.text = reward.Reward.Name.ToUpperInvariant();
            amount.text = "+" + WheelView.Compact(reward.Amount);
            amount.color = accent;

            Vector2 source = ToLocal(sourceWorldPosition);
            Vector2 destination = Vector2.zero;
            itemMotion.anchoredPosition = reducedMotion ? heroPosition : source;
            itemMotion.localScale = Vector3.one * (reducedMotion ? 1 : .22f);
            itemMotion.localRotation = Quaternion.Euler(0, 0, reducedMotion ? 0 : -13);
            itemGroup.alpha = reducedMotion ? 0 : 1;
            captionGroup.alpha = 0;
            captionMotion.anchoredPosition = captionRestingPosition + (reducedMotion ? Vector2.zero : Vector2.down * 17);
            Phase = RevealPhase.Revealing;
            animation = DOTween.Sequence().SetUpdate(true).SetTarget(this);

            float flightStart = reducedMotion ? .65f : legendary ? legendaryRevealDuration : revealDuration;
            float flightDuration = reducedMotion ? .16f : transferDuration;
            animation.Insert(0, shade.DOFade(.92f, reducedMotion ? .12f : .32f).SetEase(Ease.OutQuad));
            if (reducedMotion)
            {
                animation.Insert(0, itemGroup.DOFade(1, .16f));
                animation.Insert(.08f, captionGroup.DOFade(1, .16f));
                animation.InsertCallback(.12f, () => revealed?.Invoke());
            }
            else
            {
                var lift = Vector2.Lerp(source, heroPosition, .46f) + Vector2.up * 110;
                animation.Insert(0, DOVirtual.Float(0, 1, .72f, progress =>
                    itemMotion.anchoredPosition = Bezier(source, lift, heroPosition, progress)).SetEase(Ease.OutCubic));
                animation.Insert(0, itemMotion.DOScale(1.08f, .48f).SetEase(Ease.OutCubic));
                animation.Insert(.48f, itemMotion.DOScale(1, .24f).SetEase(Ease.OutSine));
                animation.Insert(0, itemMotion.DOLocalRotate(Vector3.zero, .72f).SetEase(Ease.OutCubic));
                animation.Insert(.72f, itemMotion.DOAnchorPos(heroPosition + Vector2.up * 7, flightStart - .88f).SetEase(Ease.InOutSine));
                animation.Insert(flightStart - .16f, itemMotion.DOAnchorPos(heroPosition + new Vector2(-12, 7), .16f).SetEase(Ease.InQuad));
                animation.InsertCallback(.30f, () => revealed?.Invoke());
                animation.Insert(.24f, DOVirtual.Float(0, 1, flightStart - .24f, RenderReveal).SetEase(Ease.Linear));
                animation.Insert(.45f, captionGroup.DOFade(1, .25f));
                animation.Insert(.45f, captionMotion.DOAnchorPos(captionRestingPosition, .38f).SetEase(Ease.OutCubic));
            }

            Vector2 flightOrigin = heroPosition;
            animation.InsertCallback(flightStart, () =>
            {
                Phase = RevealPhase.Flying;
                var landing = prepareDestination();
                Vector3 localWidth = itemMotion.parent.InverseTransformVector(landing.WorldWidth);
                landingScale = localWidth.magnitude / artworkWidth.magnitude;
                landingAngle = Vector3.SignedAngle(artworkWidth, localWidth, Vector3.forward);
                var landingRotation = Quaternion.Euler(0, 0, landingAngle);
                destination = ToLocal(landing.WorldCenter) - (Vector2)(landingRotation * (artworkCenter * landingScale));
                flightOrigin = itemMotion.anchoredPosition;
            });
            if (reducedMotion)
            {
                animation.Insert(flightStart, itemGroup.DOFade(0, flightDuration));
                animation.Insert(flightStart, captionGroup.DOFade(0, flightDuration));
                animation.Insert(flightStart, shade.DOFade(0, flightDuration));
            }
            else
            {
                animation.Insert(flightStart, DOVirtual.Float(0, 1, flightDuration, progress =>
                {
                    var control = new Vector2(Mathf.Lerp(flightOrigin.x, destination.x, .50f),
                        Mathf.Max(flightOrigin.y, destination.y) + transferArcHeight);
                    itemMotion.anchoredPosition = Bezier(flightOrigin, control, destination, progress);
                    // Match the destination artwork exactly, including transparent source padding.
                    itemMotion.localScale = Vector3.one * Mathf.Lerp(1, landingScale, progress);
                    itemMotion.localRotation = Quaternion.Euler(0, 0,
                        Mathf.Lerp(0, landingAngle, progress) - 9 * Mathf.Sin(progress * Mathf.PI));
                    RenderTrail(flightOrigin, control, destination, progress);
                }).SetEase(Ease.InOutCubic));
                animation.Insert(flightStart, captionGroup.DOFade(0, .18f));
                animation.Insert(flightStart, shade.DOFade(0, .48f).SetEase(Ease.InQuad));
                if (backlight != null) animation.Insert(flightStart, backlight.DOFade(0, .24f));
                if (rays != null) animation.Insert(flightStart, rays.DOFade(0, .18f));
            }
            animation.InsertCallback(flightStart + flightDuration, () =>
            {
                itemMotion.localScale = Vector3.one * landingScale;
                itemMotion.localRotation = Quaternion.Euler(0, 0, landingAngle);
                itemMotion.anchoredPosition = destination;
                itemGroup.alpha = 0;
                ResetEffects();
                Phase = RevealPhase.Landed;
                arrived();
            });
            animation.AppendInterval(reducedMotion ? .08f : .30f);
            animation.OnComplete(() =>
            {
                animation = null;
                Phase = RevealPhase.Idle;
                completed?.Invoke();
            });
        }

        void RenderReveal(float progress)
        {
            float bloom = Mathf.Sin(Mathf.Clamp01(progress / .2f) * Mathf.PI * .5f);
            float fade = 1 - Mathf.Clamp01((progress - .55f) / .45f);
            if (backlight != null)
            {
                backlight.color = WithAlpha(accent, bloom * (.11f + fade * .13f) * intensity);
                backlight.rectTransform.localScale = Vector3.one * Mathf.Lerp(.42f, 1.16f, 1 - Mathf.Pow(1 - progress, 3));
            }
            if (rays != null)
            {
                rays.color = WithAlpha(accent, bloom * fade * .14f * intensity);
                rays.rectTransform.localScale = Vector3.one * Mathf.Lerp(.65f, 1.12f, progress);
                rays.rectTransform.localRotation = Quaternion.Euler(0, 0, -12 + progress * 18);
            }
            if (glint != null) glint.color = WithAlpha(new Color(1, .96f, .84f), Mathf.Sin(progress * Mathf.PI) * fade * .12f * intensity);
            if (sparks == null) return;
            for (int i = 0; i < sparks.Length; i++)
            {
                float t = Mathf.Clamp01((progress - i % 3 * .035f) / .92f);
                float angle = i * 2.39996f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var rect = sparks[i].rectTransform;
                rect.anchoredPosition = heroPosition + direction * Mathf.Lerp(70, 220 + i % 4 * 15, 1 - Mathf.Pow(1 - t, 2))
                    + Vector2.up * (t * t * 35);
                rect.localRotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg - 90);
                rect.localScale = new Vector3(.25f, Mathf.Lerp(1.1f, .25f, t), 1);
                sparks[i].color = WithAlpha(accent, Mathf.Sin(t * Mathf.PI) * (1 - t) * .8f * intensity);
            }
        }

        void RenderTrail(Vector2 from, Vector2 control, Vector2 to, float progress)
        {
            if (glint != null) glint.color = Color.clear;
            if (sparks == null) return;
            for (int i = 0; i < sparks.Length; i++)
            {
                float lag = (i + 1) * .011f;
                float t = Mathf.Max(0, progress - lag);
                var point = Bezier(from, control, to, t);
                var tangent = Bezier(from, control, to, Mathf.Min(1, t + .025f)) - point;
                var rect = sparks[i].rectTransform;
                rect.anchoredPosition = point + Vector2.up * Mathf.Sin(i * 2.4f) * 6;
                rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg - 90);
                rect.localScale = new Vector3(.18f, .9f - i * .025f, 1);
                float fade = Mathf.Sin(progress * Mathf.PI) * (1 - (float)i / sparks.Length);
                sparks[i].color = WithAlpha(accent, fade * .7f * intensity);
            }
        }

        void ResetEffects()
        {
            if (backlight != null) backlight.color = Color.clear;
            if (rays != null) rays.color = Color.clear;
            if (glint != null) glint.color = Color.clear;
            if (sparks != null) foreach (var spark in sparks) if (spark != null) spark.color = Color.clear;
        }

        void OnValidate()
        {
            Resolve(ref heroAnchor, "ui_reward_hero_anchor");
            Resolve(ref artBounds, "ui_reward_art_bounds");
            Resolve(ref itemMotion, "ui_reward_item_motion");
            Resolve(ref item, "ui_image_revealed_reward_value");
            Resolve(ref shade, "ui_image_reward_shade_value");
            Resolve(ref itemGroup, "ui_reward_item_motion");
            Resolve(ref captionGroup, "ui_reward_caption_motion");
            Resolve(ref captionMotion, "ui_reward_caption_motion");
            Resolve(ref itemName, "ui_text_revealed_name_value");
            Resolve(ref amount, "ui_text_revealed_amount_value");
            Resolve(ref backlight, "ui_image_reward_backlight_value");
            Resolve(ref rays, "ui_image_reward_rays_value");
            Resolve(ref glint, "ui_image_reward_glint_value");
            ResolveImages(ref sparks, "ui_image_reward_spark_");
        }

        void Resolve<T>(ref T reference, string objectName) where T : Component
        {
            if (reference != null) return;
            foreach (var component in GetComponentsInChildren<T>(true))
            {
                if (component.name != objectName) continue;
                reference = component;
                return;
            }
        }

        void ResolveImages(ref UnityEngine.UI.Image[] references, string prefix)
        {
            if (references != null && references.Length > 0) return;
            var matches = new List<UnityEngine.UI.Image>();
            foreach (var image in GetComponentsInChildren<UnityEngine.UI.Image>(true))
                if (image.name.StartsWith(prefix, StringComparison.Ordinal)) matches.Add(image);
            references = matches.ToArray();
        }

        static Vector2 Bezier(Vector2 from, Vector2 control, Vector2 to, float t)
            => (1 - t) * (1 - t) * from + 2 * (1 - t) * t * control + t * t * to;
        static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);
        Vector2 ToLocal(Vector3 worldPosition)
        {
            Vector2 anchorOrigin = (Vector2)itemMotion.localPosition - itemMotion.anchoredPosition;
            return (Vector2)itemMotion.parent.InverseTransformPoint(worldPosition) - anchorOrigin;
        }
        void OnEnable() => animation?.Play();
        void OnDisable() => animation?.Pause();
        void OnDestroy()
        {
            animation?.Kill();
            animation = null;
            ResetEffects();
        }
    }
}
