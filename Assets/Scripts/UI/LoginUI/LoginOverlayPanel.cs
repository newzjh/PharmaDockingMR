using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.UI;
using MoleculeLogic;
using UnityEngine.SceneManagement;

namespace LoginUI
{
    public class LoginOverlayPanel : BasePanelEx<LoginOverlayPanel>
    {
        public override void OnClick(GameObject sender)
        {
            if (sender.name == "ExitButton")
            {
                Application.Quit();
            }
        }

    }
}