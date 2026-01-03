using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnifiedInput;
using Mirror;
using MoleculeUI;

public class DynamicPanel : BasePanelEx<DynamicPanel>
{

    //private GameObject DynamicMenuButton;

    public void Start()
    {
        //DynamicMenuButton = this.transform.Find("DynamicMenuButton").gameObject;

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

    public void SetLeftVis(bool vis)
    {
        CmdSetLeftVis(vis);
    }

    [Command(requiresAuthority = false)]
    public void CmdSetLeftVis(bool vis)
    {
        RpcSetLeftVis(vis);
    }

    [ClientRpc]
    public void RpcSetLeftVis(bool vis)
    {
        BigPanel.Instance.SetVisible(vis);
    }

    public void SetRightVis(bool vis)
    {
        CmdSetRightVis(vis);
    }

    [Command(requiresAuthority = false)]
    public void CmdSetRightVis(bool vis)
    {
        RpcSetRightVis(vis);
    }

    [ClientRpc]
    public void RpcSetRightVis(bool vis)
    {
        RightPanel.Instance.SetVisible(vis);
    }

    public void SetZINCVis(bool vis)
    {
        CmdSetZINCVis(vis);
    }

    [Command(requiresAuthority = false)]
    public void CmdSetZINCVis(bool vis)
    {
        RpcSetZINCVis(vis);
    }

    [ClientRpc]
    public void RpcSetZINCVis(bool vis)
    {
        ZINCPanel.Instance.SetVisible(vis);
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

    public void OnButtonAll()
    {
        SwitchAllVis();
    }

    public void OnButtonLeft()
    {
        SetLeftVis(!BigPanel.Instance.GetVisible());
    }

    public void OnButtonRight()
    {
        SetRightVis(!RightPanel.Instance.GetVisible());
    }

    public void OnButtonZINC()
    {
        SetZINCVis(!ZINCPanel.Instance.GetVisible());
    }

    public void OnButtonExit()
    {
        Application.Quit();
    }

}
