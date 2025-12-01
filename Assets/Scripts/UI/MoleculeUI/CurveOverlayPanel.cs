using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.UI;
using MoleculeLogic;

namespace MoleculeUI
{
    public class CurveOverlayPanel : BasePanelEx<CurveOverlayPanel>
    {
        public override void OnClick(GameObject sender)
        {
            if (sender.name == "BackButton")
            {
                SceneController.LoadScene("LoginScene");
                //Application.Quit();
            }
            else if (sender.name == "ResetButton")
            {
                SceneController.LoadScene("MainScene");
                //Application.Quit();
            }
            else if (sender.name == "ConsoleButton")
            {
                if (ConsolePanel.Instance)
                {
                    ConsolePanel.Instance.SwitchVisible();
                }
            }
        }

    }
}