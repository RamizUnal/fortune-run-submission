using UnityEngine;
using UnityEngine.UI;

namespace Vertigo.Fortune.Presentation
{
    /// <summary>A quiet, machined metal face for the wheel's central spindle.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WheelHubGraphic : MaskableGraphic
    {
        const int Segments = 96;
        static readonly Vector2 LightDirection = new Vector2(-.45f, .89f);
        static readonly Color Face = new Color32(42, 48, 51, 255);
        static readonly Color Recess = new Color32(19, 24, 27, 255);

        [SerializeField] Color finish = new Color32(160, 132, 87, 255);

        public void SetFinish(Color value)
        {
            finish = value;
            SetVerticesDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
            maskable = false;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect bounds = rectTransform.rect;
            float radius = Mathf.Min(bounds.width, bounds.height) * .5f;
            if (radius <= 0) return;

            Vector2 center = bounds.center;
            float feather = .65f / Mathf.Max(1, canvas != null ? canvas.scaleFactor : 1);
            radius = Mathf.Max(0, radius - feather);

            // The narrow turned edge catches the light; the broad face stays matte.
            AddRing(mesh, center, radius * .92f, radius, Shade(finish, .43f), Shade(finish, .7f), .24f);
            AddRing(mesh, center, radius * .875f, radius * .92f, Recess, Shade(finish, .57f), .10f);
            AddRing(mesh, center, radius * .84f, radius * .875f, Shade(Face, 1.28f), Shade(Face, .72f), .09f);
            AddFace(mesh, center, radius * .84f);

            // A single inset lip defines the spindle without an icon or lettering.
            AddRing(mesh, center, radius * .20f, radius * .23f, Shade(Face, .72f), Shade(Face, 1.13f), .07f);
            AddRing(mesh, center, 0, radius * .20f, Shade(Face, .83f), Shade(Face, .88f), .025f);

            Color edge = Shade(finish, .7f);
            Color transparent = edge;
            transparent.a = 0;
            AddRing(mesh, center, radius, radius + feather, edge, transparent, .24f);
        }

        void AddFace(VertexHelper mesh, Vector2 center, float radius)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(center, Tint(Shade(Face, 1.035f)), Vector2.zero);
            for (int i = 0; i < Segments; i++)
            {
                Vector2 direction = Direction(i);
                float light = Vector2.Dot(direction, LightDirection);
                Color shade = Shade(Face, 1 + light * .16f);
                shade.a = 1;
                mesh.AddVert(center + direction * radius, Tint(shade), Vector2.zero);
            }
            for (int i = 0; i < Segments; i++)
                mesh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % Segments);
        }

        void AddRing(VertexHelper mesh, Vector2 center, float innerRadius, float outerRadius,
            Color innerColor, Color outerColor, float illumination)
        {
            int start = mesh.currentVertCount;
            for (int i = 0; i < Segments; i++)
            {
                Vector2 direction = Direction(i);
                float light = Vector2.Dot(direction, LightDirection) * illumination;
                mesh.AddVert(center + direction * innerRadius, Tint(Lit(innerColor, light)), Vector2.zero);
                mesh.AddVert(center + direction * outerRadius, Tint(Lit(outerColor, light)), Vector2.zero);
            }
            for (int i = 0; i < Segments; i++)
            {
                int current = start + i * 2;
                int next = start + ((i + 1) % Segments) * 2;
                mesh.AddTriangle(current, current + 1, next + 1);
                mesh.AddTriangle(current, next + 1, next);
            }
        }

        static Color Shade(Color source, float brightness)
        {
            return new Color(source.r * brightness, source.g * brightness, source.b * brightness, source.a);
        }

        static Color Lit(Color source, float light)
        {
            return new Color(source.r + light, source.g + light, source.b + light, source.a);
        }

        Color Tint(Color source)
        {
            return new Color(source.r * color.r, source.g * color.g, source.b * color.b, source.a * color.a);
        }

        static Vector2 Direction(int index)
        {
            float angle = index * Mathf.PI * 2 / Segments;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
    }
}
