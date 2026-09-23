using UnityEngine;
namespace Vertigo.Fortune.Presentation
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeArea : MonoBehaviour
    {
        Rect previous; Vector2 previousSize; float previousScale;
        void LateUpdate()
        {
            var area=Screen.safeArea;var size=new Vector2(Screen.width,Screen.height);
            var canvas=GetComponentInParent<Canvas>().rootCanvas;float canvasScale=canvas.scaleFactor;
            if(area==previous && size==previousSize && Mathf.Approximately(canvasScale,previousScale))return;
            previous=area;previousSize=size;previousScale=canvasScale;var r=(RectTransform)transform;
            r.anchorMin=area.position/size;r.anchorMax=(area.position+area.size)/size;r.offsetMin=r.offsetMax=Vector2.zero;
            // Keep the authored stage inside both the reference rectangle and the hardware safe area.
            var stage=transform.Find("ui_stage");if(stage!=null){var available=area.size / canvasScale;float scale=Mathf.Min(1,Mathf.Min(available.x/1600,available.y/900));stage.localScale=Vector3.one*scale;}
        }
    }
}
