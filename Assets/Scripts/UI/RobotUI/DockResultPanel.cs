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

    public class DockResultPanel : BasePanelEx<DockResultPanel>
    {
        public class FileItem : MonoBehaviour
        {
            public int index;
        }

        private MoleculeFactory mf;

        GameObject fileitemprefab;

        public Text TitleName;

        public override void Awake()
        {
            base.Awake();

            GameObject go = GameObject.Find("MoleculeFactory");
            if (go != null)
            {
                mf = go.GetComponent<MoleculeFactory>();
            }

        }



        private GameObject itemprefab;

        private Molecule curmol;

        public void BuildContent(Molecule mol)
        {
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

            if (mol == null)
                return;

            for (int i = 0; i < mol.partCunnt; i++)
            {
                GameObject itemgo = GameObject.Instantiate<GameObject>(itemprefab);
                itemgo.transform.SetParent(glg.transform, false);
                Text text = itemgo.GetComponentInChildren<Text>();
                if (text != null)
                {
                    if (i == 0)
                        text.text = "Trunk";
                    else
                        text.text = "Model_" + i.ToString();
                }
                Toggle tog = itemgo.GetComponent<Toggle>();
                if (tog)
                {
                    tog.isOn = mol.scheme.showparts[i];
                    UnityAction<bool> uaction = delegate(bool isOn) { this.OnToggleClick(tog, isOn); };
                    tog.onValueChanged.AddListener(uaction);
                }
            }

        }

        public void OnLoadedMol(Molecule mol)
        {
            BuildContent(mol);
        }

        public void Show()
        {
            SetVisible(true);
            curmol = null;
            Molecule.OnLoaded -= OnLoadedMol;
            Molecule.OnLoaded += OnLoadedMol;
        }

        public void Hide()
        {
            Molecule.OnLoaded -= OnLoadedMol;
            curmol = null;
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
            curmol.scheme.showparts.Clear();
            foreach (Toggle tog in togs)
            {
                curmol.scheme.showparts.Add(tog.isOn);
            }

            curmol.Represent();
        }
    }

}