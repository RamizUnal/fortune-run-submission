using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Vertigo.Fortune.Presentation
{
    /// <summary>Stages the bomb, impact, and aftermath without changing the run's rules.</summary>
    public sealed class BombRevealView : MonoBehaviour
    {
        public enum BombPhase { Idle, Arming, Detonating, Aftermath }

        static readonly Color Hot = new Color(1, .79f, .39f);
        static readonly Color Ember = new Color(1, .38f, .09f);

        [SerializeField] UnityEngine.UI.Image shade;
        [SerializeField] UnityEngine.UI.Image bomb;
        [SerializeField] RectTransform bombMotion;
        [SerializeField] CanvasGroup bombGroup;
        [SerializeField] UnityEngine.UI.Image fire;
        [SerializeField] UnityEngine.UI.Image impact;
        [SerializeField] UnityEngine.UI.Image core;
        [SerializeField] UnityEngine.UI.Image fuse;
        [SerializeField] UnityEngine.UI.Image[] smoke;
        [SerializeField] UnityEngine.UI.Image[] embers;

        [Header("Layout")]
        [SerializeField] RectTransform centerAnchor;

        [Header("Animation")]
        [SerializeField, Min(.6f)] float detonationTime = 1.02f;
        [SerializeField, Min(1)] float blastDuration = 1.66f;

        Sequence animation;
        RectTransform recoilMotion;
        Vector2 recoilOrigin;
        Vector2 center;

        public BombPhase Phase { get; private set; }
        public bool IsPlaying => Phase != BombPhase.Idle;

        public void Play(Vector3 sourceWorldPosition, RectTransform wheelMotion, bool reducedMotion,
            Action fuseIgnited, Action detonated, Action settling, Action completed)
        {
            if (IsPlaying) throw new InvalidOperationException("A bomb is already being presented.");
            if (detonated == null) throw new ArgumentNullException(nameof(detonated));
            if (completed == null) throw new ArgumentNullException(nameof(completed));

            center = ToLocal(centerAnchor.position);
            ResetVisuals();
            fire.rectTransform.anchoredPosition = center;
            impact.rectTransform.anchoredPosition = center;
            core.rectTransform.anchoredPosition = center;
            recoilMotion = wheelMotion;
            if (recoilMotion != null) recoilOrigin = recoilMotion.anchoredPosition;
            Phase = BombPhase.Arming;
            bombMotion.anchoredPosition = reducedMotion ? center : ToLocal(sourceWorldPosition);
            bombMotion.localScale = Vector3.one * (reducedMotion ? 1 : .28f);
            bombGroup.alpha = reducedMotion ? 0 : 1;
            animation = DOTween.Sequence().SetUpdate(true).SetTarget(this);

            if (reducedMotion)
                BuildReducedSequence(fuseIgnited, detonated, settling);
            else
                BuildFullSequence(bombMotion.anchoredPosition, fuseIgnited, detonated, settling);

            animation.OnComplete(() =>
            {
                animation = null;
                ResetVisuals();
                Phase = BombPhase.Idle;
                completed();
            });
        }

        void BuildFullSequence(Vector2 source, Action fuseIgnited, Action detonated, Action settling)
        {
            animation.Append(shade.DOFade(.71f, .38f).SetEase(Ease.OutCubic));
            animation.Insert(0, DOVirtual.Float(0, detonationTime, detonationTime,
                elapsed => RenderArming(source, elapsed)).SetEase(Ease.Linear));
            animation.InsertCallback(.50f, () => fuseIgnited?.Invoke());
            animation.InsertCallback(detonationTime, () =>
            {
                Phase = BombPhase.Detonating;
                bombGroup.alpha = 0;
                detonated();
            });
            animation.Insert(detonationTime, DOVirtual.Float(0, 1, blastDuration, RenderBlast).SetEase(Ease.Linear));
            animation.Insert(detonationTime, DOVirtual.Float(0, 1, .48f, Recoil).SetEase(Ease.Linear));
            animation.InsertCallback(detonationTime + .27f, () =>
            {
                Phase = BombPhase.Aftermath;
                settling?.Invoke();
            });
            animation.Insert(detonationTime + .08f, shade.DOFade(.28f, .30f).SetEase(Ease.OutQuad));
            animation.Insert(detonationTime + .84f, shade.DOColor(new Color(.015f, .035f, .06f, .93f), .58f));
            animation.AppendInterval(.12f);
        }

        void RenderArming(Vector2 source, float elapsed)
        {
            float lift = Mathf.Clamp01(elapsed / .49f);
            float easedLift = EaseOut(lift);
            Vector2 arc = Vector2.up * (Mathf.Sin(lift * Mathf.PI) * 36);
            bombMotion.anchoredPosition = Vector2.LerpUnclamped(source, center, easedLift) + arc;

            float tension = Mathf.Clamp01((elapsed - .50f) / (detonationTime - .50f));
            float anticipation = tension * tension;
            float scale = Mathf.Lerp(.28f, 1.08f, easedLift) + anticipation * .075f;
            bombMotion.localScale = Vector3.one * scale;
            float angle = -12 * (1 - easedLift) + Mathf.Sin(lift * Mathf.PI) * 4;
            angle += Mathf.Sin(tension * Mathf.PI * 10) * anticipation * 2.4f;
            bombMotion.localRotation = Quaternion.Euler(0, 0, angle);

            // The fuse grows hotter and less stable, with a short bright squeeze before ignition.
            float flicker = .5f + .5f * Mathf.Sin(tension * 39 + Mathf.Sin(tension * 17) * 1.7f);
            float energy = Mathf.Clamp01(tension * 4) * (.45f + flicker * .38f + anticipation * .30f);
            fuse.color = WithAlpha(Color.Lerp(Hot, new Color(1, .96f, .78f), anticipation), energy);
            fuse.rectTransform.localScale = Vector3.one * (.46f + tension * .40f + flicker * .18f);
            fuse.rectTransform.localRotation = Quaternion.Euler(0, 0, elapsed * 210);
        }

        void BuildReducedSequence(Action fuseIgnited, Action detonated, Action settling)
        {
            animation.Append(shade.DOFade(.66f, .18f));
            animation.Join(bombGroup.DOFade(1, .18f));
            animation.AppendCallback(() => fuseIgnited?.Invoke());
            animation.AppendInterval(.38f);
            animation.AppendCallback(() =>
            {
                Phase = BombPhase.Detonating;
                detonated();
            });
            animation.Append(bombGroup.DOFade(0, .22f));
            animation.Join(shade.DOFade(.20f, .3f));
            animation.AppendCallback(() =>
            {
                Phase = BombPhase.Aftermath;
                settling?.Invoke();
            });
            animation.AppendInterval(.18f);
            animation.Append(shade.DOColor(new Color(.015f, .035f, .06f, .93f), .18f));
        }

        void RenderBlast(float progress)
        {
            float seconds = progress * blastDuration;
            float burst = Mathf.Clamp01(seconds / .48f);
            float coreProgress = Mathf.Clamp01(seconds / .22f);
            core.color = WithAlpha(new Color(1, .95f, .81f), Mathf.Pow(1 - coreProgress, 1.55f));
            core.rectTransform.localScale = Vector3.one * Mathf.Lerp(.32f, 1.46f, EaseOut(coreProgress));
            core.rectTransform.localRotation = Quaternion.Euler(0, 0, 24);

            float fireEnvelope = Mathf.Pow(1 - burst, 1.4f);
            fire.color = WithAlpha(Color.Lerp(Hot, Ember, burst), fireEnvelope);
            fire.rectTransform.localScale = Vector3.one * Mathf.Lerp(.28f, 1.42f, EaseOut(burst));
            fire.rectTransform.localRotation = Quaternion.Euler(0, 0, -18 + burst * 34);
            float impactProgress = Mathf.Clamp01(seconds / .30f);
            impact.color = WithAlpha(Hot, Mathf.Pow(1 - impactProgress, 2) * .90f);
            impact.rectTransform.localScale = Vector3.one * Mathf.Lerp(.34f, 1.55f, EaseOut(impactProgress));

            for (int i = 0; i < smoke.Length; i++)
            {
                float delay = .025f + .034f * (i % 3);
                float duration = i < 3 ? 1.52f : 1.30f;
                float t = Mathf.Clamp01((seconds - delay) / duration);
                float angle = i * 2.39996f + .31f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 tangent = new Vector2(-direction.y, direction.x);
                float reach = 56 + (i % 3) * 35;
                Vector2 expansion = direction * Mathf.Lerp(10, reach, EaseOut(t));
                Vector2 curl = tangent * (Mathf.Sin(t * Mathf.PI) * (i % 2 == 0 ? 24 : -20));
                Vector2 drift = Vector2.up * (t * t * (65 + i * 4));
                smoke[i].rectTransform.anchoredPosition = center + expansion + curl + drift;
                smoke[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(.24f, 1.15f + i % 3 * .15f, EaseOut(t));
                smoke[i].rectTransform.localRotation = Quaternion.Euler(0, 0, i * 53 + t * (i % 2 == 0 ? 25 : -22));
                float alpha = Mathf.Clamp01(t / .065f) * Mathf.Pow(1 - t, 1.26f) * .86f;
                Color color = Color.Lerp(new Color(.78f, .51f, .26f), new Color(.37f, .40f, .43f), Mathf.Clamp01(t * 3.8f));
                smoke[i].color = WithAlpha(color, alpha);
            }

            for (int i = 0; i < embers.Length; i++)
            {
                float delay = (i % 3) * .014f;
                float duration = .55f + i % 5 * .13f;
                float elapsed = Mathf.Max(0, seconds - delay);
                float t = Mathf.Clamp01(elapsed / duration);
                float angle = i * 2.39996f + .15f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float speed = 270 + i % 4 * 70;
                float dragTime = (1 - Mathf.Exp(-t * 2.4f)) / 2.4f;
                Vector2 displacement = direction * (speed * duration * dragTime)
                    + Vector2.down * (150 * duration * duration * t * t);
                Vector2 velocity = direction * (speed * Mathf.Exp(-t * 2.4f))
                    + Vector2.down * (300 * duration * t);
                var rect = embers[i].rectTransform;
                rect.anchoredPosition = center + displacement;
                rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg - 90);
                float width = i % 4 == 0 ? .52f : .29f;
                rect.localScale = new Vector3(width * (1 - t * .55f), Mathf.Lerp(1.25f, .12f, t), 1);
                float alpha = seconds < delay ? 0 : Mathf.Pow(1 - t, 1.1f);
                embers[i].color = WithAlpha(Color.Lerp(Hot, Ember, t), alpha);
            }
        }

        void Recoil(float progress)
        {
            if (recoilMotion == null) return;
            float envelope = Mathf.Pow(1 - progress, 2.6f);
            var offset = new Vector2(Mathf.Sin(progress * Mathf.PI * 12) * 15,
                Mathf.Sin(progress * Mathf.PI * 9 + .7f) * 10) * envelope;
            recoilMotion.anchoredPosition = recoilOrigin + offset;
        }

        void ResetVisuals()
        {
            if (recoilMotion != null) recoilMotion.anchoredPosition = recoilOrigin;
            if (shade != null) shade.color = new Color(.035f, .027f, .024f, 0);
            if (bombGroup != null) bombGroup.alpha = 0;
            if (bomb != null) bomb.color = Color.white;
            if (bombMotion != null)
            {
                bombMotion.localRotation = Quaternion.identity;
                bombMotion.localScale = Vector3.one;
                bombMotion.anchoredPosition = center;
            }
            if (fuse != null) fuse.color = Color.clear;
            if (fire != null) fire.color = Color.clear;
            if (impact != null) impact.color = Color.clear;
            if (core != null) core.color = Color.clear;
            if (smoke != null) foreach (var image in smoke) if (image != null) image.color = Color.clear;
            if (embers != null) foreach (var image in embers) if (image != null) image.color = Color.clear;
        }

        void OnValidate()
        {
            Resolve(ref centerAnchor, "ui_bomb_center_anchor");
            Resolve(ref shade, "ui_image_bomb_shade_value");
            Resolve(ref bomb, "ui_image_bomb_value");
            Resolve(ref bombMotion, "ui_bomb_icon_motion");
            Resolve(ref bombGroup, "ui_bomb_icon_motion");
            Resolve(ref fire, "ui_image_bomb_fire_value");
            Resolve(ref impact, "ui_image_bomb_impact_value");
            Resolve(ref core, "ui_image_bomb_core_value");
            Resolve(ref fuse, "ui_image_bomb_fuse_value");
            ResolveImages(ref smoke, "ui_image_bomb_smoke_");
            ResolveImages(ref embers, "ui_image_bomb_ember_");
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

        Vector2 ToLocal(Vector3 worldPosition)
        {
            var root = (RectTransform)transform;
            Vector3 local = root.InverseTransformPoint(worldPosition);
            return new Vector2(local.x - root.rect.xMin, local.y - root.rect.yMax);
        }

        static float EaseOut(float value) => 1 - Mathf.Pow(1 - value, 3);
        static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);
        void OnEnable() => animation?.Play();
        void OnDisable() => animation?.Pause();
        void OnDestroy()
        {
            animation?.Kill();
            ResetVisuals();
        }
    }
}
