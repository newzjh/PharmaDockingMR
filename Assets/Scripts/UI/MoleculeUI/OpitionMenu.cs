

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using MoleculeLogic;
using UnifiedInput;

namespace MoleculeUI
{
    public class OpitionMenu : BasePanelEx<OpitionMenu>
    {
        public override void OnShow()
        {
            base.OnShow();

            Toggle[] togs = this.GetComponentsInChildren<Toggle>(true);
            if (togs != null)
            {
                foreach (Toggle tog in togs)
                {
                    if (tog.name == "Toggle2" || tog.name == "Toggle3" ||
                        tog.name == "Toggle4" || tog.name == "Toggle5")
                    {
                        tog.isOn = false;
                    }
                }
            }

            PocketPanel.Instance.SetVisible(false);
            PharmaPanel.Instance.SetVisible(false);
            AtomPanel.Instance.SetVisible(false);
            CompondPanel.Instance.SetVisible(false);
        }

        public override void OnHide()
        {
            base.OnHide();

            Toggle[] togs = this.GetComponentsInChildren<Toggle>(true);
            if (togs != null)
            {
                foreach (Toggle tog in togs)
                {
                    if (tog.name == "Toggle2" || tog.name == "Toggle3" || 
                        tog.name == "Toggle4" || tog.name == "Toggle5")
                    {
                        tog.isOn = false;
                    }
                }
            }

            PocketPanel.Instance.SetVisible(false);
            PharmaPanel.Instance.SetVisible(false);
            AtomPanel.Instance.SetVisible(false);
            CompondPanel.Instance.SetVisible(false);
        }
    }

}