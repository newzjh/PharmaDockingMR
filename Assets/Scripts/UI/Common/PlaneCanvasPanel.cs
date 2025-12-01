using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class PlaneCanvasPanel : BasePanelEx<PlaneCanvasPanel>
{

    private void Start()
    {
        Invoke("RefreshPosition", 0.1f);
    }

    public override void OnShow()
    {
        base.OnShow();

        this.GetComponent<Canvas>().worldCamera = Camera.main;

        Invoke("RefreshPosition", 0.1f);
    }

    public override void OnHide()
    {
        base.OnHide();

        Transform tparent = this.transform;
        for (int i = 0; i < tparent.childCount; i++)
        {
            Transform t = tparent.GetChild(i);
            IRefreshable panel = t.GetComponent<IRefreshable>();
            if (panel != null)
                panel.SetVisible(false);
            else
                t.gameObject.SetActive(false);
        }
    }
    private void RefreshPosition()
    {
        BaseUIProperty.Update();
        //this.transform.position = BaseUIProperty.BasePos;
        //this.transform.forward = BaseUIProperty.BaseForward;
    }
}
