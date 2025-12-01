using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.UI;
using MoleculeLogic;
using UnifiedInput;
using UnityEngine.SceneManagement;

namespace MoleculeUI
{

    public class PlaneMainPanel : BasePanelEx<PlaneMainPanel>
    {


        public override void OnClick(GameObject sender)
        {
            string buttonname = sender.name;
            if (buttonname == "lock")
                buttonname = sender.transform.parent.name;

            if (buttonname == "MolSwitchButton")
            {
                //CruveMainPanel.Instance.SwitchVisible();
                CruveCanvasPanel.Instance.SwitchVisible();
                //UIManager.SwitchPanelsByType<CruveMainPanel>();
            }
            else if (buttonname == "FileSwitchButton")
            {
                FileBrowserPanelEx.Instance.SwitchVisible();
                //UIManager.SwitchPanelsByType<FileBrowserPanelEx>();
            }
            else if (buttonname == "BackButton")
            {
                SceneManager.LoadScene("LoginScene");
            }
        }

        private void Update()
        {
            if (UnifiedInputManager.GetOperation(OperationCode.Escape))
            {
                SceneManager.LoadScene("LoginScene");
            }
        }

    }
}