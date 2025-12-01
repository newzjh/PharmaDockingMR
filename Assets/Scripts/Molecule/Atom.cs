//=============================================================================
// This file is part of The Scripps Research Institute's C-ME Application built
// by InterKnowlogy.  
//
// Copyright (C) 2006, 2007 Scripps Research Institute / InterKnowlogy, LLC.
// All rights reserved.
//
// For information about this application contact Tim Huckaby at
// TimHuck@InterKnowlogy.com or (760) 930-0075 x201.
//
// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND,
// EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.
//=============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoleculeLogic
{
    public enum AtomType
    {
        Other = 0,
        C,
        N,
        O,
        S,
        P,
        H,
        Max
    }


    /// <summary>
    /// Abstract base class for all types of atoms.
    /// </summary>
    /// <remarks>
    /// Handles the majority of atom-related display logic.
    /// </remarks>
    public class Atom : Representable
    {
        public static Color[] AtomColorMap = new Color[(int)AtomType.Max]
        {
            Color.magenta, //Other
            Color.gray, //C
            Color.blue, //N
            Color.red,  //O
            Color.yellow,  //S
            Color.cyan,  //P
            Color.green  //H
        };

        public bool hetType;
        public string atomName;
        public string residueName;
        public string chainIdentifier;
        public int residueSequenceNumber;
        public Vector3 position;
        public float temperatureFactor;
        public Color atomColor;
        public Color structureColor;
        public Color temperatureColor;
        public AtomType atomType;
        public Dictionary<Atom, Bond> bonds = new Dictionary<Atom, Bond>();
        public Residue residue;
        public Chain chain;
        protected GameObject displayobj;
        private ColorScheme colorScheme;
        private bool isSelected;
        private bool showAsSelected;
		public int index;
        public int partIndex = 0;

        //share elec
        public List<Atom> shareE_targetAtom;
        public List<int> shareE_num;






    









        /// <summary>
        /// Currently used coloring method.
        /// </summary>
        public ColorScheme ColorScheme
        {
            get
            {
                return this.colorScheme;
            }
            set
            {
                if (this.colorScheme != value)
                {
                    this.colorScheme = value;
                    this.UpdateColorView();
                }
            }
        }
  

        /// <summary>
        /// Static method for parsing atom entries in a pdb file and instantiating the correct
        /// <see cref="Atom" /> subclass.
        /// </summary>
        /// <param name="molecule">The molecule this atom belongs to.</param>
        /// <param name="pdbLine">An atom entry from a pdb file.</param>
        /// <returns>An instance of an <see cref="Atom" /> subclass.</returns>
        public static Atom CreateAtom(Molecule molecule, string atomName, bool hetType,
             string residueName, int residueSequenceNumber, string chainIdentifier, Vector3 pos,
            float temperatureFactor, int index, Transform parent)
        {
            GameObject atomobj = new GameObject();
            atomobj.transform.parent = parent;
            atomobj.transform.localPosition = Vector3.zero;
            atomobj.transform.localEulerAngles = Vector3.zero;
            atomobj.transform.localScale = Vector3.one;
            Atom atom=null;

            if (Residue.IsAminoName(residueName))
            {
                if (atomName == "CA")
                    atom = atomobj.AddComponent<CAlpha>();
                else
                    atom = atomobj.AddComponent<ChainAtom>();
            }
            else
            {
                if (residueName == "HOH")
                    atom = atomobj.AddComponent<Water>();
                else
                    atom =  atomobj.AddComponent<HetAtom>();
            }

            atom.index = index;
            atom.atomName = atomName;
            atom.residueName = residueName;
            atom.hetType = hetType;
            atom.residueSequenceNumber = residueSequenceNumber;
            atom.chainIdentifier = chainIdentifier;
            atom.position = pos;
            atom.temperatureFactor = temperatureFactor;
            if (atom.atomName.StartsWith("C"))
                atom.atomType = AtomType.C;
            else if (atom.atomName.StartsWith("N"))
                atom.atomType = AtomType.N;
            else if (atom.atomName.StartsWith("O"))
                atom.atomType = AtomType.O;
            else if (atom.atomName.StartsWith("H") || 
                (atom.atomName.Length>1 && atom.atomName[0]>'0' && atom.atomName[0]<'9'))
                atom.atomType = AtomType.H;
            else if (atom.atomName.StartsWith("S"))
                atom.atomType = AtomType.S;
            else
                atom.atomType = AtomType.Other;
            atom.atomColor = AtomColorMap[(int)atom.atomType];
            atom.structureColor = atom.atomColor;
            atom.colorScheme = ColorScheme.Structure;

            atomobj.name = index.ToString() + "_"+ atom.atomName;

            return atom;
        }

        /// <summary>
        /// Toggles visiblity of the atom. Also used to delay creation of the model until it's
        /// actually needed.
        /// </summary>
        /// <param name="show">True to show the atom, false to hide it.</param>

        public override void Represent(MoleculeScheme scheme, MolType type, bool dirty, int level)
        {
            if (!dirty) 
                return;

            this.transform.localPosition = this.position;

            if (displayobj)
            {
                DestroyImmediate(displayobj);
            }

            if (!scheme.showatoms[(int)atomType])
                return;

            if (!scheme.showparts[partIndex])
                return;

            if (scheme.style == (int)MoleculeStyle.Micro)
            {
                GenerateMicroRepresentation(scheme, type, level);
            }
            else if (scheme.style == (int)MoleculeStyle.AEO)
            {
                GenerateMicroRepresentation(scheme, type, level,true);
            }
            else
            {
                GenerateNormalRepresentation(scheme,type, level);
            }

        }

        private void GenerateNormalRepresentation(MoleculeScheme scheme, MolType type, int level)
        {
            if (type == MolType.Conformation && partIndex == 0)
                return;

            displayobj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            displayobj.transform.parent = this.transform;
            displayobj.transform.localPosition = Vector3.zero;
            displayobj.transform.localEulerAngles = Vector3.zero;

            if (partIndex>=1)
            {
                displayobj.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            }
            else if (scheme.ContainStyle(MoleculeStyle.Wireframe))
            {
                displayobj.transform.localScale = new Vector3(0.1f, 0.05f, 0.1f);
            }
            else if (scheme.ContainStyle(MoleculeStyle.Stick))
            {
                displayobj.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            }
            else if (scheme.ContainStyle(MoleculeStyle.Ball))
            {
                displayobj.transform.localScale = new Vector3(2.5f, 2.5f, 2.5f);
            }
            else if (scheme.ContainStyle(MoleculeStyle.BallStick))
            {
                displayobj.transform.localScale = new Vector3(0.75f, 0.75f, 0.75f);
            }
            else if (scheme.ContainStyle(MoleculeStyle.Cartoon))
            {
                displayobj.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
            }
            else
            {
                displayobj.transform.localScale = new Vector3(0.0f, 0.0f, 0.0f);
            }

            MeshFilter mf = displayobj.GetComponent<MeshFilter>();
            mf.sharedMesh = MeshLib.CreateMesh(MeshLib.MeshType.Sphere, level);

            MeshRenderer mr = displayobj.GetComponent<MeshRenderer>();
            mr.sharedMaterial = MaterialLib.GetStandardMaterialByColor(atomColor);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
#if UNITY_5_3_OR_NEWER
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
#endif
        }

        private static GameObject ringprefab = null;

        private void GenerateMicroRepresentation(MoleculeScheme scheme, MolType type, int level, bool presentShareE = false)
        {
            displayobj = new GameObject();
            displayobj.name = "displayobj";
            displayobj.transform.parent = this.transform;
            displayobj.transform.localPosition = Vector3.zero;
            displayobj.transform.localEulerAngles = Vector3.zero;
            displayobj.transform.localScale = new Vector3(0.3f,0.3f,0.3f);

            string atomType=atomName.Substring(0,1);
            AtomInfo atomInfo=AtomInfoReader.atomInfos["C"];
            if (AtomInfoReader.atomInfos.ContainsKey(atomType))
            {
                atomInfo = AtomInfoReader.atomInfos[atomType];
            }


            int k = 0;
            float latStepAngle=Mathf.PI/(float)(atomInfo.Nucleus.Length); //留一行空出极点，因为极点只能摆一个球
            float latStartAngle=0-latStepAngle*(float)(atomInfo.Nucleus.Length-1)/2.0f;
            for (int lat = 0; lat < atomInfo.Nucleus.Length; lat++) //for (int lat = -2; lat <= 2; lat++)
            {
                //int lat = i - atomInfo.Nucleus.Length / 2;
                int[] pnCount = new int[2];
                pnCount[0] = (int)atomInfo.Nucleus[lat].x;
                pnCount[1] = (int)atomInfo.Nucleus[lat].y;
                int count = pnCount[0] + pnCount[1];
                int indexPtr = lat % 2;
                for (int lot = 0; lot < count; lot++) //for (int lot = -4; lot <= 4; lot++)
                {
                    if (pnCount[indexPtr] <= 0) indexPtr = 1 - indexPtr;
                    if (pnCount[indexPtr] <= 0) break;
                    pnCount[indexPtr]--;

                    GameObject subdisplayobj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    if (indexPtr == 0)
                        subdisplayobj.name = "proton_" + k.ToString();
                    else
                        subdisplayobj.name = "neutron_"+k.ToString();

                    subdisplayobj.transform.parent = displayobj.transform;
                    float latangle = lat * latStepAngle+ latStartAngle;
                    float lotangle = lot * (Mathf.PI*2.0f / (float)(count));
                    float distance = 1.0f*atomInfo.nucleusDisScale;
                    Vector3 pos;
                    pos.x = Mathf.Cos(latangle) * Mathf.Sin(lotangle) * distance;
                    pos.y = Mathf.Sin(latangle) * distance;
                    pos.z = Mathf.Cos(latangle) * Mathf.Cos(lotangle) * distance;
                    subdisplayobj.transform.localPosition = pos;
                    subdisplayobj.transform.localEulerAngles = Vector3.zero;
                    subdisplayobj.transform.localScale = Vector3.one*atomInfo.nucleusRadScale;

                    MeshFilter mf = subdisplayobj.GetComponent<MeshFilter>();
                    mf.sharedMesh = MeshLib.CreateMesh(MeshLib.MeshType.Sphere, level);

                    MeshRenderer mr = subdisplayobj.GetComponent<MeshRenderer>();
                    if (indexPtr==0)
                        mr.sharedMaterial = MaterialLib.GetStandardMaterialByColor(this.atomColor);
                    else
                        mr.sharedMaterial = MaterialLib.GetStandardMaterialByColor(Color.gray);
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
#if UNITY_5_3_OR_NEWER
                    mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
#endif
                    indexPtr = 1 - indexPtr;
                    k++;
                }
            }

            if (ringprefab == null)
                ringprefab = Resources.Load<GameObject>("Platform/Ring");
            Material whitemat = MaterialLib.GetStandardMaterialByColor(Color.white);

            int unShareECount = 0;
            if (presentShareE)
            {
                int shareECount = 0;
                if (shareE_num != null)
                {
                    foreach (int num in shareE_num)
                    {
                        shareECount += num;
                    }
                }
                int elecCount=0;
                for (int i = 0; i < atomInfo.Orbit.Length; i++)
                {
                    elecCount += atomInfo.Orbit[i];
                }
                unShareECount = elecCount - shareECount;
            }

            int orbitCount = 0;
            int shareOrbitCounti = 0;
            int shareOrbitCountj = 0;
            for (int i = 0; i < atomInfo.Orbit.Length; i++)
            {
                for (int j = 0; j < atomInfo.Orbit[i]; j++)
                {
                    GameObject orbitobj = new GameObject();
                    orbitobj.name = "orbit_" + i.ToString() + "_" + j.ToString();
                    orbitobj.transform.parent = displayobj.transform;
                    orbitobj.transform.localPosition = Vector3.zero;
                    orbitobj.transform.localEulerAngles = Vector3.zero;
                    orbitobj.transform.localScale = Vector3.one;
                    Orbit orbitSrp = orbitobj.AddComponent<Orbit>();
                    orbitSrp.sourceAtom = this;
                    orbitSrp.targetAtom = null;

                    float orbitRad = atomInfo.orbitStartDisScale + (float)i * atomInfo.orbitStepDisScale;
                    float elecRad = atomInfo.nucleusRadScale * 0.5f;

                    Material mat = MaterialLib.GetStandardMaterialByColor(atomColor);

                    GameObject electronobj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    {
                        electronobj.name = "electron" + j.ToString();
                        electronobj.transform.parent = orbitobj.transform;
                        electronobj.transform.localPosition = Vector3.right * orbitRad;
                        electronobj.transform.localEulerAngles = Vector3.zero;
                        electronobj.transform.localScale = Vector3.one * elecRad;
                        electronobj.tag = "uncombineable";

                        MeshFilter mf = electronobj.GetComponent<MeshFilter>();
                        mf.sharedMesh = MeshLib.CreateMesh(MeshLib.MeshType.Sphere, level);

                        MeshRenderer meshmrender = electronobj.GetComponent<MeshRenderer>();
                        meshmrender.sharedMaterial = mat;
                        meshmrender.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
#if UNITY_5_3_OR_NEWER
                        meshmrender.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
#endif
                    }

                    GameObject ringobj = GameObject.Instantiate<GameObject>(ringprefab);
                    {
                        ringobj.name = "ring" + i.ToString() + "_" + j.ToString();
                        ringobj.transform.parent = orbitobj.transform;
                        ringobj.transform.localPosition = Vector3.zero;
                        ringobj.transform.localEulerAngles = new Vector3(0, 160.0f, 0);
                        ringobj.transform.localScale = new Vector3(orbitRad / 2.5f * 1.75f, 3.0f, orbitRad / 2.5f * 1.75f);
                        ringobj.tag = "uncombineable";

                        MeshRenderer meshmrender = ringobj.GetComponent<MeshRenderer>();
                        //meshmrender.material.SetColor("_TintColor",atomColor);
                        //meshmrender.sharedMaterial = whitemat;
                        meshmrender.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
#if UNITY_5_3_OR_NEWER
                        meshmrender.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
#endif
                    }

                    if (presentShareE && orbitCount >= unShareECount && shareOrbitCounti < shareE_num.Count)
                    {
                        orbitSrp.targetAtom = shareE_targetAtom[shareOrbitCounti];
                        Vector3 currentDis = electronobj.transform.position - this.transform.position;
                        Vector3 targetDis = orbitSrp.targetAtom.transform.position - this.transform.position;
                        if (currentDis.sqrMagnitude > 0.0f)
                        {
                            float scale = targetDis.magnitude / 2.0f / currentDis.magnitude;
                            orbitobj.transform.localScale *= scale;
                            electronobj.transform.localScale /= scale;
                        }
                        shareOrbitCountj++;
                        if (shareOrbitCountj >= shareE_num[shareOrbitCounti])
                        {
                            shareOrbitCounti++;
                        }
                    }

                    orbitCount++;

                }
            }
        }

        /// <summary>
        /// Static method that sets colors for the temperature coloring method for a list of atoms
        /// by normalizing the temperature values across the list.
        /// </summary>
        /// <param name="atoms">The list of atoms.</param>
        public static void SetBFactorColors(List<Atom> atoms)
        {
            if (atoms.Count == 0) return;

            float minTemperature = atoms[0].temperatureFactor;
            float maxTemperature = atoms[0].temperatureFactor;

            foreach (Atom atom in atoms)
            {
                minTemperature = Math.Min(minTemperature, atom.temperatureFactor);
                maxTemperature = Math.Max(maxTemperature, atom.temperatureFactor);
            }

            float temperatureRange = maxTemperature - minTemperature;

            foreach (Atom atom in atoms)
            {
                float relativeTemperature = temperatureRange == 0.0f ? 0.0f :
                    (atom.temperatureFactor - minTemperature) / temperatureRange;

                if (relativeTemperature < 0.25)
                    atom.temperatureColor = Colors.FromRgb(
                        0, (byte)(255 * (4 * relativeTemperature)), 255);
                else if (relativeTemperature < 0.5)
                    atom.temperatureColor = Colors.FromRgb(
                        0, 255, (byte)(255 * (1 - 4 * (relativeTemperature - 0.25))));
                else if (relativeTemperature < 0.75)
                    atom.temperatureColor = Colors.FromRgb(
                        (byte)(255 * (4 * (relativeTemperature - 0.5))), 255, 0);
                else
                    atom.temperatureColor = Colors.FromRgb(
                        255, (byte)(255 * (1 - 4 * (relativeTemperature - 0.75))), 0);
            }
        }

        /// <summary>
        /// Used by a <see cref="Residue" /> to calculate it's temperature color.
        /// </summary>
        /// <param name="atoms">A list of atoms.</param>
        /// <returns>The average color.</returns>
        public static Color GetAverageTemperateColor(List<Atom> atoms)
        {
            float r = 0.0f, g = 0.0f, b = 0.0f;

            foreach (Atom atom in atoms)
            {
                r += atom.atomColor.r;
                g += atom.atomColor.g;
                b += atom.atomColor.b;
            }

            float fnum = atoms.Count;
            if (atoms.Count <= 0) fnum = 1.0f;

            return new Color(r, g, b) / fnum;
        }

        /// <summary>
        /// Static method to calculate the 3D bounding box for a list of atoms.
        /// </summary>
        /// <param name="atoms">A list of atoms.</param>
        /// <returns>The 3D bounding box.</returns>
        public static Bounds GetBounds(List<Atom> atoms)
        {
            Bounds bound=new Bounds();
            if (atoms.Count == 0) return bound;

            float x1 = atoms[0].position.x;
            float x2 = atoms[0].position.x;
            float y1 = atoms[0].position.y;
            float y2 = atoms[0].position.y;
            float z1 = atoms[0].position.z;
            float z2 = atoms[0].position.z;

            foreach (Atom atom in atoms)
            {
                x1 = Math.Min(x1, atom.position.x);
                x2 = Math.Max(x2, atom.position.x);
                y1 = Math.Min(y1, atom.position.y);
                y2 = Math.Max(y2, atom.position.y);
                z1 = Math.Min(z1, atom.position.z);
                z2 = Math.Max(z2, atom.position.z);
            }

            Vector3 vmin=new Vector3(x1,y1,z1);
            Vector3 vmax = new Vector3(x2, y2, z2);
            Vector3 vszie = vmax - vmin;
            Vector3 vcenter = (vmin + vmax) * 0.5f;

            return new Bounds(vcenter, vszie);
        }

        /// <summary>
        /// Extra initialization for this class and subclasses that can't be done in the
        /// constructor since certain properties are expected to be set.
        /// </summary>
        public virtual void Initialize()
        {
            this.UpdateColorView();
        }








        /// <summary>
        /// Updates the material color for this atom based on the coloring method and the current
        /// hover state.
        /// </summary>
        private void UpdateColorView()
        {
            Color color = this.atomColor;

            if (this.colorScheme == ColorScheme.Structure)
                color = this.structureColor;
            else if (this.colorScheme == ColorScheme.Residue && this.residue != null)
                color = this.residue.ResidueColor;
            else if (this.colorScheme == ColorScheme.Chain && this.residue != null &&
                this.residue.Chain != null)
                color = this.residue.Chain.ChainColor;
            else if (this.colorScheme == ColorScheme.Temperature)
                color = this.temperatureColor;

    
        }
    }
}
