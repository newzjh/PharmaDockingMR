
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoleculeLogic
{

    public class Surface : Representable
    {
        public PDBtoDEN PDBtoDENsurface=null;

        private Molecule m_mol = null;

        public static Surface CreateSurface(Molecule molecule, Transform parent)
        {

            GameObject surfaceobj = new GameObject("Surface");
            surfaceobj.transform.parent = parent;
            surfaceobj.transform.localPosition = Vector3.zero;
            surfaceobj.transform.localEulerAngles = Vector3.zero;
            surfaceobj.transform.localScale = Vector3.one;

            Surface sur = surfaceobj.AddComponent<Surface>();
            sur.PDBtoDENsurface = null;
            sur.m_mol = molecule;
   

            return sur;
        }

        public override void Represent(MoleculeScheme scheme, MolType type, bool dirty, int level)
        {
            if (!dirty) return;

            List<GameObject> deletelist = new List<GameObject>();
            for (int i = 0; i < transform.childCount; i++)
            {
                deletelist.Add(transform.GetChild(i).gameObject);
            }
            foreach (GameObject go in deletelist)
            {
                DestroyImmediate(go);
            }

            if (!scheme.showparts[0])
                return;

            if (scheme.ContainStyle(MoleculeStyle.Surface))
            {
                if (PDBtoDENsurface == null && m_mol!=null)
                {
                    PDBtoDENsurface = transform.gameObject.AddComponent<PDBtoDEN>();
                    long bc = GC.GetTotalMemory(false);
                    PDBtoDENsurface.TranPDBtoDEN(m_mol);
                    long ac = GC.GetTotalMemory(false);
                    Debug.Log("memory TranPDBtoDEN:" + (ac - bc) / 1024.0f / 1024.0f);
                }
                if (PDBtoDENsurface != null)
                {
                    PDBtoDENsurface.ProSurface(0.5f);
                }
            }

   
        }
    }

}
