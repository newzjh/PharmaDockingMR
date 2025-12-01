using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MoleculeLogic
{

    public class Pocket : HoverObject
    {
        public Vector3 barycenter;
        public List<PocketPoint> points = new List<PocketPoint>();
        public int index;
        public Color color;

        /// <summary>
        /// Static method for parsing atom entries in a pdb file and instantiating the correct
        /// <see cref="Atom" /> subclass.
        /// </summary>
        /// <param name="molecule">The molecule this atom belongs to.</param>
        /// <param name="pdbLine">An atom entry from a pdb file.</param>
        /// <returns>An instance of an <see cref="Atom" /> subclass.</returns>
        public static Pocket CreatePocket( int index, Transform parent)
        {
            GameObject pocketobj = new GameObject();
            pocketobj.transform.parent = parent;
            pocketobj.transform.localPosition = Vector3.zero;
            pocketobj.transform.localEulerAngles = Vector3.zero;
            pocketobj.transform.localScale = Vector3.one;
            pocketobj.name = "Pocket_" + index;
            Pocket pocket = pocketobj.AddComponent<Pocket>();
            pocket.index = index;
            return pocket;
        }


        //public void RepresentImmediately(int showpocket, int level, bool dirty)
        //{
        //    foreach (PocketPoint pt in points)
        //    {
        //        pt.Represent(showpocket, level, dirty);
        //    }
        //}

        //private YieldInstruction waitframe = new WaitForEndOfFrame();
        //public IEnumerator RepresentProgressively(int showpocket, int level, bool dirty)
        //{
        //    for(int i=0;i<points.Count;i++)
        //    {
        //        PocketPoint pt = points[i];
        //        pt.Represent(showpocket, level, dirty);
        //        if (dirty && (i % 30 == 0)) yield return waitframe;
        //    }
        //    yield return true;
        //}

    }
}
