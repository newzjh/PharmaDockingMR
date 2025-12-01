using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace MoleculeLogic
{

    public class Bond : Representable
    {

        public Atom a1;
        public Atom a2;
        public float distance;
        public int order;
        int index;

        private List<GameObject> displayobjs=new List<GameObject>();

        public static Bond CreateBond(Molecule molecule, Atom a1, Atom a2,int order,int index,Transform parent)
        {
            Vector3 deltapos = a2.position - a1.position;

            GameObject bondobj = new GameObject();
            bondobj.name = "bond_"+index;
            bondobj.transform.parent = parent;// molecule.transform;
            bondobj.transform.localScale = Vector3.one;
            bondobj.transform.localPosition = (a1.position + a2.position) * 0.5f;
            bondobj.transform.localRotation = Quaternion.LookRotation(deltapos);
            Bond bond = bondobj.AddComponent<Bond>();
            bond.a1 = a1;
            bond.a2 = a2;
            bond.index = index;
            bond.order = order;
            bond.distance = deltapos.magnitude;
            a1.bonds.Add(a2, bond);
            a2.bonds.Add(a1, bond);

            return bond;
        }

        // Use this for initialization
        void Start()
        {

        }

        public override void Represent(MoleculeScheme scheme, MolType type, bool dirty, int level)
        {
            if (!dirty) return;

            for (int i = 0; i < displayobjs.Count; i++)
            {
                DestroyImmediate(displayobjs[i]);
            }
            displayobjs.Clear();


            if (!scheme.showparts[a1.partIndex] || !scheme.showparts[(int)a2.partIndex])
                return;

            //if (a1.partIndex == CompondType.Receptor || a2.partIndex == CompondType.Receptor)
            //    return;
            if (type == MolType.Conformation && (a1.partIndex == 0 || a2.partIndex==0))
                return;

            Vector3 delta = a2.position - a1.position;
            float len = delta.magnitude;

            if (a1.partIndex>=1 || a2.partIndex>=1)
            {
                GameObject displayobj1 = null;
                displayobj1 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                displayobj1.transform.parent = this.transform;
                displayobj1.transform.localScale = new Vector3(0.3f, len * 0.25f, 0.3f);
                displayobj1.transform.localPosition = new Vector3(0, 0, -len * 0.25f);
                displayobj1.transform.localEulerAngles = new Vector3(90, 0, 0);
                displayobjs.Add(displayobj1);

                GameObject displayobj2 = null;
                displayobj2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                displayobj2.transform.parent = this.transform;
                displayobj2.transform.localScale = new Vector3(0.3f, len * 0.25f, 0.3f);
                displayobj2.transform.localPosition = new Vector3(0, 0, len * 0.25f);
                displayobj2.transform.localEulerAngles = new Vector3(90, 0, 0);
                displayobjs.Add(displayobj2);
            }
            else if (scheme.ContainStyle(MoleculeStyle.Wireframe))
            {
                GameObject displayobj = null;
                displayobj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                displayobj.transform.parent = this.transform;
                displayobj.transform.localScale = new Vector3(0.1f, len * 0.5f, 0.1f);
                displayobj.transform.localPosition = new Vector3(0, 0, 0);
                displayobj.transform.localEulerAngles = new Vector3(90, 0, 0);
                displayobjs.Add(displayobj);
            }
            else if (scheme.ContainStyle(MoleculeStyle.Stick))
            {
                GameObject displayobj1 = null;
                displayobj1 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                displayobj1.transform.parent = this.transform;
                displayobj1.transform.localScale = new Vector3(0.3f, len * 0.25f, 0.3f);
                displayobj1.transform.localPosition = new Vector3(0, 0, -len * 0.25f);
                displayobj1.transform.localEulerAngles = new Vector3(90, 0, 0);
                displayobjs.Add(displayobj1);

                GameObject displayobj2 = null;
                displayobj2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                displayobj2.transform.parent = this.transform;
                displayobj2.transform.localScale = new Vector3(0.3f, len * 0.25f, 0.3f);
                displayobj2.transform.localPosition = new Vector3(0, 0, len * 0.25f);
                displayobj2.transform.localEulerAngles = new Vector3(90, 0, 0);
                displayobjs.Add(displayobj2);
            }
            else if (scheme.ContainStyle(MoleculeStyle.Ball))
            {
                GameObject displayobj = null;
                displayobj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                displayobj.transform.parent = this.transform;
                displayobj.transform.localScale = new Vector3(0.0f, len * 0.5f, 0.0f);
                displayobj.transform.localPosition = new Vector3(0, 0, 0);
                displayobj.transform.localEulerAngles = new Vector3(90, 0, 0);
                displayobjs.Add(displayobj);
            }
            else if (scheme.ContainStyle(MoleculeStyle.BallStick))
            {
                GameObject displayobj = null;
                displayobj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                displayobj.transform.parent = this.transform;
                displayobj.transform.localScale = new Vector3(0.3f, len * 0.5f, 0.3f);
                displayobj.transform.localPosition = new Vector3(0, 0, 0);
                displayobj.transform.localEulerAngles = new Vector3(90, 0, 0);
                displayobjs.Add(displayobj);
            }
            else if (scheme.ContainStyle(MoleculeStyle.Cartoon))
            {
                GameObject displayobj = null;
                displayobj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                displayobj.transform.parent = this.transform;
                displayobj.transform.localScale = new Vector3(0.2f, len * 0.5f, 0.2f);
                displayobj.transform.localPosition = new Vector3(0, 0, 0);
                displayobj.transform.localEulerAngles = new Vector3(90, 0, 0);
                displayobjs.Add(displayobj);
            }
            else
            {
                GameObject displayobj = null;
                displayobj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                displayobj.transform.parent = this.transform;
                displayobj.transform.localScale = new Vector3(0.0f, 0.0f, 0.0f);
                displayobj.transform.localPosition = new Vector3(0, 0, 0);
                displayobj.transform.localEulerAngles = new Vector3(90, 0, 0);
                displayobjs.Add(displayobj);
            }

            for(int i=0;i<displayobjs.Count;i++)
            {
                GameObject displayobj = displayobjs[i];
                MeshFilter mf = displayobj.GetComponent<MeshFilter>();
                mf.sharedMesh = MeshLib.CreateMesh(MeshLib.MeshType.Cylinder, level);

                MeshRenderer mr = displayobj.GetComponent<MeshRenderer>();
                if (scheme.ContainStyle(MoleculeStyle.Stick))
                {
                    if (i==0)
                        mr.sharedMaterial = MaterialLib.GetStandardMaterialByColor(a1.atomColor);
                    else
                        mr.sharedMaterial = MaterialLib.GetStandardMaterialByColor(a2.atomColor);
                }
                else
                {
                    mr.sharedMaterial = MaterialLib.GetStandardMaterialByColor(Color.gray);
                }
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
#if UNITY_5_3_OR_NEWER
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
#endif
            }
                

        }
    }
}