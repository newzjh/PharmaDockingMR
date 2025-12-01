using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.UI;
using MoleculeLogic;

namespace LoginUI
{

    public class LoginMenuPanel : BasePanelEx<LoginMenuPanel>
    {
        public void Start()
        {
            Refresh();
        }

        public override void OnShow()
        {
            base.OnShow();

            Refresh();
        }

        private void Refresh()
        {
            Camera cam = Camera.main;
            if (!cam)
                return;

            GameObject go = GameObject.Find("Playground Manager/Turbulent Logo");
            if (go == null)
                return;

            go.transform.position = this.transform.position + this.transform.forward * 0.4f +
                this.transform.right * 0.2f + this.transform.up * (-1.7f);
            go.transform.eulerAngles = this.transform.eulerAngles;
        }

        public override void OnClick(GameObject sender)
        {
            if (sender.name == "LoginMenuButton0")
            {
                SceneController.LoadScene("MainScene");
            }
            else if (sender.name == "LoginMenuButton1")
            {
                SceneController.LoadScene("human_abdomen");
            }
            else if (sender.name == "LoginMenuButton2")
            {
                SceneController.LoadScene("DrawScene");
            }
            else if (sender.name == "LoginMenuButton3")
            {
                SceneController.LoadScene("RobotScene");
            }
            else if (sender.name == "LoginMenuButton4")
            {
                SceneController.LoadScene("HeartScene");
            }
        }

        private void LoadMainScene()
        {
            SceneController.LoadScene("MainScene");
        }

    }

}