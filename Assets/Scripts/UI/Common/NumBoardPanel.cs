using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.UI;
using MoleculeLogic;


public class NumBoardPanel : BasePanelEx<NumBoardPanel>
{


    public InputField inputfield;

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
        else if (sender.name == "Minus")
        {
            if (inputfield)
            {
                inputfield.text += "-";
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
            this.gameObject.SetActive(false);
        }
    }


	
}
