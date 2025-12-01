using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace MoleculeLogic
{

    public class Combineable : MonoBehaviour
    {
        protected int oldvalue = -2;
        List<bool> oldshowparts = new List<bool>();
        public virtual bool IsDirty(MoleculeScheme scheme)
        {
            if (oldshowparts.Count != scheme.showparts.Count)
                return true;

            for (int i = 0; i < oldshowparts.Count; i++)
            {
                if (oldshowparts[i] != scheme.showparts[i])
                    return true;
            }

            return oldvalue != scheme.style;
        }
        public virtual void SetDirty(MoleculeScheme scheme)
        {
            oldvalue = scheme.style;
            oldshowparts.Clear();
            oldshowparts.AddRange(scheme.showparts);
        }

        public virtual int GetBreathInterval()
        {
            return 100;
        }

        public virtual int GetCombineInterval()
        {
            return 1000;
        }

        private int batchindex = 0;

        public void PreProcess()
        {
            CombinedMesh[] oldcms = this.GetComponentsInChildren<CombinedMesh>();
            if (oldcms != null)
            {
                for (int i = 0; i < oldcms.Length; i++)
                {
                    if (oldcms[i] == null) 
                        continue;
                    DestroyImmediate(oldcms[i].gameObject);
                }
            }
            MeshFilter[] mfs = this.GetComponentsInChildren<MeshFilter>();
            if (mfs != null)
            {
                for (int i = 0; i < mfs.Length; i++)
                {
                    if (mfs[i].gameObject == this.gameObject)
                        continue;
                    DestroyImmediate(mfs[i].gameObject);
                }
            }

            batchindex = 0;
        }

        public void PostProcess()
        { }

        // Use this for initialization
        public void CombineImmediately()
        {
            if (!Application.isPlaying)
                return;

            List<MeshFilter> mfs = new List<MeshFilter>();
            HoverObject[] hos = this.GetComponentsInChildren<HoverObject>();
            if (hos != null)
            {
                for (int i = 0; i < hos.Length; i++)
                {
                    if (hos[i].gameObject == this.gameObject)
                        continue;
                    MeshFilter[] submfs = hos[i].GetComponentsInChildren<MeshFilter>();
                    if (submfs != null)
                    {
                        foreach (MeshFilter submf in submfs)
                        {
                            if (submf.tag != "uncombineable")
                                mfs.Add(submf);
                        }
                        //mfs.AddRange(submfs);
                    }
                }
            }

            List<GameObject> combinedgolist = new List<GameObject>();
            List<MeshFilter[]> mfgroup = CombinedMesh.Cluster(mfs.ToArray());
            if (mfgroup != null)
            {
                for (int i = 0; i < mfgroup.Count; i++)
                {
                    MeshFilter[] mflist = mfgroup[i];
                    if (mflist == null) 
                        continue;

                    string combinedname = this.name+"_combined" +batchindex.ToString()+ "_" + i.ToString();
                    GameObject combinedgo = CombinedMesh.CreateCombinedObj(combinedname, this.transform, mflist);
                    if (combinedgo == null) 
                        continue;

                    combinedgolist.Add(combinedgo);
                }
            }

            this.gameObject.SetActive(true);

            Resources.UnloadUnusedAssets();
            GC.Collect();

            batchindex++;

        }


        // Use this for initialization
        public async UniTask CombineProgressively()
        {
            if (!Application.isPlaying)
                return;

            List<MeshFilter> mfs = new List<MeshFilter>();
            HoverObject[] hos = this.GetComponentsInChildren<HoverObject>();
            if (hos != null)
            {
                for (int i = 0; i < hos.Length; i++)
                {
                    if (hos[i].gameObject == this.gameObject) 
                        continue;
                    MeshFilter[] submfs = hos[i].GetComponentsInChildren<MeshFilter>();
                    if (submfs != null)
                    {
                        foreach (MeshFilter submf in submfs)
                        {
                            if (submf.tag != "uncombineable")
                                mfs.Add(submf);
                        }
                        //mfs.AddRange(submfs);
                    }
                }
            }

            await UniTask.NextFrame();

            List<GameObject> combinedgolist = new List<GameObject>();
            List<MeshFilter[]> mfgroup = CombinedMesh.Cluster(mfs.ToArray());

            await UniTask.NextFrame();

            if (mfgroup != null)
            {
                for (int i = 0; i < mfgroup.Count; i++)
                {
                    MeshFilter[] mflist = mfgroup[i];
                    if (mflist == null)
                        continue;

                    string combinedname = this.name + "_combined" + i.ToString();
                    GameObject combinedgo = CombinedMesh.CreateCombinedObj(combinedname, this.transform, mflist);
                    if (combinedgo == null) 
                        continue;

                    combinedgolist.Add(combinedgo);

                    await UniTask.NextFrame();
                }
            }

            this.gameObject.SetActive(true);

            //Resources.UnloadUnusedAssets();

            await UniTask.NextFrame();
        }

    }

}