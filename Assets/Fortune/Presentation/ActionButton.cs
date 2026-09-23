using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Vertigo.Fortune.Presentation
{
    [RequireComponent(typeof(Button))]
    public sealed class ActionButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] Button button;
        [SerializeField] TMP_Text label;
        public RectTransform Visual;
        public bool DimWhenDisabled = true;

        [SerializeField] CanvasGroup visualGroup;
        Vector3 restingScale = Vector3.one;
        Tween scaleAnimation;
        Action clickFeedback;

        public event Action Clicked;

        public void SetClickFeedback(Action feedback) => clickFeedback = feedback;

        public bool Interactable
        {
            get => button.interactable;
            set
            {
                button.interactable = value;
                if (Visual == null) return;

                if (visualGroup != null) visualGroup.alpha = value || !DimWhenDisabled ? 1 : .35f;

                if (!value) ResetScale();
            }
        }

        public string Label
        {
            set { if (label != null) label.text = value; }
        }

        public void ResolveReferences()
        {
            button = GetComponent<Button>();
            label = GetComponentInChildren<TMP_Text>(true);
            visualGroup = Visual != null ? Visual.GetComponent<CanvasGroup>() : null;
        }

        void OnValidate() => ResolveReferences();

        void Awake()
        {
            ResolveReferences();
            if (Visual != null) restingScale = Visual.localScale;
            button.onClick.AddListener(Invoke);
        }

        void Invoke()
        {
            if (!isActiveAndEnabled || !button.IsInteractable()) return;
            Clicked?.Invoke();
            // Run after the action so starting a spin or restarting cannot cut off the click.
            clickFeedback?.Invoke();
        }

        void Scale(float scale)
        {
            if (!Interactable || Visual == null) return;
            scaleAnimation?.Kill();
            scaleAnimation = Visual.DOScale(restingScale * scale, .13f).SetEase(Ease.OutCubic).SetUpdate(true);
        }

        void ResetScale()
        {
            scaleAnimation?.Kill();
            scaleAnimation = null;
            if (Visual != null) Visual.localScale = restingScale;
        }

        void OnDisable() => ResetScale();

        void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(Invoke);
            clickFeedback = null;
            ResetScale();
        }

        public void OnPointerEnter(PointerEventData e) => Scale(1.025f);
        public void OnPointerExit(PointerEventData e) => Scale(1);
        public void OnPointerDown(PointerEventData e) => Scale(.965f);
        public void OnPointerUp(PointerEventData e) => Scale(1);
    }
}
