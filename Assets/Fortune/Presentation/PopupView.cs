using DG.Tweening;
using UnityEngine;

namespace Vertigo.Fortune.Presentation
{
    public sealed class PopupView : MonoBehaviour
    {
        [SerializeField] CanvasGroup backdropGroup;
        [SerializeField] RectTransform contentMotion;
        [SerializeField] CanvasGroup contentGroup;
        [SerializeField] bool fadeContentOnly;
        [SerializeField, Min(0)] float fadeDuration = .2f;
        [SerializeField, Min(0)] float openDuration = .32f;
        [SerializeField, Range(.5f, 1f)] float openingScale = .93f;

        Vector3 restingScale;
        bool capturedLayout;

        public void Open(bool reducedMotion)
        {
            CaptureLayout();
            StopAnimation();
            gameObject.SetActive(true);
            backdropGroup.blocksRaycasts = true;
            backdropGroup.interactable = true;
            contentMotion.localScale = restingScale;
            contentGroup.alpha = 1;
            backdropGroup.alpha = 1;

            if (fadeContentOnly)
            {
                // The result keeps the blast backdrop covered while its content appears.
                contentGroup.alpha = 0;
                contentGroup.DOFade(1, reducedMotion ? .08f : fadeDuration).SetUpdate(true);
                return;
            }

            backdropGroup.alpha = 0;
            backdropGroup.DOFade(1, reducedMotion ? .08f : fadeDuration).SetUpdate(true);
            if (!reducedMotion)
            {
                contentMotion.localScale = restingScale * openingScale;
                contentMotion.DOScale(restingScale, openDuration).SetEase(Ease.OutBack).SetUpdate(true);
            }
        }

        public void Hide()
        {
            StopAnimation();
            backdropGroup.blocksRaycasts = false;
            backdropGroup.interactable = false;
            gameObject.SetActive(false);
        }

        void Awake() => CaptureLayout();

        void CaptureLayout()
        {
            if (capturedLayout) return;
            restingScale = contentMotion.localScale;
            capturedLayout = true;
        }

        void StopAnimation()
        {
            if (backdropGroup != null) backdropGroup.DOKill();
            if (contentGroup != null) contentGroup.DOKill();
            if (contentMotion != null)
            {
                contentMotion.DOKill();
                if (capturedLayout) contentMotion.localScale = restingScale;
            }
        }

        void OnDisable() => StopAnimation();

        void OnValidate()
        {
            if (backdropGroup == null) backdropGroup = GetComponent<CanvasGroup>();
            if (contentMotion == null) contentMotion = transform.Find("ui_modal_content_motion") as RectTransform;
            if (contentGroup == null && contentMotion != null) contentGroup = contentMotion.GetComponent<CanvasGroup>();
        }
    }
}
