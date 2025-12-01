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

    public class DockMatchPanel : BasePanelEx<DockMatchPanel>
    {
        public class FileItem : MonoBehaviour
        {
            public int index;
        }

        private MoleculeFactory mf;

        GameObject fileitemprefab;
        public string selectedReceptor;
        List<string> receptors = new List<string>();

        public Text TitleName;
        public Text TitleLigand;

        public override void Awake()
        {
            base.Awake();

            GameObject go = GameObject.Find("MoleculeFactory");
            if (go != null)
            {
                mf = go.GetComponent<MoleculeFactory>();
            }

            receptors.Add("A2A_4UHR");
            receptors.Add("Beta1_2Y00");
            receptors.Add("Beta2_2RH1");
            receptors.Add("CB1_5TGZ");
            receptors.Add("CB2_Model");
            receptors.Add("CXCR4_3ODU");
            receptors.Add("D3R_3PBL");
            receptors.Add("H1R_3RZE");
            receptors.Add("M2M_3UON");
            receptors.Add("mGlu1_4OR2");
            receptors.Add("Mu_4N6H");
            receptors.Add("Rhodopsin_1F88");
            receptors.Add("SIP_3V2W");
            receptors.Add("SMO_4JKV");

            LoadMatchTable();
        }



        void BuildListContent()
        {
            if (fileitemprefab == null) 
                fileitemprefab = Resources.Load<GameObject>("UI/FileItem");

            Transform tgrid = this.transform.Find("ReceptorPanel").Find("ScrollView").Find("Viewport").Find("Content");
            List<GameObject> deletelist = new List<GameObject>();
            for (int i = 0; i < tgrid.childCount; i++)
            {
                Transform t = tgrid.GetChild(i);
                deletelist.Add(t.gameObject);
            }
            for (int i = 0; i < deletelist.Count; i++)
            {
                DestroyImmediate(deletelist[i]);
            }

            for (int i = 0; i < receptors.Count; i++)
            {
                string file = receptors[i];
                GameObject fileitemgo = GameObject.Instantiate(fileitemprefab);
                fileitemgo.name = "filebutton_" + i.ToString();
                fileitemgo.transform.SetParent(tgrid, false);
                FileItem fi = fileitemgo.AddComponent<FileItem>();
                fi.index = i;
                Text text = fileitemgo.GetComponentInChildren<Text>();
                if (text != null)
                {
                    text.text = file;
                }
                Button b = fileitemgo.GetComponent<Button>();
                if (b)
                {
                    b.onClick.AddListener(delegate() { this.OnSelectReceptor(b); });
                }
            }
        }

        void OnSelectReceptor(Button b)
        {
            FileItem[] fis = this.GetComponentsInChildren<FileItem>();
            if (fis == null) return;

            foreach (FileItem fi in fis)
            {
                Image img = fi.GetComponent<Image>();
                if (!img)
                    continue;
                if (fi.gameObject == b.gameObject)
                {
                    img.color = new Color(255 / 255.0f, 235.0f / 255.0f, 55.0f / 255.0f, 1.0f); ;
                    selectedReceptor = receptors[fi.index];
                    LoadReceptor(selectedReceptor);
                }
                else
                {
                    img.color = new Color(154 / 255.0f, 240.0f / 255.0f, 179.0f / 255.0f, 1.0f); ;
                }
            }
        }

        void LoadReceptor(string receptor)
        {
            string zippath = "userdata/drugs/Memantine/" + receptor + ".pdb";
            if (mf)
            {
                List<GameObject> deletelist = new List<GameObject>();
                for (int i = 1; i < mf.transform.childCount; i++)
                {
                    Transform tChild = mf.transform.GetChild(i);
                    deletelist.Add(tChild.gameObject);
                }
                for (int i = 0; i < deletelist.Count; i++)
                    GameObject.DestroyImmediate(deletelist[i]);
                deletelist.Clear();

                mf.CreateFromZip(zippath);

            }
        }



        private MoleculeController srcmc;
        private MoleculeController destmc;

        public Dictionary<string, iDockConfItem> cfgItems = new Dictionary<string, iDockConfItem>();

        private void LoadMatchTable()
        {
            TextAsset ta = Resources.Load<TextAsset>("matchtable");
            StringReader sr = new StringReader(ta.text);
            while (true)
            {
                string sLine = sr.ReadLine();
                if (sLine == null)
                    break;

                string[] cells = sLine.Split(',');
                if (cells == null || cells.Length < 9)
                    break;

                string cmd = cells[0];

                if (cmd == "i")
                {
                    iDockConfItem cfgitem = new iDockConfItem();
                    cfgitem.receptor_path = cells[1];
                    cfgitem.ligand_path = cells[2];
                    float.TryParse(cells[3], out cfgitem.center.x);
                    float.TryParse(cells[4], out cfgitem.center.y);
                    float.TryParse(cells[5], out cfgitem.center.z);
                    float.TryParse(cells[6], out cfgitem.size.x);
                    float.TryParse(cells[7], out cfgitem.size.y);
                    float.TryParse(cells[8], out cfgitem.size.z);
                    string key = Path.GetFileNameWithoutExtension(cfgitem.receptor_path) + "&" + Path.GetFileNameWithoutExtension(cfgitem.ligand_path);
                    key = key.ToLower();
                    cfgItems[key] = cfgitem;
                }
            }
        }

        public void OnLoadedMol(Molecule mol)
        {
            MoleculeController[] mcs = mf.GetComponentsInChildren<MoleculeController>();
            if (mcs != null && mcs.Length >= 2)
            {
                srcmc = mcs[0];
                destmc = mcs[1];
                Refresh();
            }
        }

        private void Refresh()
        {
            string ligand_path = srcmc.cachefile;
            string receptor_path = destmc.cachefile;
            string key = Path.GetFileNameWithoutExtension(receptor_path) + "&" + Path.GetFileNameWithoutExtension(ligand_path);
            if (cfgItems.ContainsKey(key))
            {
                iDockConfItem cfgitem = cfgItems[key];

                InputField[] fields = this.GetComponentsInChildren<InputField>();
                foreach (InputField field in fields)
                {
                    if (field.name == "CenterXField")
                    {
                        field.text = cfgitem.center.x.ToString("F3");
                    }
                    else if (field.name == "CenterYField")
                    {
                        field.text = cfgitem.center.y.ToString("F3");
                    }
                    else if (field.name == "CenterZField")
                    {
                        field.text = cfgitem.center.z.ToString("F3");
                    }
                    else if (field.name == "SizeXField")
                    {
                        field.text = cfgitem.size.x.ToString("F3");
                    }
                    else if (field.name == "SizeYField")
                    {
                        field.text = cfgitem.size.y.ToString("F3");
                    }
                    else if (field.name == "SizeZField")
                    {
                        field.text = cfgitem.size.z.ToString("F3");
                    }
                }
            }

            iDock.Instance.ShowBound(destmc);

        }

        public iDockConfItem curConfItem;
        private void GenerateConfItem()
        {
            string ligand_path = srcmc.cachefile;
            string receptor_path = destmc.cachefile;

            iDockConfItem cfgitem = new iDockConfItem();
            cfgitem.center = Vector3.zero;
            cfgitem.size = new Vector3(20.0f, 20.0f, 20.0f);

            InputField[] fields = this.GetComponentsInChildren<InputField>();
            foreach (InputField field in fields)
            {
                if (field.name == "CenterXField")
                {
                    float.TryParse(field.GetComponentInChildren<Text>().text, out cfgitem.center.x);
                }
                else if (field.name == "CenterYField")
                {
                    float.TryParse(field.GetComponentInChildren<Text>().text, out cfgitem.center.y);
                }
                else if (field.name == "CenterZField")
                {
                    float.TryParse(field.GetComponentInChildren<Text>().text, out cfgitem.center.z);
                }
                else if (field.name == "SizeXField")
                {
                    float.TryParse(field.GetComponentInChildren<Text>().text, out cfgitem.size.x);
                }
                else if (field.name == "SizeYField")
                {
                    float.TryParse(field.GetComponentInChildren<Text>().text, out cfgitem.size.y);
                }
                else if (field.name == "SizeZField")
                {
                    float.TryParse(field.GetComponentInChildren<Text>().text, out cfgitem.size.z);
                }
            }

            cfgitem.receptor_path = receptor_path;
            cfgitem.ligand_path = ligand_path;

            curConfItem = cfgitem;
        }

        public void Show(string ligandname)
        {
            SetVisible(true);

            if (TitleLigand)
                TitleLigand.text = ligandname;

            BuildListContent();

            Molecule.OnLoaded -= OnLoadedMol;
            Molecule.OnLoaded += OnLoadedMol;
        }

        public void Hide()
        {
            Molecule.OnLoaded -= OnLoadedMol;

            SetVisible(false);

            iDock.Instance.HideBound();

            srcmc = null;
            destmc = null;

            if (NumBoardPanel.Instance)
                NumBoardPanel.Instance.SetVisible(false);
        }

        public override void OnClick(GameObject sender)
        {
            base.OnClick(sender);
            if (sender.name.StartsWith("Button_0"))
            {
                if (srcmc != null && destmc != null)
                    GenerateConfItem();
                else
                    curConfItem = null;
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

        public void Update()
        {
            CheckTextField();
            UpdateTransform();
        }

        private void CheckTextField()
        {
            if (!UnifiedInputManager.GetMouseButtonDown(0))
                return;

            InputField currentInputField = null;
            if (UnifiedInputManager.OnUI)
            {
                if (UnifiedInputManager.CurObj)
                {
                    currentInputField = UnifiedInputManager.CurObj.GetComponentInParent<InputField>();
                }
            }
            if (currentInputField != null && currentInputField.GetComponentInParent<DockMatchPanel>() != null)
            {
                NumBoardPanel keyboardpanel = NumBoardPanel.Instance;
                if (keyboardpanel != null)
                {
                    Transform tcam = Camera.main.transform;
                    Vector3 pos = currentInputField.transform.position
                        - tcam.forward * 0.2f;
                    pos += new Vector3(0, -0.3f, 0);
                    keyboardpanel.transform.position = pos;
                    keyboardpanel.transform.forward = tcam.forward;
                    //keyboardpanel.transform.position = currentInputField.transform.position
                    //- Camera.main.transform.forward * 0.01f;
                    keyboardpanel.inputfield = currentInputField;
                    keyboardpanel.gameObject.SetActive(true);
                }
            }


        }

        private void UpdateTransform()
        {
            if (srcmc != null && destmc != null)
            {
                Vector3 center = Vector3.zero;
                Vector3 size = new Vector3(20.0f, 20.0f, 20.0f);

                InputField[] fields = this.GetComponentsInChildren<InputField>();
                foreach (InputField field in fields)
                {
                    if (field.name == "CenterXField")
                    {
                        float.TryParse(field.GetComponentInChildren<Text>().text, out center.x);
                    }
                    else if (field.name == "CenterYField")
                    {
                        float.TryParse(field.GetComponentInChildren<Text>().text, out center.y);
                    }
                    else if (field.name == "CenterZField")
                    {
                        float.TryParse(field.GetComponentInChildren<Text>().text, out center.z);
                    }
                    else if (field.name == "SizeXField")
                    {
                        float.TryParse(field.GetComponentInChildren<Text>().text, out size.x);
                    }
                    else if (field.name == "SizeYField")
                    {
                        float.TryParse(field.GetComponentInChildren<Text>().text, out size.y);
                    }
                    else if (field.name == "SizeZField")
                    {
                        float.TryParse(field.GetComponentInChildren<Text>().text, out size.z);
                    }
                }

                iDock.Instance.UpdateTransform(destmc, center, size);
            }

        }

    }

}