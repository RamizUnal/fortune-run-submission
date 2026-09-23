using UnityEngine;

namespace Vertigo.Fortune.Presentation
{
    /// <summary>Keeps a backdrop over the whole canvas while its content stays inside the safe area.</summary>
    [ExecuteAlways, RequireComponent(typeof(RectTransform))]
    public sealed class CanvasBackdrop : MonoBehaviour
    {
        readonly Vector3[] corners = new Vector3[4];
        RectTransform rect;
        RectTransform canvasRect;

        void OnEnable()
        {
            rect = (RectTransform)transform;
            var canvas = GetComponentInParent<Canvas>();
            canvasRect = canvas != null ? (RectTransform)canvas.rootCanvas.transform : null;
            Fit();
        }

        void LateUpdate() => Fit();

        public void Fit()
        {
            if (rect == null || canvasRect == null || rect.parent == null) return;
            canvasRect.GetWorldCorners(corners);
            Vector3 bottomLeft = rect.parent.InverseTransformPoint(corners[0]);
            Vector3 topRight = rect.parent.InverseTransformPoint(corners[2]);
            var size = new Vector2(topRight.x - bottomLeft.x, topRight.y - bottomLeft.y);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            if (rect.sizeDelta != size) rect.sizeDelta = size;
            var position = new Vector3(bottomLeft.x, bottomLeft.y, 0);
            if (rect.localPosition != position) rect.localPosition = position;
        }
    }
}
