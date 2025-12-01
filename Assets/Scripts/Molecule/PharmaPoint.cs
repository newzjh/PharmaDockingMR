using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoleculeLogic
{
    public class PharmaPoint : Representable
    {
        public Vector3 pos;
        public float radius;
        public int index;
        public int parent = -1;
        public Color color;

        /// <summary>
        /// Static method for parsing atom entries in a pdb file and instantiating the correct
        /// <see cref="Atom" /> subclass.
        /// </summary>
        /// <param name="molecule">The molecule this atom belongs to.</param>
        /// <param name="pdbLine">An atom entry from a pdb file.</param>
        /// <returns>An instance of an <see cref="Atom" /> subclass.</returns>
        public static PharmaPoint CreatePharmaPoint(int index, Transform parent)
        {
            GameObject ptobj = new GameObject();
            ptobj.name = "PharamPt" + index.ToString();
            ptobj.transform.parent = parent;
            ptobj.transform.localPosition = Vector3.zero;
            ptobj.transform.localEulerAngles = Vector3.zero;
            ptobj.transform.localScale = Vector3.one;
            PharmaPoint pt = ptobj.AddComponent<PharmaPoint>();

            pt.index = index;
            return pt;
        }

        private GameObject displayobj=null;

        /// <summary>
        /// Toggles visiblity of the atom. Also used to delay creation of the model until it's
        /// actually needed.
        /// </summary>
        /// <param name="show">True to show the atom, false to hide it.</param>
        public override void Represent(MoleculeScheme scheme, MolType type, bool dirty, int level)
        {
            if (!dirty) return;

            this.transform.localPosition = this.pos;

            DestroyImmediate(displayobj);

            if (parent < scheme.showpharmas.Count &&
                scheme.showpharmas[parent])
            {
                displayobj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                displayobj.transform.parent = this.transform;
                displayobj.transform.localPosition = Vector3.zero;
                displayobj.transform.localEulerAngles = Vector3.zero;
                displayobj.transform.localScale = new Vector3(radius*1.5f, radius*1.5f, radius*1.5f);

                MeshFilter mf = displayobj.GetComponent<MeshFilter>();
                mf.sharedMesh = MeshLib.CreateMesh(MeshLib.MeshType.Sphere, level+1);

                MeshRenderer mr = displayobj.GetComponent<MeshRenderer>();
                mr.sharedMaterial = MaterialLib.GetStandardMaterialByColor(color);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
#if UNITY_5_3_OR_NEWER
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
#endif
            }


        }


    }
}
