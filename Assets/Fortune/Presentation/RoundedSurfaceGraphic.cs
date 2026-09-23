using UnityEngine;
using UnityEngine.UI;

namespace Vertigo.Fortune.Presentation
{
    /// <summary>A lightweight rounded surface whose fill and outline share one contour.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RoundedSurfaceGraphic : MaskableGraphic
    {
        const int SegmentsPerCorner = 10;
        const int ContourVertices = (SegmentsPerCorner + 1) * 4;
        [SerializeField, Min(0)] float radius = 12f;
        [SerializeField, Min(0)] float outlineWidth = 1f;
        [SerializeField] Color outlineColor = new Color32(42, 69, 87, 255);

        public void Configure(Color fill, Color outline, float cornerRadius, float borderWidth)
        {
            color = fill; outlineColor = outline;
            radius = Mathf.Max(0, cornerRadius); outlineWidth = Mathf.Max(0, borderWidth);
            raycastTarget = false; maskable = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var bounds = rectTransform.rect;
            if (bounds.width <= 0 || bounds.height <= 0) return;
            float corner = Mathf.Min(radius, Mathf.Min(bounds.width, bounds.height) * .5f);
            float border = Mathf.Min(outlineWidth, Mathf.Min(bounds.width, bounds.height) * .5f);
            float feather = .65f / Mathf.Max(1, canvas != null ? canvas.scaleFactor : 1);
            var inner = Inset(bounds, border);
            var outside = Inset(bounds, -feather);
            Color edge = border > 0 ? outlineColor : color;
            mesh.AddVert(bounds.center, FillAt(bounds.center.y, bounds), Vector2.zero);
            for (int i = 0; i < ContourVertices; i++)
            {
                Vector2 p = ContourPoint(inner, Mathf.Max(0, corner - border), i);
                mesh.AddVert(p, FillAt(p.y, bounds), Vector2.zero);
            }
            for (int i = 0; i < ContourVertices; i++)
                mesh.AddVert(ContourPoint(inner, Mathf.Max(0, corner - border), i), edge, Vector2.zero);
            for (int i = 0; i < ContourVertices; i++)
                mesh.AddVert(ContourPoint(bounds, corner, i), edge, Vector2.zero);
            var transparent = edge; transparent.a = 0;
            for (int i = 0; i < ContourVertices; i++)
                mesh.AddVert(ContourPoint(outside, corner + feather, i), transparent, Vector2.zero);
            for (int i = 0; i < ContourVertices; i++)
            {
                int next = (i + 1) % ContourVertices;
                mesh.AddTriangle(0, 1 + i, 1 + next);
                RingQuad(mesh, 1 + ContourVertices, 1 + ContourVertices * 2, i, next);
                RingQuad(mesh, 1 + ContourVertices * 2, 1 + ContourVertices * 3, i, next);
            }
        }

        Color FillAt(float y, Rect bounds)
        {
            float light = 1f + .035f * Mathf.InverseLerp(bounds.yMin, bounds.yMax, y);
            return new Color(color.r * light, color.g * light, color.b * light, color.a);
        }

        static Rect Inset(Rect bounds, float amount)
            => Rect.MinMaxRect(bounds.xMin + amount, bounds.yMin + amount, bounds.xMax - amount, bounds.yMax - amount);

        static Vector2 ContourPoint(Rect bounds, float cornerRadius, int index)
        {
            int corner = index / (SegmentsPerCorner + 1);
            int segment = index % (SegmentsPerCorner + 1);
            var center = new Vector2(corner < 2 ? bounds.xMax - cornerRadius : bounds.xMin + cornerRadius,
                corner == 1 || corner == 2 ? bounds.yMax - cornerRadius : bounds.yMin + cornerRadius);
            float angle = (-90 + corner * 90 + segment * 90f / SegmentsPerCorner) * Mathf.Deg2Rad;
            return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * cornerRadius;
        }

        static void RingQuad(VertexHelper mesh, int inner, int outer, int current, int next)
        {
            mesh.AddTriangle(inner + current, outer + current, outer + next);
            mesh.AddTriangle(inner + current, outer + next, inner + next);
        }
    }
}
