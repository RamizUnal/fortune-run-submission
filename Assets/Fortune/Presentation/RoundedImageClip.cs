using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Vertigo.Fortune.Presentation
{
    /// <summary>Clips an Image's existing sprite mesh without a stencil mask or extra draw call.</summary>
    [RequireComponent(typeof(Image))]
    public sealed class RoundedImageClip : BaseMeshEffect
    {
        const int SegmentsPerCorner = 8;
        [SerializeField, Min(0)] float radius = 8f;
        readonly List<UIVertex> source = new List<UIVertex>(54);
        readonly List<UIVertex> first = new List<UIVertex>(48);
        readonly List<UIVertex> second = new List<UIVertex>(48);
        readonly List<Vector2> contour = new List<Vector2>(36);

        public float Radius
        {
            get => radius;
            set { radius = Mathf.Max(0, value); if (graphic != null) graphic.SetVerticesDirty(); }
        }

        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive() || radius <= 0) return;
            BuildContour(graphic.rectTransform.rect);
            source.Clear(); mesh.GetUIVertexStream(source); mesh.Clear();
            for (int triangle = 0; triangle < source.Count; triangle += 3)
            {
                first.Clear(); first.Add(source[triangle]); first.Add(source[triangle + 1]); first.Add(source[triangle + 2]);
                var input = first; var output = second;
                for (int edge = 0; edge < contour.Count && input.Count > 0; edge++)
                {
                    Clip(input, output, contour[edge], contour[(edge + 1) % contour.Count]);
                    var swap = input; input = output; output = swap;
                }
                int start = mesh.currentVertCount;
                foreach (var vertex in input) mesh.AddVert(vertex);
                for (int i = 1; i + 1 < input.Count; i++) mesh.AddTriangle(start, start + i, start + i + 1);
            }
        }

        void BuildContour(Rect bounds)
        {
            contour.Clear();
            float r = Mathf.Min(radius, Mathf.Min(bounds.width, bounds.height) * .5f);
            for (int corner = 0; corner < 4; corner++)
            {
                var center = new Vector2(corner < 2 ? bounds.xMax - r : bounds.xMin + r,
                    corner == 1 || corner == 2 ? bounds.yMax - r : bounds.yMin + r);
                for (int segment = 0; segment <= SegmentsPerCorner; segment++)
                {
                    float angle = (-90 + corner * 90 + segment * 90f / SegmentsPerCorner) * Mathf.Deg2Rad;
                    contour.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r);
                }
            }
        }

        static void Clip(List<UIVertex> input, List<UIVertex> output, Vector2 a, Vector2 b)
        {
            output.Clear();
            var previous = input[input.Count - 1];
            float previousDistance = Distance(a, b, previous.position);
            foreach (var current in input)
            {
                float currentDistance = Distance(a, b, current.position);
                bool previousInside = previousDistance >= -.0001f, currentInside = currentDistance >= -.0001f;
                if (previousInside != currentInside)
                    output.Add(Interpolate(previous, current, previousDistance / (previousDistance - currentDistance)));
                if (currentInside) output.Add(current);
                previous = current; previousDistance = currentDistance;
            }
        }

        static float Distance(Vector2 a, Vector2 b, Vector3 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);

        static UIVertex Interpolate(UIVertex a, UIVertex b, float t)
        {
            var result = a;
            result.position = Vector3.LerpUnclamped(a.position, b.position, t);
            result.color = Color.LerpUnclamped(a.color, b.color, t);
            result.normal = Vector3.LerpUnclamped(a.normal, b.normal, t);
            result.tangent = Vector4.LerpUnclamped(a.tangent, b.tangent, t);
            result.uv0 = Vector4.LerpUnclamped(a.uv0, b.uv0, t);
            result.uv1 = Vector4.LerpUnclamped(a.uv1, b.uv1, t);
            result.uv2 = Vector4.LerpUnclamped(a.uv2, b.uv2, t);
            result.uv3 = Vector4.LerpUnclamped(a.uv3, b.uv3, t);
            return result;
        }
    }
}
