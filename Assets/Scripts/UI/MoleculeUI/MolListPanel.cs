using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.UI;
using MoleculeLogic;


namespace MoleculeUI
{
    public class MolListPanel : BasePanelEx<MolListPanel>
    {

        private MoleculeFactory mf;


        public override void Awake()
        {
            base.Awake();
            GameObject go = GameObject.Find("MoleculeFactory");
            if (go != null)
            {
                mf = go.GetComponent<MoleculeFactory>();
            }

        }



        public override void OnClick(GameObject sender)
        {
            if (sender.name.StartsWith("MolList"))
            {
                string molname = sender.name;
                int index = molname.LastIndexOf('_');
                if (index <= 0)
                    return;
                index += 1;

                molname = molname.Substring(index, molname.Length - index);
                Transform tChild = MoleculeFactory.Instance.transform.Find(molname);
                if (tChild == null)
                    return;

                MoleculeController mc = tChild.GetComponentInChildren<MoleculeController>();
                if (mc == null)
                    return;

                SelectPanel.Instance.curlockmc = mc;
                DynamicPanel.Instance.SetAllVis(false);
            }


        }


        int lastchildcount = -1;
        public void Update()
        {
            int curchildcount = mf.transform.childCount;
            if (curchildcount != lastchildcount)
            {
                Button[] buttons = this.GetComponentsInChildren<Button>();

                for (int i = 0; i < 5; i++)
                {
                    GameObject item = buttons[i].gameObject;

                    if (i < curchildcount)
                    {
                        Transform tCop = mf.transform.GetChild(i);
                        item.name = "MolList_" + i + "_" + tCop.name;
                        Text[] texts = item.GetComponentsInChildren<Text>();
                        texts[0].text = tCop.name;
                        texts[1].text = "";
                        texts[2].text = "";
                    }
                    else
                    {
                        item.name = "MolList_" + i + "_Unused";
                        Text[] texts = item.GetComponentsInChildren<Text>();
                        texts[0].text = "";
                        texts[1].text = "";
                        texts[2].text = "";
                    }
                }



                lastchildcount = curchildcount;


            }


        }

    }
}