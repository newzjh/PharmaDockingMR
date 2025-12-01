using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnifiedInput;

public class SystemMenu : BasePanelEx<SystemMenu>
{

    private GameObject DynamicMenuButton;
    private Camera cam;

    public void Start()
    {
        DynamicMenuButton = this.transform.Find("SystemMenuButton").gameObject;
        cam = Camera.main;
    }

    public void ShowSetting()
    {
        if (!SettingPanel.Instance)
            return;

        Vector3 eyepos = this.transform.position;

        Transform tPanel = SettingPanel.Instance.transform;
        Vector3 viewdir = cam.transform.forward;
        viewdir.y = 0.0f;
        viewdir = viewdir.normalized;

        tPanel.transform.position = eyepos - viewdir * 0.1f + new Vector3(0,0.9f,0);
        tPanel.transform.forward = viewdir;
        Vector3 langle = tPanel.transform.localEulerAngles;
        langle.y -= 15.0f;
        tPanel.transform.localEulerAngles = langle;

        SettingPanel.Instance.Show();
    }

    public override void OnClick(GameObject sender)
    {
        string buttonname = sender.name;
        if (buttonname == "lock")
            buttonname = sender.transform.parent.name;

        if (buttonname == "SystemMenuButton")
        {
            ShowSetting();
        }
    }





    // Update is called once per frame
    void Update()
    {
        //if (UnifiedInputManager.GetOperation(OperationCode.Menu))
        //{
        //}
    }



}
