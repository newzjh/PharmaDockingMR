using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.UI;
using UnifiedInput;
using MoleculeLogic;

namespace MoleculeUI
{
    public class PocketPanel : BasePanelEx<PocketPanel>
    {
        private GameObject itemprefab;

        private Molecule curmol;

        public void SetContent(Molecule mol)
        {
            if (SelectPanel.Instance.curlockmc.mol != mol) //skip select change
                return;

            if (mol.pockets == null)
                return;

            curmol = mol;

            if (itemprefab == null)
                itemprefab = Resources.Load<GameObject>("UI/PocketPanelItem");

            GridLayoutGroup glg = this.GetComponentInChildren<GridLayoutGroup>();
            if (!glg)
                return;

            List<GameObject> deletelist = new List<GameObject>();
            for (int i = 0; i < glg.transform.childCount; i++)
            {
                Transform t = glg.transform.GetChild(i);
                deletelist.Add(t.gameObject);
            }
            for (int i = 0; i < deletelist.Count; i++)
            {
                DestroyImmediate(deletelist[i]);
            }

            for (int i = 0; i < curmol.pockets.Count; i++)
            {
                Pocket pocket = curmol.pockets[i];
                GameObject itemgo = GameObject.Instantiate<GameObject>(itemprefab);
                itemgo.transform.SetParent(glg.transform, false);
                Text text = itemgo.GetComponentInChildren<Text>();
                if (text != null)
                {
                    text.text = pocket.name;
                }
                Toggle tog = itemgo.GetComponent<Toggle>();
                if (tog)
                {
                    Transform tImage = tog.transform.Find("Image");
                    if (tImage != null)
                    {
                        Image timg = tImage.GetComponent<Image>();
                        if (timg)
                        {
                            timg.color = pocket.color;
                        }
                    }
                    tog.isOn = mol.scheme.showpockets[i];
                    UnityAction<bool> uaction = delegate(bool isOn) { this.OnToggleClick(tog, isOn); };
                    tog.onValueChanged.AddListener(uaction);
                }
            }

        }


        public override void OnClick(GameObject sender)
        {
            base.OnClick(sender);
            if (sender.name == "ButtonAll")
            {
                Toggle[] togs = this.GetComponentsInChildren<Toggle>(true);
                if (togs != null)
                {
                    foreach (Toggle tog in togs)
                    {
                        tog.isOn = true;
                    }
                }
            }
            else if (sender.name == "ButtonNone")
            {
                Toggle[] togs = this.GetComponentsInChildren<Toggle>(true);
                if (togs != null)
                {
                    foreach (Toggle tog in togs)
                    {
                        tog.isOn = false;
                    }
                }
            }
        }

        private bool dirty = false;

        void OnToggleClick(Toggle toggle, bool value)
        {
            dirty = true;
        }

        private void Update()
        {
            if (dirty)
            {
                Refersh();
                dirty = false;
            }
        }

        private void Refersh()
        {
            if (!curmol)
                return;

            Toggle[] togs = this.GetComponentsInChildren<Toggle>(true);
            curmol.scheme.showpockets.Clear();
            for (int i = 0; i < togs.Length; i++)
            {
                curmol.scheme.showpockets.Add(togs[i].isOn);
            }

            curmol.Represent();
        }

    }
}