using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;


namespace MoleculeLogic
{

    public class MoleculeFactory : Singleton<MoleculeFactory>
    {
        public void Clear()
        {
            List<GameObject> deletelist = new List<GameObject>();
            for (int i = 0; i < this.transform.childCount; i++)
            {
                deletelist.Add(this.transform.GetChild(i).gameObject);
            }
            for (int i = 0; i < deletelist.Count; i++)
            {
                GameObject.DestroyImmediate(deletelist[i]);
            }
        }

        private void Start()
        {
            Clear();

            AtomInfoReader.loadAtomInfo();

            //this.transform.localPosition = new Vector3(0, 0, 4);
            //CreateFromResource("1crn.pdb");

            //CreateFromZip("userdata/receptors/1AQ1.pdbqt");
        }

        float[] defaultangles = new float[] { -270f, -230.0f, -310.0f };
        private void ResetChildTransforms()
        {
            for (int i = 0; i < this.transform.childCount; i++)
            {
                Transform tchild = this.transform.GetChild(i);
                int index = i;
                float angle = -(index % 3 - 1) * 22.5f;
                float radius = defaultangles[index % 3] / 180.0f * Mathf.PI;
                float distance = (index / 3 + 1) * 1.0f;
                tchild.localPosition = new Vector3(distance * Mathf.Cos(radius), 0.3f, distance * Mathf.Sin(radius) + 1);
            }
        }

        public void CreateFromResource(string path)
        {

            string filename = Path.GetFileNameWithoutExtension(path).ToLower();
            string ext = Path.GetExtension(path).ToLower();

            TextAsset ta = Resources.Load<TextAsset>("DownloadPDB/" + path);
            if (ta == null)
                return;

            Transform t = this.transform.Find(filename);
            if (t != null)
                return;

            GameObject controllerobj = new GameObject(filename);
            controllerobj.transform.parent = this.transform;
            controllerobj.transform.localScale = Vector3.one;
            controllerobj.transform.localEulerAngles = Vector3.zero;
            controllerobj.transform.localPosition = Vector3.zero;
            MoleculeController mc = controllerobj.AddComponent<MoleculeController>();

            mc.CacheFile(filename, ta.bytes);
            mc.url = "resource:" + path;

            MemoryStream ms = new MemoryStream(ta.bytes);
            mc.CreateFromStream(ms, ext);

            ResetChildTransforms();

        }

        public void CreateFromZip(string path)
        {
            TextAsset ta = Resources.Load<TextAsset>("userdata.zip");
            if (ta == null)
                return;
            MemoryStream zipms = new MemoryStream(ta.bytes);
            ZipInputStream zis = new ZipInputStream(zipms);

            MemoryStream ms = null;

            ZipEntry zipEntry;
            while ((zipEntry = zis.GetNextEntry()) != null)
            {
                if (zipEntry.IsFile)
                {
                    if (zipEntry.Name == path)
                    {
                        ms = new MemoryStream();
                        int size = 2048;
                        byte[] bytes = new byte[2048];
                        while (true)
                        {
                            size = zis.Read(bytes, 0, bytes.Length);
                            if (size > 0)
                            {
                                ms.Write(bytes, 0, size);
                            }
                            else
                            {
                                break;
                            }
                        }
                        ms.Position = 0;
                    }
                }
            }


            string filename = Path.GetFileNameWithoutExtension(path).ToLower();
            string ext = Path.GetExtension(path).ToLower();

            Transform t = this.transform.Find(filename);
            if (t != null)
                return;

            GameObject controllerobj = new GameObject(filename);
            controllerobj.transform.parent = this.transform;
            controllerobj.transform.localScale = Vector3.one;
            controllerobj.transform.localEulerAngles = Vector3.zero;
            controllerobj.transform.localPosition = Vector3.zero;
            MoleculeController mc = controllerobj.AddComponent<MoleculeController>();

            ms.Seek(0, SeekOrigin.Begin);
            byte[] cachedata = new byte[ms.Length];
            ms.Read(cachedata, 0, (int)ms.Length);
            mc.CacheFile(filename, cachedata);
            mc.url = "zip:" + path;

            ms.Seek(0, SeekOrigin.Begin);
            mc.CreateFromStream(ms, ext);

            ResetChildTransforms();

        }

