using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnifiedInput;

public class ChatPanel : BasePanelEx<ChatPanel>
{
    private Text titletext;
    private Text msgtext;



    private Button[] buttons;

    public override void Awake()
    {
        base.Awake();

        buttons = this.GetComponentsInChildren<Button>(true);

        Transform tContentPanel = this.transform.Find("ContentPanel");
        if (tContentPanel != null)
            msgtext = tContentPanel.GetComponentInChildren<Text>();

        Transform tTitleText = this.transform.Find("TitleText");
        if (tTitleText != null)
            titletext = tTitleText.GetComponent<Text>();

    }

 

    public void Show(string title, string msg, string[] opitions)
    {
        SetVisible(true);

        if (titletext != null)
            titletext.text = title;

        if (msgtext != null)
            msgtext.text = msg;

        if (buttons != null && opitions != null)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                Button btn = buttons[i];
                if (btn)
                {
                    if (i < opitions.Length)
                    {
                        Text selecttext = btn.GetComponentInChildren<Text>();
                        if (selecttext)
                            selecttext.text = opitions[i];
                        btn.gameObject.SetActive(true);
                    }
                    else
                    {
                        btn.gameObject.SetActive(false);
                    }
                }
            }
        }


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
        else if (sender.name.StartsWith("Button_1"))
        {
            OnSelect(1);
        }
        else if (sender.name.StartsWith("Button_2"))
        {
            OnSelect(2);
        }
        else if (sender.name.StartsWith("Button_3"))
        {
            OnSelect(3);
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
