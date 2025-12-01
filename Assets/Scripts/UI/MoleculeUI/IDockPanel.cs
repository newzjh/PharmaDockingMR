using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using MoleculeLogic;
using UnifiedInput;
using Mirror;


namespace MoleculeUI
{


    public class IDockPanel : BasePanelEx<IDockPanel>
    {
        private MoleculeController srcmc;
        private MoleculeController destmc;

        public Dictionary<string, iDockConfItem> cfgItems = new Dictionary<string, iDockConfItem>();

        public void Show(MoleculeController _srcmc,MoleculeController _destmc)
        {
            SetVisible(true);

            srcmc = _srcmc;
            destmc = _destmc;

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

            if (srcmc != null && destmc != null)
            {
                Vector3 pos = (srcmc.mol.transform.position + destmc.mol.transform.position) * 0.5f;
                Transform tcam = Camera.main.transform;
                pos -= tcam.up * 0.6f;
                pos -= tcam.forward * 0.1f;
                this.transform.position = pos;
                this.transform.forward = tcam.transform.forward;
                //Vector3 lpos = this.transform.localPosition;
                //lpos.z -= 100.0f;
                //lpos.y -= 300.0f;
                //this.transform.localPosition = lpos;
                //float scale = srcmc.mol.transform.localScale.y * 0.45f;
                //this.transform.localScale = new Vector3(scale, scale, scale);
            }
        }

        public void Hide()
        {
            SetVisible(false);
    
            //this.transform.localPosition = new Vector3(-5000, 0, 0);

            iDock.Instance.HideBound();

            srcmc = null;
            destmc = null;

            NumBoardPanel.Instance.SetVisible(false);
        }

        public override void Awake()
        {
            base.Awake();

            //Button[] buttons = this.GetComponentsInChildren<Button>();
            //if (buttons != null)
            //{
            //    for (int i = 0; i < buttons.Length; i++)
            //    {
            //        Button btn = buttons[i];
            //        if (btn)
            //        {
            //            btn.onClick.AddListener(delegate() { this.OnClick(btn); });
            //        }
            //    }
            //}

            LoadMatchTable();
        }

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

        public override void OnClick(GameObject sender)
        {
            if (sender.name.StartsWith("Button_OK"))
            {
                OnOK();
            }
            else if (sender.name.StartsWith("Button_Cancel"))
            {
                OnCancel();
            }
        }

        //private void OnClick(Button b)
        //{
        //    if (b.name.StartsWith("Button_OK"))
        //    {
        //        OnOK();
        //    }
        //    else if (b.name.StartsWith("Button_Cancel"))
        //    {
        //        OnCancel();
        //    }
        //}

        private void OnOK()
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

            CmdOnDetect(ligand_path, receptor_path, cfgitem);
        }

        private void OnCancel()
        {
            CmdOnHide();
        }

        [Command(requiresAuthority = false)]
        public void CmdOnDetect(string ligand_path, string receptor_path, iDockConfItem cfgitem)
        {
            RpcOnDetect(ligand_path, receptor_path, cfgitem);
        }

        [ClientRpc]
        public void RpcOnDetect(string ligand_path, string receptor_path, iDockConfItem cfgitem)
        {
            iDock.Instance.Detect(ligand_path, receptor_path, cfgitem);
            Hide();
        }

        [Command(requiresAuthority = false)]
        public void CmdOnHide()
        {
            RpcOnHide();
        }

        [ClientRpc]
        public void RpcOnHide()
        {
            Hide();
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
            if (currentInputField != null && currentInputField.GetComponentInParent<IDockPanel>()!=null)
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
            if (srcmc!=null && destmc!=null)
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

                iDock.Instance.UpdateTransform(destmc, center,size);
            }

        }
    }

}