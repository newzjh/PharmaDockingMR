using UnityEngine;
using System.IO;
using System.Collections.Generic;
using UnityEngine.UI;
using MoleculeLogic;
using MixedReality.Toolkit.UX;
using Mirror;
using Cysharp.Threading.Tasks;
using UnityEngine.XR.Interaction.Toolkit;


namespace MoleculeUI
{
    public class ZINCPanel : BasePanelEx<ZINCPanel>
    {
        private MoleculeFactory mf;

        public GameObject templateItem;

        public ScrollRect scrollRect;

        public override void Awake()
        {
            base.Awake();

            GameObject go = GameObject.Find("MoleculeFactory");
            if (go != null)
            {
                mf = go.GetComponent<MoleculeFactory>();
            }

        }

        public void OnEnable()
        {
            {
                var buttons = this.GetComponentsInChildren<Button>();
                for (int i = 0; i < buttons.Length; i++)
                {
                    var b = buttons[i];
                    if (b.name.StartsWith("Button"))
                    {
                        string bname = b.name.Substring(6, b.name.Length - 6);
                        if (bname.StartsWith("Recent"))
                        {
                            Text text = buttons[i].GetComponentInChildren<Text>();
                            string filename = PlayerPrefs.GetString(bname);
                            if (filename.Length > 0)
                                filename = Path.GetFileNameWithoutExtension(filename);
                            text.text = filename;
                        }
                    }
                }
            }
            {
                var buttons = this.GetComponentsInChildren<PressableButton>();
                for (int i = 0; i < buttons.Length; i++)
                {
                    var b = buttons[i];
                    if (b.name.StartsWith("Button"))
                    {
                        string bname = b.name.Substring(6, b.name.Length - 6);
                        if (bname.StartsWith("Recent"))
                        {
                            Text text = buttons[i].GetComponentInChildren<Text>();
                            string filename = PlayerPrefs.GetString(bname);
                            if (filename.Length > 0)
                                filename = Path.GetFileNameWithoutExtension(filename);
                            text.text = filename;
                        }
                    }
                }
            }

        }

        public void OpenZipCallback(string path)
        {
            CmdOpenZipCallback(path);
        }

        [Command(requiresAuthority = false)]
        public void CmdOpenZipCallback(string path)
        {
            RpcOpenZipCallback(path);
        }

        [ClientRpc]
        public void RpcOpenZipCallback(string path)
        {
            mf.CreateFromZip(path);
            PlayerPrefs.SetString("Recent1", PlayerPrefs.GetString("Recent0"));
            PlayerPrefs.SetString("Recent0", path);
            //DynamicPanel.Instance.SetAllVis(false);
        }

        public void OpenFileCallback(string path)
        {
            CmdOpenFileCallback(path);
        }

        [Command(requiresAuthority = false)]
        public void CmdOpenFileCallback(string path)
        {
            RpcOpenFileCallback(path);
        }

        [ClientRpc]
        public void RpcOpenFileCallback(string path)
        {
            mf.CreateFromFile(path);
            //DynamicPanel.Instance.SetAllVis(false);
        }

        public void OpenRecent(string path)
        {
            mf.CreateFromZip(path);
            //DynamicPanel.Instance.SetAllVis(false);
        }

        public void loadPDBFromInternet(string pdbStr)
        {
            string pdbid = pdbStr;
            pdbid = pdbid.Replace("\n", "");
#if UNITY_WEBGL && !UNITY_EDITOR
            string url = "./fetch.php?http://www.rcsb.org/pdb/download/downloadFile.do?fileFormat=pdb&compression=NO&structureId=" + pdbid;
#else
            string url = "https://files.rcsb.org/download/" + pdbid + ".pdb";
#endif
            //mf.CreateFromZip("userdata/receptors/1AQ1.pdbqt");
            mf.CreateFromInternet(url, "pdb" + pdbid, ".pdb");
            //DynamicPanel.Instance.SetAllVis(false);
        }

        private void OpenQRCode(string path)
        {
            if (path == null)
                return;

            if (path.StartsWith("zip:"))
            {
                path = path.Substring(4);
                mf.CreateFromZip(path);
                PlayerPrefs.SetString("Recent1", PlayerPrefs.GetString("Recent0"));
                PlayerPrefs.SetString("Recent0", path);
            }
            else if (path.StartsWith("file:"))
            {
                path = path.Substring(5);
                mf.CreateFromFile(path);
            }
            else if (path.StartsWith("resource:"))
            {
                path = path.Substring(9);
                mf.CreateFromResource(path);
            }
            else if (path.StartsWith("http:")||path.StartsWith("https:")||path.StartsWith("ftp:"))
            {
                mf.CreateFromInternet(path, Path.GetFileNameWithoutExtension(path), ".pdb");
            }
            //DynamicPanel.Instance.SetAllVis(false);
        }

