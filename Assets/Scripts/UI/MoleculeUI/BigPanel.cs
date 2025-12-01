using UnityEngine;
using System.IO;
using UnityEngine.UI;
using MoleculeLogic;
using UnifiedInput;
using MixedReality.Toolkit.UX;
using Mirror;
using System;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Networking;
using System.Linq;

[Serializable]
public class RequestMessagesItem
{
    /// <summary>
    /// 
    /// </summary>
    public List<RequestMessagesContentBase> content;
    /// <summary>
    /// 
    /// </summary>
    public string role;
}

[Serializable]
public class RequestMessagesUrl
{
    /// <summary>
    /// 
    /// </summary>public string url = "https://ark-project.tos-cn-beijing.ivolces.com/images/view.jpeg";
    /// <summary>
    /// 
}

[Serializable]
public class RequestMessagesContentBase
{
    /// <summary>
    /// 
    /// </summary>
    public string type;
}

[Serializable]
public class RequestMessagesContentImage : RequestMessagesContentBase
{
    /// <summary>
    /// 
    /// </summary>
    public RequestMessagesUrl image_url;
}

[Serializable]
public class RequestMessagesContentText : RequestMessagesContentBase
{
    /// <summary>
    /// 
    /// </summary>
    public string text;
}


[Serializable]
public class RequestMessageBody
{
    /// <summary>
    /// 
    /// </summary>
    public List<RequestMessagesItem> messages;
    /// <summary>
    /// 
    /// </summary>
    public string model;
    /// <summary>
    /// 
    /// </summary>
    public bool stream;
}

[Serializable]
public class Delta
{
    /// <summary>
    /// 
    /// </summary>
    public string content;

    /// <summary>
    /// 
    /// </summary>
    public string reasoning_content;

    /// <summary>
    /// 
    /// </summary>
    public string role;
}

[Serializable]
public class ChoicesItem
{
    /// <summary>
    /// 
    /// </summary>
    public Delta delta;
    /// <summary>
    /// 
    /// </summary>
    public int index;
}
[Serializable]
public class ResponseRoot
{
    /// <summary>
    /// 
    /// </summary>
    public List<ChoicesItem> choices;
    /// <summary>
    /// 
    /// </summary>
    public int created;
    /// <summary>
    /// 
    /// </summary>
    public string id;
    /// <summary>
    /// 
    /// </summary>
    public string model;
    /// <summary>
    /// 
    /// </summary>
    public string service_tier;
    /// <summary>
    /// 
    /// </summary>
    public string @object;
    /// <summary>
    /// 
    /// </summary>
    public string usage;
}

namespace MoleculeUI
{
    public class BigPanel : BasePanelEx<BigPanel>
    {
        private MoleculeFactory mf;

        [NonSerialized]
        private string apiKey = "ba0e3461-60b9-49d2-87e6-d6e6d95e10bd";
        [NonSerialized]
        private string apiUrl = "https://ark.cn-beijing.volces.com/api/v3/chat/completions";

