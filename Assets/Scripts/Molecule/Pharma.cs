using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MoleculeLogic
{

    public class Pharma : HoverObject
    {
        public int atomic_number_label;
        public float defaultSearchRadius;
        public float clusterLimit;
        public List<PharmaPoint> points = new List<PharmaPoint>();
        public int index;
        public Color color;

        /// <summary>
        /// Static method for parsing atom entries in a pdb file and instantiating the correct
        /// <see cref="Atom" /> subclass.
        /// </summary>
        /// <param name="molecule">The molecule this atom belongs to.</param>
        /// <param name="pdbLine">An atom entry from a pdb file.</param>
        /// <returns>An instance of an <see cref="Atom" /> subclass.</returns>
        public static Pharma CreatePharma(int index, Transform parent)
        {
            GameObject pharmaobj = new GameObject();
            pharmaobj.name = "pharma";
            pharmaobj.transform.parent = parent;
            pharmaobj.transform.localPosition = Vector3.zero;
            pharmaobj.transform.localEulerAngles = Vector3.zero;
            pharmaobj.transform.localScale = Vector3.one;
            Pharma pharma = pharmaobj.AddComponent<Pharma>();
            pharma.index = index;
            return pharma;
        }

        ///// <summary>
        ///// Toggles visiblity of the atom. Also used to delay creation of the model until it's
        ///// actually needed.
        ///// </summary>
        ///// <param name="show">True to show the atom, false to hide it.</param>
        //public void RepresentImmediately(int showpharma, int level, bool dirty)
        //{
        //    foreach (PharmaPoint pt in points)
        //    {
        //        pt.Represent(showpharma, level , dirty);
        //    }
        //}

        //private YieldInstruction waitframe = new WaitForEndOfFrame();
        //public IEnumerator RepresentProgressively(int showpharma, int level, bool dirty)
        //{
        //    for(int i=0;i<points.Count;i++)
        //    {
        //        PharmaPoint pt = points[i];
        //        pt.Represent(showpharma, level, dirty);
        //        if (dirty && (i % 30 == 0)) yield return waitframe;
        //    }
        //    yield return true;
        //}
    }
}
