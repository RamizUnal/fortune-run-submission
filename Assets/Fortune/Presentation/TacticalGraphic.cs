using UnityEngine;
using UnityEngine.UI;

namespace Vertigo.Fortune.Presentation
{
    /// <summary>Resolution independent decoration; no raycasts or extra texture allocations.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TacticalGraphic : MaskableGraphic
    {
        public bool Orbit;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect;
            if (Orbit)
            {
                float radius = Mathf.Min(r.width, r.height) * .475f;
                for (int i = 0; i < 120; i++)
                {
                    float a = i * Mathf.PI * 2 / 120;
                    var dir = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                    Line(vh, r.center + dir * radius, r.center + dir * (radius - (i % 5 == 0 ? 11 : 4)), i % 5 == 0 ? 1.7f : 1, new Color(.38f,.72f,.78f,i % 5 == 0 ?.35f:.16f));
                }
                return;
            }
            Quad(vh, new Vector2(r.xMin,r.yMin),new Vector2(r.xMax,r.yMax),new Color32(6,14,25,255),new Color32(18,40,54,255));
            for (float x = r.xMin; x < r.xMax; x += 76) Line(vh,new Vector2(x,r.yMin),new Vector2(x+r.height*.5f,r.yMax),1,new Color(.33f,.59f,.65f,.035f));
            for (float y = r.yMin; y < r.yMax; y += 76) Line(vh,new Vector2(r.xMin,y),new Vector2(r.xMax,y),1,new Color(.33f,.59f,.65f,.025f));
        }
        static void Quad(VertexHelper v, Vector2 min, Vector2 max, Color bottom, Color top)
        {
            int i=v.currentVertCount;v.AddVert(new Vector3(min.x,min.y),bottom,Vector2.zero);v.AddVert(new Vector3(min.x,max.y),top,Vector2.zero);v.AddVert(new Vector3(max.x,max.y),top,Vector2.zero);v.AddVert(new Vector3(max.x,min.y),bottom,Vector2.zero);v.AddTriangle(i,i+1,i+2);v.AddTriangle(i,i+2,i+3);
        }
        static void Line(VertexHelper v,Vector2 a,Vector2 b,float width,Color c)
        {
            Vector2 d=(b-a).normalized;Vector2 n=new Vector2(-d.y,d.x)*width*.5f;int i=v.currentVertCount;
            v.AddVert(a-n,c,Vector2.zero);v.AddVert(a+n,c,Vector2.zero);v.AddVert(b+n,c,Vector2.zero);v.AddVert(b-n,c,Vector2.zero);v.AddTriangle(i,i+1,i+2);v.AddTriangle(i,i+2,i+3);
        }
    }
}
