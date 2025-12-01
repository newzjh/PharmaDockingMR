using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnifiedInput;
using MoleculeLogic;

namespace RobotUI
{

    public class DrugPanel : BasePanelEx<DrugPanel>
    {

        public Text TitleName;
        public Text TitleUsefor;
        public Text TitleNotUse;
        public Text TitleHowToUse;
        public Text TitleSideEffects;
        public Text TextUsefor;
        public Text TextNotUse;
        public Text TextHowToUse;
        public Text TextSideEffects;


        public override void Awake()
        {
            base.Awake();

        }

        public void OnLoadedMol(Molecule mol)
        {
            MoleculeController mc = mol.GetComponentInParent<MoleculeController>();
            if (mc)
                mc.SetNameTitleVisible(false);
        }

        public void Show(string drugname)
        {
            if (TitleName)
                TitleName.text = drugname;
            if (TitleUsefor)
                TitleUsefor.text = "" + drugname + " is used for:";
            if (TitleNotUse)
                TitleNotUse.text = "Do NOT use " + drugname + " if:";
            if (TitleHowToUse)
                TitleHowToUse.text = "How to use " + drugname + " :";
            if (TitleSideEffects)
                TitleSideEffects.text = "Possibile side effects of " + drugname + " :";

            //public Text TextUsefor;
            //public Text TextNotUse;
            //public Text TextHowToUse;
            //public Text TextSideEffects;

            SetVisible(true);

            Molecule.OnLoaded -= OnLoadedMol;
            Molecule.OnLoaded += OnLoadedMol;
        }

        public void Hide()
        {
            Molecule.OnLoaded -= OnLoadedMol;

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
            else
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

}