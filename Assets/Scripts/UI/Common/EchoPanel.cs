using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnifiedInput;

public class EchoPanel : BasePanelEx<EchoPanel>
{
    private Text msgtext;


    public override void Awake()
    {
        base.Awake();

        Transform tContentPanel = this.transform.Find("ContentPanel");
        if (tContentPanel != null)
            msgtext = tContentPanel.GetComponentInChildren<Text>();

    }

 

    public void Show(string msg)
    {
        SetVisible(true);

        if (msgtext != null)
            msgtext.text = msg;

        Invoke("Hide", 2.0f);
    }

    public void Hide()
    {
        SetVisible(false);
    }

    public override void OnClick(GameObject sender)
    {
        base.OnClick(sender);
        if (sender.name.StartsWith("Button_0"))
        {
            OnSelect(0);
        }
    }

    public delegate void SelectEventHandler(int index);
    public SelectEventHandler Selected = null;

    private void OnSelect(int index)
    {
        Hide();

        if (Selected != null)
            Selected(index);
    }


}
