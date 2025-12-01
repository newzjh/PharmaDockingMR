using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class DynamicCanvasPanel : BasePanelEx<DynamicCanvasPanel>
{
    public void Start()
    {
        this.GetComponent<Canvas>().worldCamera = Camera.main;
        //var tdgr = this.GetComponent<TrackedDeviceGraphicRaycaster>();
        //if (tdgr == null)
        //    tdgr = this.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
    }


}
