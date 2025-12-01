using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnifiedInput;



    public class SettingPanel : BasePanelEx<SettingPanel>
    {
        private Text titletext;
        private Text msgtext;


        public override void Awake()
        {
            base.Awake();


            Transform tContentPanel = this.transform.Find("ContentPanel");
            if (tContentPanel != null)
                msgtext = tContentPanel.GetComponentInChildren<Text>();

            Transform tTitleText = this.transform.Find("TitleText");
            if (tTitleText != null)
                titletext = tTitleText.GetComponent<Text>();

        }

 

        public void Show()
        {
            SetVisible(true);
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
            else if (sender.name.StartsWith("Button_Setting0"))
            {
                if (SceneController.Instance)
                    SceneController.Instance.SetStyle(SceneScheme.HouseScene);
            }
            else if (sender.name.StartsWith("Button_Setting1"))
            {
                if (SceneController.Instance)
                    SceneController.Instance.SetStyle(SceneScheme.OfficeScene);
            }
            else if (sender.name.StartsWith("Button_Setting2"))
            {
                if (SceneController.Instance)
                    SceneController.Instance.SetStyle(SceneScheme.ShowroomScene);
            }
            else if (sender.name.StartsWith("Button_Setting3"))
            {
                if (SceneController.Instance)
                    SceneController.Instance.SetStyle(SceneScheme.ArScene);
            }
            else if (sender.name.StartsWith("Button_Setting96"))
            {
                if (ConsolePanel.Instance)
                    ConsolePanel.Instance.SwitchVisible();
            }
            else if (sender.name.StartsWith("Button_Setting97"))
            {
                SceneController.Reload();
            }
            else if (sender.name.StartsWith("Button_Setting98"))
            {
                SceneController.LoadScene("LoginScene");
            }
            else if (sender.name.StartsWith("Button_Setting99"))
            {
                Application.Quit();
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
