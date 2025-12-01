using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.UI;
using MoleculeLogic;


public class KeyboardPanelEx : BasePanelEx<KeyboardPanelEx>
{


    public InputField inputfield;
    public bool Cap = true;
    public delegate void EventHandler(string pdbStr);
    public event EventHandler onLoadingPDB;

    public override void Awake()
    {
        base.Awake();
     
    }

    public override void OnClick(GameObject sender)
    {
        if (sender == null) 
            return;

        if (sender.name.Length == 1)
        {
            if (inputfield)
            {
                inputfield.text += sender.name;
            }
        }
        else if (sender.name == "Dot")
        {
            if (inputfield)
            {
                inputfield.text += ".";
            }
        }
        else if (sender.name == "Backspace")
        {
            if (inputfield && inputfield.text.Length > 0)
            {
                inputfield.text = inputfield.text.Substring(0, inputfield.text.Length - 1);
            }
        }
        else if (sender.name == "Cancel")
        {
            //if (text)
            //{
            //    text.text = "";
            //}
            this.SetVisible(false);
        }
        else if (sender.name == "Load")
        {
            if (inputfield && inputfield.text != "" && onLoadingPDB != null)
            {
                onLoadingPDB(inputfield.text);
            }
        }
        else if (sender.name == "Caps")
        {
            Cap = !Cap;
            if (Cap)
                inputfield.text = inputfield.text.ToUpper();
            else
                inputfield.text = inputfield.text.ToLower();
            //Transform parent = sender.transform.parent;
            //if (parent)
            //{
            //    for (int i = 0; i < parent.childCount; i++)
            //    {
            //        Transform child = parent.GetChild(i);
            //        if (child.name.Length == 1)
            //        {
            //            string name = child.name;
            //            if (Cap)
            //            {
            //                name = name.ToUpper();
            //            }
            //            else
            //            {
            //                name = name.ToLower();
            //            }
            //            child.name = name;
            //            Transform textTransform = child.FindChild("Text");
            //            if (textTransform)
            //            {
            //                Text text = textTransform.GetComponent<Text>();
            //                if (text != null)
            //                {
            //                    text.text = name;
            //                }
            //            }
            //        }
            //    }
            //}
        }
    }


	
}