        public GameObject ToggleGo;

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
                    bool auto = false;
                    if (ToggleGo)
                    {
                        var b1 = ToggleGo.GetComponentInChildren<PressableButton>(true);
                        if (b1 && b1.ToggleMode == MixedReality.Toolkit.StatefulInteractable.ToggleType.Toggle)
                        {
                            auto = b1.IsToggled.Active;
                        }
                        var b2 = ToggleGo.GetComponentInChildren<Toggle>(true);
                        if (b2)
                        {
                            auto = b2.isOn;
                        }

                    }
                    if (auto)
                        CmdSearchInZINCALL(id);
                }
                else if (bname.Contains("LoadZINC"))
                {
                    var id = objname.Substring(10, objname.Length - 10);
                    mf.CreateFromZip("userdata/ligands/ZINC/" + id + ".pdbqt");
                    //mf.CreateFromInternet("https://zinc.docking.org/substances/"+id+".sdf",id,".sdf");
                }
            }
        }

        Dictionary<string, bool> searchingids = new Dictionary<string, bool>();
        [Command(requiresAuthority = false)]
        public async void CmdSearchInZINCALL(string receptorid)
        {
            if (searchingids.ContainsKey(receptorid))
                return;

            searchingids[receptorid] = true;

            var task1 = SearchInZINC(receptorid, "doubao-seed-1-6-lite-251015");
            var task2 = SearchInZINC(receptorid, "deepseek-v3-1-terminus");
            var ret = await UniTask.WhenAll<List<string>>(new UniTask<List<string>>[] { task1, task2 });

            Dictionary<string, bool> ids = new Dictionary<string, bool>();
            foreach (var each in ret)
            {
                foreach (var item in each)
                {
                    var id = item;
                    if (id.Length == 12)
                        id = id.Substring(0, 4) + "0000" + id.Substring(4);
                    var id2 = id;
                    if (id2.Length == 16)
                        id2 = id2.Substring(0, 4) + id2.Substring(8);
                    int index = -1;
                    int.TryParse(id2.Replace("ZINC", ""), out index);
                    if (index < 100)
                        continue;
                    ids[id2] = true;
                }
            }

            searchingids.Remove(receptorid);

            RefreshByIDS(ids.Keys.ToList());
        }


        [ClientRpc]
        public void RefreshByIDS(List<string> ids)
        {
            ZINCPanel.Instance.RefreshByIDS(ids);
        }



        public async UniTask<List<string>> SearchInZINC(string id, string model)
        {
            string question = "为" + id + "这个receptor筛选出一些配体，不需要步骤，只要ZINC ID列表形式返回结果，结果里只要纯文本的ZINC ID和换号分隔符";
            var ret = await SendStreamRequestCommon(question, model);
            var answer = ret.Item1;
            var lines = answer.Split("\n");
            List<string> ids = new List<string>();
            List<string> searchs = new List<string>();
            for (int i = 0; i < 10; i++)
                searchs.Add("ZINC" + i.ToString());
            foreach (var line in lines)
            {
                for (int i = 0; i < 10; i++)
                    if (line.StartsWith(searchs[i]))
                        ids.Add(line.Replace("\n", ""));
            }
            return ids;
        }

        public async UniTask<ValueTuple<string, string, float>> SendStreamRequestCommon(string question, string inmodel)
        {
            var requestbody = new RequestMessageBody
            {
                messages = new List<RequestMessagesItem>
                {
                    new RequestMessagesItem
                    {
                        content = new List<RequestMessagesContentBase>
                        {
                            new RequestMessagesContentText
                            {
                                type = "text",
                                text = question
                            }
                        },
                        role = "user"
                    }
                },
                model = inmodel,
                stream = true
            };

            string jsonBody = JsonConvert.SerializeObject(requestbody);

            Debug.Log("question:" + question);

            var fullContent = new StringBuilder();
            var fullReason = new StringBuilder();

            float t1 = Time.time;
            using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();

                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", $"Bearer {apiKey}");

                try
                {
                    await request.SendWebRequest();
                }
                catch(Exception e)
                {
                    Debug.LogError(e);
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"request error: {request.error}");
                    return ("", "", 0);
                }

                string response = request.downloadHandler.text;

                string[] lines = response.Split('\n');
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (line.StartsWith("data:"))
                    {
                        string jsonData = line.Substring(5).Trim();

                        if (jsonData == "[DONE]")
                        {
                            Debug.Log($"fullContent: {fullContent}");
                            Debug.Log($"fullReason: {fullReason}");
                            break;
                        }

                        try
                        {
                            var fragment = JsonConvert.DeserializeObject<ResponseRoot>(jsonData);

                            if (fragment.choices.Count > 0)
                            {
                                string content = fragment.choices[0].delta.content;
                                fullContent.Append(content);

                                string reason = fragment.choices[0].delta.reasoning_content;
                                fullReason.Append(reason);
                            }
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogWarning($"detect fail: {e.Message}\ndata: {jsonData}");
                        }
                    }
                }
            }

            float t2 = Time.time;
            Debug.Log("detect time = " + (t2 - t1) + " s");

            return (fullContent.ToString(), fullReason.ToString(), t2 - t1);
        }


    }
}