        public override void OnClick(GameObject sender)
        {
            string objname = sender.name;
            if (objname.StartsWith("Button"))
            {
                string bname = objname.Substring(6, objname.Length - 6);
                if (bname.StartsWith("Recent"))
                {
                    string path = PlayerPrefs.GetString(bname);
                    OpenRecent(path);
                }
                else if (bname == "LoadFromFile")
                {
                    FileBrowserPanelEx filebrowserpanel = FileBrowserPanelEx.Instance;
                    filebrowserpanel.SetVisible(true);
                    filebrowserpanel.Selected -= OpenFileCallback;
                    filebrowserpanel.Selected += OpenFileCallback;
                    filebrowserpanel.Init("*.pdb|*.ent|*.mol2|*.ml2|*.sy2|*.pdbqt", null);
                    CruveMainPanel.Instance.SetVisible(false);
                    CurveOverlayPanel.Instance.SetVisible(false);
                }
                else if (bname == "LoadFromResources")
                {
                    ZipBrowserPanelEx filebrowserpanel = ZipBrowserPanelEx.Instance;
                    filebrowserpanel.SetVisible(true);
                    filebrowserpanel.Selected -= OpenZipCallback;
                    filebrowserpanel.Selected += OpenZipCallback;
                    filebrowserpanel.Init("*.pdb|*.ent|*.mol2|*.ml2|*.sy2|*.pdbqt", null);
                    CruveMainPanel.Instance.SetVisible(false);
                    CurveOverlayPanel.Instance.SetVisible(false);
                }
                else if (bname == "LoadFromInternet")
                {
                    //Transform pdbidfieldgo = sender.transform.parent.FindChild("InputField");
                    //if (pdbidfieldgo != null)
                    //{
                    //    Transform tplaceholder = pdbidfieldgo.FindChild("Placeholder");
                    //    if (tplaceholder != null)
                    //    {
                    //        Text t = tplaceholder.GetComponent<Text>();
                    //        string pdbid = t.text;
                    //        pdbid = pdbid.Replace("\n", "");
                    //        string url = "http://www.rcsb.org/pdb/download/downloadFile.do?fileFormat=pdb&compression=NO&structureId=" + pdbid;
                    //        mf.StartCoroutine(mf.CreateFromInternet(url, "pdb" + pdbid, ".pdb"));
                    //    }
                    //}
                    var inputfield = sender.transform.parent.parent.GetComponentInChildren<InputField>();
                    if (inputfield != null)
                    {
                        loadPDBFromInternet(inputfield.text);
                    }
                    var inputfield2 = sender.transform.parent.parent.GetComponentInChildren<TMPro.TMP_InputField>();
                    if (inputfield2 != null)
                    {
                        loadPDBFromInternet(inputfield2.text);
                    }
                }
                else if (bname == "LoadByQRCode")
                {
                    QRCodeScanPanel filebrowserpanel = QRCodeScanPanel.Instance;
                    filebrowserpanel.SetVisible(true);
                    filebrowserpanel.Selected -= OpenQRCode;
                    filebrowserpanel.Selected += OpenQRCode;
                    CruveMainPanel.Instance.SetVisible(false);
                    CurveOverlayPanel.Instance.SetVisible(false);
                }
                else if (bname.Contains("LoadPDB"))
                {
                    var id = objname.Substring(13, objname.Length - 13); ;
                    mf.CreateFromZip("userdata/receptors/" + id + ".pdbqt");
                }
                else if (bname.Contains("LoadZINC"))
                {
                    var id = objname.Substring(10, objname.Length - 10);
                    if (id.Length == 12)
                        id = id.Substring(0, 4) + "0000" + id.Substring(4);
                    var id2 = id;
                    if (id2.Length == 16)
                        id2 = id2.Substring(0, 4) + id2.Substring(8);
                    mf.CreateFromInternet("https://zinc.docking.org/substances/"+id+".sdf",id2,".sdf");
                }
            }
        }

        public async void RefreshByIDS(List<string> ids)
        {
            var deleteList = new List<GameObject>();
            for (int i = 0; i < scrollRect.content.childCount; i++) 
            {
                deleteList.Add(scrollRect.content.GetChild(i).gameObject);
            }
            foreach(var go in deleteList)
            {
                GameObject.DestroyImmediate(go);
            }

            await UniTask.NextFrame();

            foreach(var id in ids)
            {
                GameObject go = GameObject.Instantiate(templateItem, scrollRect.content);
                go.transform.localScale = Vector3.one;
                go.transform.localEulerAngles = Vector3.zero;
                go.gameObject.SetActive(true);

                var text = go.GetComponentInChildren<Text>(true);
                text.text = id;

                var b = go.GetComponentInChildren<PressableButton>(true);
                b.gameObject.name = "ButtonLoad" + id;

                var rc = b.gameObject.GetComponent<RectTransform>();
                var box = b.gameObject.GetComponentInChildren<BoxCollider>(true);
                var delta = Vector2.one * 0.5f - rc.pivot;
                delta.x *= rc.sizeDelta.x;
                delta.y *= rc.sizeDelta.y;
                box.center = new Vector3(delta.x, delta.y, 4.606735f);
                box.size = new Vector3(rc.sizeDelta.x, rc.sizeDelta.y, 32);
                if (b)
                {
                    b.OnClicked.RemoveAllListeners();
                    b.OnClicked.AddListener(delegate ()
                    {
                        string path = GetGameObjectPath(b.gameObject);
                        this.CmdOnClick(path);
                        //this.OnClick(newb.gameObject);
                    });
                    b.firstHoverEntered.RemoveAllListeners();
                    b.firstHoverEntered.AddListener(delegate (HoverEnterEventArgs e)
                    {
                        e.interactableObject.transform.localScale = 1.1f * Vector3.one;
                    });
                    b.lastHoverExited.RemoveAllListeners();
                    b.lastHoverExited.AddListener(delegate (HoverExitEventArgs e)
                    {
                        e.interactableObject.transform.localScale = 1.0f * Vector3.one;
                    });
                }
                var adapter = b.gameObject.GetComponent<UGUIInputAdapter>();
                adapter.interactable = true;


            }
        }



    }
}