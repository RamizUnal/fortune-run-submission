using UnityEngine;

namespace Vertigo.Fortune.Presentation
{
    /// <summary>Chamber centers measured from the supplied wheel artwork, in a 590 × 590 reference.</summary>
    internal static class WheelArtLayout
    {
        static readonly Vector2[] Bronze =
        {
            new Vector2(293.06f, 121.22f), new Vector2(415.03f, 168.46f),
            new Vector2(468.72f, 291.71f), new Vector2(421.10f, 416.70f),
            new Vector2(295.65f, 469.91f), new Vector2(168.83f, 418.83f),
            new Vector2(117.98f, 294.06f), new Vector2(171.17f, 172.64f)
        };
        static readonly Vector2[] Silver =
        {
            new Vector2(291.83f, 120.00f), new Vector2(414.98f, 167.26f),
            new Vector2(468.71f, 290.48f), new Vector2(421.09f, 415.51f),
            new Vector2(295.67f, 468.72f), new Vector2(168.83f, 417.61f),
            new Vector2(117.98f, 292.85f), new Vector2(171.16f, 171.42f)
        };
        static readonly Vector2[] Golden =
        {
            new Vector2(293.08f, 120.01f), new Vector2(415.00f, 167.26f),
            new Vector2(468.70f, 290.48f), new Vector2(421.09f, 415.47f),
            new Vector2(295.64f, 468.67f), new Vector2(168.83f, 417.60f),
            new Vector2(117.98f, 292.82f), new Vector2(171.17f, 171.45f)
        };

        public static Vector2 Center(Sprite wheel, int index, Vector2 displaySize)
        {
            var centers = wheel != null && wheel.name == "ui_spin_bronze_base" ? Bronze :
                wheel != null && wheel.name == "ui_spin_silver_base" ? Silver :
                wheel != null && wheel.name == "ui_spin_golden_base" ? Golden : null;
            Vector2 reference;
            if (centers != null) reference = centers[index];
            else
            {
                float angle = index * Mathf.PI / 4;
                reference = new Vector2(295 + Mathf.Sin(angle) * 174.5f, 295 - Mathf.Cos(angle) * 174.5f);
            }
            return Vector2.Scale(reference / 590, displaySize);
        }
    }
}
