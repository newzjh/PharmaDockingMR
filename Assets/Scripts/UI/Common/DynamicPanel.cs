using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnifiedInput;
using Mirror;

public class DynamicPanel : BasePanelEx<DynamicPanel>
{

    private GameObject DynamicMenuButton;

    public void Start()
    {
        DynamicMenuButton = this.transform.Find("DynamicMenuButton").gameObject;

        SetAllVis(true);
    }

    public override void OnClick(GameObject sender)
    {
        string buttonname = sender.name;
        if (buttonname == "lock")
            buttonname = sender.transform.parent.name;

        if (buttonname == "DynamicMenuButton")
        {
            SwitchAllVis();
        }
    }

    public void SetAllVis(bool vis)
    {
        CmdSetAllVis(vis);
    }

    [Command(requiresAuthority = false)]
    public void CmdSetAllVis(bool vis)
    {
        RpcSetAllVis(vis);
    }

    [ClientRpc]
    public void RpcSetAllVis(bool vis)
    {
        CruveCanvasPanel.Instance.SetVisible(vis);
        PlaneCanvasPanel.Instance.SetVisible(vis);
    }

    public bool GetAllVis()
    {
        bool vis = false;
        if (CruveCanvasPanel.Instance.GetVisible())
        {
            Transform tparent = CruveCanvasPanel.Instance.transform;
            for (int i = 0; i < tparent.childCount; i++)
            {
                Transform t = tparent.GetChild(i);
                if (t.gameObject.activeSelf)
                    vis = true;
            }
        }
        if (PlaneCanvasPanel.Instance.GetVisible())
        {
            Transform tparent = PlaneCanvasPanel.Instance.transform;
            for (int i = 0; i < tparent.childCount; i++)
            {
                Transform t = tparent.GetChild(i);
                if (t.gameObject.activeSelf)
                    vis = true;
            }
        }
        return vis;
    }

    public void SwitchAllVis()
    {
        bool vis = GetAllVis();
        SetAllVis(!vis);
    }



    // Update is called once per frame
    void Update()
    {
        BaseUIProperty.Update();
        //DynamicMenuButton.transform.position = BaseUIProperty.BasePos + BaseUIProperty.BaseUp * 0.9f;
        //DynamicMenuButton.transform.forward = BaseUIProperty.BaseForward;

        if (UnifiedInputManager.GetOperation(OperationCode.Menu))
        {
            SwitchAllVis();
        }


    }



}
