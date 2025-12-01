using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnifiedInput;

namespace RobotUI
{
    public class WelcomePanel : BasePanelEx<WelcomePanel>
    {

        public override void Awake()
        {
            base.Awake();

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

            OnSelect(0);
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
}