        public void CreateFromFile(string path)
        {
            string filename = Path.GetFileNameWithoutExtension(path).ToLower();
            string ext = Path.GetExtension(path).ToLower();

            Transform t = this.transform.Find(filename);
            if (t != null)
                return;

            if (!File.Exists(path))
                return;

            GameObject controllerobj = new GameObject(filename);
            controllerobj.transform.parent = this.transform;
            controllerobj.transform.localScale = Vector3.one;
            controllerobj.transform.localEulerAngles = Vector3.zero;
            controllerobj.transform.localPosition = Vector3.zero;
            MoleculeController mc = controllerobj.AddComponent<MoleculeController>();

            mc.CacheFile(filename, File.ReadAllBytes(path));
            mc.url = "file:" + path;

            FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            mc.CreateFromStream(fs, ext);

            ResetChildTransforms();
        }

        public class WebRequestCertificate : CertificateHandler
        {
            protected override bool ValidateCertificate(byte[] certificateData)
            {
                //return base.ValidateCertificate(certificateData);
                return true;
            }
        }


        public async UniTask CreateFromInternet(string url, string filename, string ext)
        {
            filename = filename.ToLower();
            Transform t = this.transform.Find(filename);
            if (t != null)
            {
                return;
            }

            GameObject controllerobj = new GameObject(filename);
            controllerobj.transform.parent = this.transform;
            controllerobj.transform.localScale = Vector3.one;
            controllerobj.transform.localEulerAngles = Vector3.zero;
            controllerobj.transform.localPosition = Vector3.zero;
            MoleculeController mc = controllerobj.AddComponent<MoleculeController>();

            var r = UnityWebRequest.Get(url);
            r.redirectLimit = 10;
            r.certificateHandler = new WebRequestCertificate();
            r.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36 Edg/130.0.0.0");
            //r.SetRequestHeader("Referer", "https://zinc.docking.org/");
            r.SetRequestHeader("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            r.SetRequestHeader("Accept-Encoding", "gzip, deflate, br, zstd");
            r.SetRequestHeader("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8,en-GB;q=0.7,en-US;q=0.6");
            r.SetRequestHeader("Upgrade-Insecure-Requests", "1");
            r.SetRequestHeader("Connection", "keep-alive");
            //r.SetRequestHeader("Host", "zinc.docking.org");

            try
            {
                await r.SendWebRequest();
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }

            if (r.isNetworkError || r.isHttpError)
            {
                Debug.LogError($"Error: {r.error}"); // 输出错误信息
                return;
            }

            if (r.downloadedBytes <= 0 || r.downloadHandler.data == null)
            {
                return;
            }

            var data = r.downloadHandler.data;

            r.certificateHandler.Dispose();

            mc.CacheFile(filename, data);
            mc.url = url;

            MemoryStream ms = new MemoryStream(data);
            await mc.CreateFromStream(ms, ext);

            if (!(ext == ".pdb" || ext == ".ent" || ext == ".pdbqt" || ext == ".ret"))
            {
                mc.mol.WritePdbFile(mc.cachefile);
            }

            ResetChildTransforms();

        }

        Vector2 pos = new Vector2(0, 0);
        public void OnUILayout()
        {
            pos = GUILayout.BeginScrollView(pos, false, true);

            for (int i = 0; i < this.transform.childCount; i++)
            {
                Transform t = this.transform.GetChild(i);
                MoleculeController molc = t.GetComponentInChildren<MoleculeController>();
                if (molc == null) continue;
                GUILayout.BeginVertical("", GUI.skin.textField);
                molc.OnUILayout();
                GUILayout.EndVertical();
            }

            GUILayout.EndScrollView();
        }
    }
}