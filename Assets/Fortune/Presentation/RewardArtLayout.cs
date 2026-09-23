using UnityEngine;

namespace Vertigo.Fortune.Presentation
{
    /// <summary>Fits visible reward artwork inside a box without changing or stretching the sprite.</summary>
    public static class RewardArtLayout
    {
        public static void FitInto(string assetName, UnityEngine.UI.Image image, RectTransform bounds)
        {
            if (image.sprite == null) return;
            var fit = Fit(assetName, image.sprite.rect.size, new Rect(Vector2.zero, bounds.rect.size));
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(fit.x, -fit.y);
            rect.sizeDelta = fit.size;
            image.preserveAspect = true;
        }

        public static Vector2 VisibleSize(string assetName, Vector2 sourceSize)
        {
            if (assetName != null && RewardArtBounds.All.TryGetValue(assetName, out var bounds))
                return Vector2.Scale(sourceSize, bounds.size);
            return sourceSize;
        }

        public static Vector3 VisibleCenter(string assetName, RectTransform artwork)
        {
            Rect bounds;
            if (assetName == null || !RewardArtBounds.All.TryGetValue(assetName, out bounds))
                bounds = new Rect(0, 0, 1, 1);
            var rect = artwork.rect;
            return artwork.TransformPoint(new Vector3(
                rect.xMin + rect.width * bounds.center.x,
                rect.yMax - rect.height * bounds.center.y, 0));
        }

        /// <summary>Returns the complete image rectangle. Target coordinates use a top-left origin.</summary>
        public static Rect Fit(string assetName, Vector2 sourceSize, Rect target)
        {
            if (sourceSize.x <= 0 || sourceSize.y <= 0 || target.width <= 0 || target.height <= 0)
                return target;
            Rect bounds;
            if (assetName == null || !RewardArtBounds.All.TryGetValue(assetName, out bounds))
                bounds = new Rect(0, 0, 1, 1);
            var visibleSize = Vector2.Scale(bounds.size, sourceSize);
            float scale = Mathf.Min(target.width / visibleSize.x, target.height / visibleSize.y);
            var fullSize = sourceSize * scale;
            var visibleOrigin = target.position + (target.size - visibleSize * scale) * .5f;
            var fullOrigin = visibleOrigin - Vector2.Scale(bounds.position, fullSize);
            return new Rect(fullOrigin, fullSize);
        }
    }
}
