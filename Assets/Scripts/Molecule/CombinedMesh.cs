using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MoleculeLogic
{

    public class CombinedMesh : MonoBehaviour
    {
        //private void OnDestroy()
        //{
        //    MeshFilter mf = GetComponent<MeshFilter>();
        //    if (mf == null) return;
        //    Mesh mesh = mf.sharedMesh;
        //    if (mesh == null) return;
        //    DestroyImmediate(mesh);
        //}

        public static List<MeshFilter[]> Cluster(MeshFilter[] mfs)
        {
            List<MeshFilter[]> mfgroup = new List<MeshFilter[]>();
            Dictionary<Material, List<MeshFilter>> mfdict = new Dictionary<Material, List<MeshFilter>>();
            if (mfs == null) 
                return mfgroup;
            foreach (MeshFilter mf in mfs)
            {
                if (mf == null) continue;
                if (mf.sharedMesh == null) continue;

                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr == null) 
                    continue;

                Material mat = mr.sharedMaterial;
                if (mat == null) 
                    continue;

                List<MeshFilter> mflist = null;
                
                if (mfdict.ContainsKey(mat))
                {
                    mflist = mfdict[mat];
                }
                else
                {
                    mflist = new List<MeshFilter>();
                    mfdict[mat] = mflist;
                }
                mflist.Add(mf);
            }

            List<MeshFilter> newmflist = new List<MeshFilter>();
            foreach (KeyValuePair<Material, List<MeshFilter>> pair in mfdict)
            {
                int combinedvertexcount = 0;
                List<MeshFilter> mflist = pair.Value;
                for (int i = 0; i < mflist.Count; i++)
                {
                    MeshFilter mf = mflist[i];
                    
                    int vertexcount = mf.sharedMesh.vertexCount; ;

                    if (combinedvertexcount + vertexcount > 32275)
                    {
                        mfgroup.Add(newmflist.ToArray());
                        newmflist.Clear();
                        combinedvertexcount = 0;
                    }

                    newmflist.Add(mf);
                    combinedvertexcount += vertexcount;
                }
                if (combinedvertexcount > 0)
                {
                    mfgroup.Add(newmflist.ToArray());
                    newmflist.Clear();
                    combinedvertexcount = 0;
                }
            }

            return mfgroup;
        }


        public static GameObject CreateCombinedObj(string combinedname,Transform parent,MeshFilter[] submfs)
        {
            if (parent == null) 
                return null;
            if (submfs == null) 
                return null;
            if (submfs.Length <= 0)
                return null;

            GameObject go = new GameObject(combinedname);
            go.transform.parent = parent;
            go.transform.localScale = Vector3.one;
            go.transform.localEulerAngles = Vector3.zero;
            go.transform.localPosition = Vector3.zero;
            CombinedMesh cm = go.AddComponent<CombinedMesh>();
            MeshFilter combinedmf = go.AddComponent<MeshFilter>();
            MeshRenderer combinedmr = go.AddComponent<MeshRenderer>();

            Matrix4x4 combinedtransform = parent.transform.localToWorldMatrix;

            Mesh combinedmesh = new Mesh();
            combinedmesh.name = combinedname;
            CombineInstance[] combine = new CombineInstance[submfs.Length];
            for (int i = 0; i < submfs.Length; i++)
            {
                combine[i].mesh = submfs[i].sharedMesh;
                Matrix4x4 subtransform = submfs[i].transform.localToWorldMatrix;
                combine[i].transform = combinedtransform.inverse * subtransform;
            }
            combinedmesh.CombineMeshes(combine);

            Material mat = null;
            for (int i = 0; i < submfs.Length; i++)
            {
                MeshRenderer mr = submfs[i].GetComponent<MeshRenderer>();
                if (mr == null) continue;
                mat = mr.sharedMaterial;
                mr.enabled = false;
            }

            if (Application.isPlaying)
            {
                combinedmf.sharedMesh = combinedmesh;
            }
            else
            {
                combinedmf.sharedMesh = combinedmesh;
            }
            combinedmr.material = mat;
            combinedmr.enabled = true;

            return go;
        }

    }

}
