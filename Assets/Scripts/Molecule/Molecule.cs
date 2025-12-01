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
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.InputSystem.Android;


namespace MoleculeLogic
{
    public enum MolType
    { 
        Receptor=0,
        Ligand=1,
        Conformation=2,
    }

    /// <summary>
    /// Represents a molecule and is constructed from a PDB file stream.
    /// </summary>
    /// <remarks>
    /// This class contains references to the other business objects that make up 
    /// a molecule and centralizes the functionality to generate the necessary 3D meshes.
    /// </remarks>
    public class Molecule : MonoBehaviour
    {

        public List<Atom> atoms = new List<Atom>();
        public List<Bond> bonds = new List<Bond>();
        public List<Residue> residues = new List<Residue>();
        public Dictionary<string, Chain> chains = new Dictionary<string, Chain>();
        public List<Structure> structures = new List<Structure>();
        public List<Ribbon> ribbons = new List<Ribbon>();
        public List<Surface> surfaces = new List<Surface>();
        public List<Pocket> pockets;
        public List<Pharma> pharmas;
        public MoleculeScheme scheme = new MoleculeScheme();
        private ColorScheme colorScheme;
        public string molname;
        public MolType type = MolType.Receptor;
        public int partCunnt = 1;

        public GameObject atomgroup;
        public GameObject bondgroup;
        public GameObject chaingroup;
        public GameObject surfacegroup;
        public GameObject pocketgroup;
        public GameObject pharmagroup;

        public delegate void ProgressEventHandler(float progress, string msg);
        public static event ProgressEventHandler ProgressEvent = null;

        public void Build(Transform parent)
        {
            this.atomgroup = new GameObject("atomgroup");
            this.atomgroup.transform.parent = parent;
            this.atomgroup.transform.localPosition = new Vector3(0, 0, 0);
            this.atomgroup.transform.localScale = new Vector3(1, 1, 1);
            this.atomgroup.transform.localEulerAngles = new Vector3(0, 0, 0);
            this.atomgroup.AddComponent<AtomGroup>();
            this.bondgroup = new GameObject("bondgroup");
            this.bondgroup.transform.parent = parent;
            this.bondgroup.transform.localPosition = new Vector3(0, 0, 0);
            this.bondgroup.transform.localScale = new Vector3(1, 1, 1);
            this.bondgroup.transform.localEulerAngles = new Vector3(0, 0, 0);
            this.bondgroup.AddComponent<BondGroup>();
            this.chaingroup = new GameObject("chaingroup");
            this.chaingroup.transform.parent = parent;
            this.chaingroup.transform.localPosition = new Vector3(0, 0, 0);
            this.chaingroup.transform.localScale = new Vector3(1, 1, 1);
            this.chaingroup.transform.localEulerAngles = new Vector3(0, 0, 0);
            this.chaingroup.AddComponent<ChainGroup>();
            this.surfacegroup = new GameObject("surfacegroup");
            this.surfacegroup.transform.parent = parent;
            this.surfacegroup.transform.localPosition = new Vector3(0, 0, 0);
            this.surfacegroup.transform.localScale = new Vector3(1, 1, 1);
            this.surfacegroup.transform.localEulerAngles = new Vector3(0, 0, 0);
            this.surfacegroup.AddComponent<SurfaceGroup>();
            this.pocketgroup = new GameObject("pocketgroup");
            this.pocketgroup.transform.parent = parent;
            this.pocketgroup.transform.localPosition = new Vector3(0, 0, 0);
            this.pocketgroup.transform.localScale = new Vector3(1, 1, 1);
            this.pocketgroup.transform.localEulerAngles = new Vector3(0, 0, 0);
            this.pocketgroup.AddComponent<PocketGroup>();
            this.pharmagroup = new GameObject("pharmagroup");
            this.pharmagroup.transform.parent = parent;
            this.pharmagroup.transform.localPosition = new Vector3(0, 0, 0);
            this.pharmagroup.transform.localScale = new Vector3(1, 1, 1);
            this.pharmagroup.transform.localEulerAngles = new Vector3(0, 0, 0);
            this.pharmagroup.AddComponent<PharmaGroup>();
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
        }

        public async UniTask LoadFromStream(Stream s, string ext)
        {
            if (!Application.isPlaying)
            {
                _LoadFromStreamEditor(s, ext);
            }
            else
            {
                await _LoadFromStreamRuntime(s, ext);
            }
        }

        private bool loaded = false;
        public static event Action<Molecule> OnLoaded = null;

        /// <summary>
        /// Parses a PDB stream and build the constituent objects.
        /// </summary>
        /// <param name="pdbStream">The PDB stream.</param>
        private async UniTask _LoadFromStreamRuntime(Stream stream, string ext)
        {
            GameObject loadingObj = new GameObject("loadingObj");
            loadingObj.transform.parent = transform.parent;
            loadingObj.transform.localPosition = Vector3.zero;
            loadingObj.transform.localEulerAngles = Vector3.zero;
            loadingObj.transform.localScale = Vector3.one;
            LoadingTab loadingTab = loadingObj.AddComponent<LoadingTab>();
            loadingTab.Create("Loading Molecule...", Vector3.zero);

            if (ProgressEvent != null) ProgressEvent(0.0f, "LoadFromStreamRuntime");
            await UniTask.NextFrame();
            float t1 = Time.time;
            
            await MoleculeProgressiveLoader.LoadFromStream(this, stream, ext);

            if (ProgressEvent != null) ProgressEvent(40.0f, "LoadFromStreamRuntime");
            await UniTask.NextFrame();
            float t2 = Time.time;

            this.CreateBackbone();
            await CreateBondsProgressively();

            if (ProgressEvent != null) ProgressEvent(60.0f, "LoadFromStreamRuntime");
            await UniTask.NextFrame();
            float t3 = Time.time;

            this.CreateChains();
            await CreateResiduesProgressively();
            Atom.SetBFactorColors(this.atoms);
            this.SetStructureInfo();
            this.CreateRibbons();
            this.CreateSurfaces();

            if (ProgressEvent != null) ProgressEvent(80.0f, "LoadFromStreamRuntime");
            await UniTask.NextFrame();
            float t4 = Time.time;

            foreach (Atom atom in this.atoms)
            {
                atom.Initialize();
            }

            if (ProgressEvent != null) ProgressEvent(100.0f, "LoadFromStreamRuntime");
            await UniTask.NextFrame();
            float t5 = Time.time;

            GameObject.Destroy(loadingObj);
            loaded = true;
            if (OnLoaded!=null)
                OnLoaded(this);

            float dt1 = t2 - t1;
            float dt2 = t3 - t2;
            float dt3 = t4 - t3;
            float dt4 = t5 - t4;
            float dt = t5 - t1;
            LogTrace.Trace.TraceLn("mol:" + molname + " loaded!,cost time=" + dt.ToString("F2") + "s," +
                dt1.ToString("F2") + "," + dt2.ToString("F2") + "," + dt3.ToString("F2") + "," + dt4.ToString("F2"));

            this.Represent();


        }

        public void WritePdbFile(string pdbPath)
        {
            using (var writer = new StreamWriter(pdbPath))
            {
                Dictionary<string, int> counts = new Dictionary<string, int>();

                for (int i = 0; i < atoms.Count; i++)
                {
                    var atom = atoms[i];
                    int atomNum = i + 1;

                    if (counts.ContainsKey(atom.atomName))
                        counts[atom.atomName]++;
                    else
                        counts.Add(atom.atomName, 0);
                    int index = counts[atom.atomName]+1;

                    // 严格按PDB固定列格式拼接行（关键：补空格保证列对齐）
                    var hetatmLine = string.Format(
                        "ATOM  {0,5}  {1,-4}UNL A{2,4}    {3,8:F3}{4,8:F3}{5,8:F3}  0.00  0.00           {6,-2}",
                        atomNum,                  // 7-11列：原子序号
                        atom.atomName+index.ToString(),              // 13-16列：原子名称
                        "",                        // 22-26列：残基序号
                        atom.position.x,                   // 31-38列：X坐标（保留3位小数）
                        atom.position.y,                   // 39-46列：Y坐标
                        atom.position.z,                   // 47-54列：Z坐标
                        atom.atomName == "O" ?"OA":atom.atomName               // 77-78列：元素符号
                    );

                    writer.WriteLine(hetatmLine);
                }
                writer.WriteLine("END"); // PDB文件结尾标记
                writer.Close();
                writer.Dispose();
            }
        }

        /// <summary>
        /// Parses a PDB stream and build the constituent objects.
        /// </summary>
        /// <param name="pdbStream">The PDB stream.</param>
        private void _LoadFromStreamEditor(Stream stream, string ext)
        {
            MoleculeDirectLoader.LoadFromStream(this, stream, ext);
            this.CreateBackbone();
            this.CreateBondsImmediately();
            this.CreateChains();
            this.CreateResiduesImmediately();
            Atom.SetBFactorColors(this.atoms);
            this.SetStructureInfo();
            this.CreateRibbons();
            this.CreateSurfaces();

            foreach (Atom atom in this.atoms)
            {
                atom.Initialize();
            }

            loaded = true;
            if (OnLoaded != null)
                OnLoaded(this);

            this.Represent();
        }

        public Vector3 minAtomPosition;
        public Vector3 maxAtomPosition;
        public void CreateSurfaces()
        {
            Bounds b = this.GetBound();
            minAtomPosition = b.min;
            maxAtomPosition = b.max;

            Surface sur = Surface.CreateSurface(this, this.surfacegroup.transform);
            this.surfaces.Add(sur);
        }

        private bool representing = false;

        public async void Represent()
        {
            if (!loaded)
                return;

            Bounds b = GetBound();
            Vector3 vsize = b.size;
            //float initscale = 1.0f / Mathf.Max(vsize.x, vsize.y, vsize.z);
            float initscale = 1.0f / vsize.magnitude;

            atomgroup.transform.localPosition = -b.center * initscale;
            bondgroup.transform.localPosition = -b.center * initscale;
            chaingroup.transform.localPosition = -b.center * initscale;
            surfacegroup.transform.localPosition = -b.center * initscale;
            pocketgroup.transform.localPosition = -b.center * initscale;
            pharmagroup.transform.localPosition = -b.center * initscale;
            atomgroup.transform.localScale = new Vector3(initscale, initscale, initscale);
            bondgroup.transform.localScale = new Vector3(initscale, initscale, initscale);
            chaingroup.transform.localScale = new Vector3(initscale, initscale, initscale);
            surfacegroup.transform.localScale = new Vector3(initscale, initscale, initscale);
            pocketgroup.transform.localScale = new Vector3(initscale, initscale, initscale);
            pharmagroup.transform.localScale = new Vector3(initscale, initscale, initscale);

            int level = 0;
            if (atoms.Count < 100)
                level = 2;
            else if (atoms.Count < 1000)
                level = 1;
            else
                level = 0;

            if (!Application.isPlaying)
            {
                RepresentImmediately(level);
            }
            else
            {
                if (representing) StopAllCoroutines();
                await RepresentProgressively(level, representing);
            }
        }

        private void RepresentImmediately(int level)
        {
            representing = true;

            GameObject[] groupobjs = new GameObject[] { atomgroup, bondgroup, chaingroup, surfacegroup, pocketgroup, pharmagroup };
            foreach (GameObject groupobj in groupobjs)
            {
                Combineable combinemono = groupobj.GetComponent<Combineable>();
                bool dirty = combinemono.IsDirty(scheme);
                int breathinterval = combinemono.GetBreathInterval();
                int combineinterval = combinemono.GetCombineInterval();

                if (dirty)
                {
                    combinemono.PreProcess();
                }
                Representable[] elements = combinemono.GetComponentsInChildren<Representable>();
                if (elements != null)
                {
                    for (int i = 0; i < elements.Length; i++)
                    {
                        Representable element = elements[i];
                        element.Represent(scheme, type, dirty, level);
                        if (dirty)
                        {
                            if (i > 0 && i % combineinterval == 0 && scheme.Combineable)
                                combinemono.CombineImmediately();
                        }
                    }
                    if (dirty && scheme.Combineable)
                        combinemono.CombineImmediately();
                }
                if (dirty)
                {
                    combinemono.PostProcess();
                }
                combinemono.SetDirty(scheme);
            }

            Resources.UnloadUnusedAssets();
            GC.Collect();

            representing = false;
        }


        private async UniTask RepresentProgressively(int level, bool forcedirty)
        {
            float t1 = Time.time;
            representing = true;

            GameObject[] groupobjs = new GameObject[] { atomgroup, bondgroup, chaingroup, surfacegroup, pocketgroup, pharmagroup };

            foreach (GameObject groupobj in groupobjs)
            {
                Combineable combinemono = groupobj.GetComponent<Combineable>();
                bool dirty = combinemono.IsDirty(scheme) || forcedirty;
                if (dirty)
                {
                    combinemono.PreProcess();
                }
            }

            foreach (GameObject groupobj in groupobjs)
            {
                Combineable combinemono = groupobj.GetComponent<Combineable>();
                bool dirty = combinemono.IsDirty(scheme) || forcedirty;
                int breathinterval = combinemono.GetBreathInterval();
                int combineinterval = combinemono.GetCombineInterval();

                Representable[] elements = combinemono.GetComponentsInChildren<Representable>();
                if (elements != null)
                {
                    for (int i = 0; i < elements.Length; i++)
                    {
                        Representable element = elements[i];
                        element.Represent(scheme, type, dirty, level);
                        if (dirty)
                        {
                            if (i % breathinterval == 0)
                                await UniTask.NextFrame();
                            if (i > 0 && i % combineinterval == 0 && scheme.Combineable)
                                await combinemono.CombineProgressively();
                        }
                    }
                    if (dirty && scheme.Combineable)
                    {
                        await combinemono.CombineProgressively();
                    }
                }
            }

            foreach (GameObject groupobj in groupobjs)
            {
                Combineable combinemono = groupobj.GetComponent<Combineable>();
                bool dirty = combinemono.IsDirty(scheme) || forcedirty;
                if (dirty)
                {
                    combinemono.PostProcess();
                }
                combinemono.SetDirty(scheme);
            }

            Resources.UnloadUnusedAssets();
            GC.Collect();

            representing = false;
            float t2 = Time.time;
            float dt = t2 - t1;
            LogTrace.Trace.TraceLn("mol:" + molname + " represented!,cost time=" + dt.ToString("F2") + "s");
        }





        /// <summary>
        /// The current molecule coloring method.
        /// </summary>
        public ColorScheme ColorScheme
        {
            get { return this.colorScheme; }
            set
            {
                if (this.colorScheme != value)
                {
                    this.colorScheme = value;

                    foreach (Residue residue in this.residues)
                        residue.ColorScheme = this.colorScheme;
                    foreach (Atom atom in this.atoms)
                        atom.ColorScheme = this.colorScheme;
                }
            }
        }

        public Bounds GetBound()
        {

            Vector3 vmin = new Vector3(99999.0f, 99999.0f, 99999.0f);
            Vector3 vmax = new Vector3(-99999.0f, -99999.0f, -99999.0f); ;
            for (int i = 0; i < atoms.Count; i++)
            {
                Vector3 pos = atoms[i].position;
                if (pos.x < vmin.x) vmin.x = pos.x;
                if (pos.y < vmin.y) vmin.y = pos.y;
                if (pos.z < vmin.z) vmin.z = pos.z;
                if (pos.x > vmax.x) vmax.x = pos.x;
                if (pos.y > vmax.y) vmax.y = pos.y;
                if (pos.z > vmax.z) vmax.z = pos.z;
            }
            Vector3 vcenter = (vmin + vmax) * 0.5f;
            Vector3 vsize = vmax - vmin;

            Bounds b = new Bounds(vcenter, vsize);
            return b;
        }




        /// <summary>
        /// Called by the contructor after creating the <see cref="Atom"/> objects to identify the
        /// backbone atoms and connect them via referneces.
        /// </summary>
        private void CreateBackbone()
        {
            if (atoms.Count <= 0)
                return;

            CAlpha previousCAlpha = null;

            foreach (Atom atom in this.atoms)
            {
                CAlpha nextCAlpha = atom as CAlpha;

                if (nextCAlpha != null)
                {
                    if (previousCAlpha != null &&
                        nextCAlpha.chainIdentifier == previousCAlpha.chainIdentifier)
                    {
                        previousCAlpha.NextCAlpha = nextCAlpha;
                        nextCAlpha.PreviousCAlpha = previousCAlpha;

                        float distance = (nextCAlpha.position - previousCAlpha.position).magnitude;

                        if (distance * distance < 3.6f && previousCAlpha.partIndex == nextCAlpha.partIndex)
                        {
                            if (!(previousCAlpha.bonds.ContainsKey(nextCAlpha) || nextCAlpha.bonds.ContainsKey(previousCAlpha)))
                            {
                                Bond backbone = Bond.CreateBond(this, previousCAlpha, nextCAlpha, -1, bonds.Count, this.bondgroup.transform);
                                bonds.Add(backbone);
                            }
                        }
                    }

                    previousCAlpha = nextCAlpha;
                }
            }
        }

        static float bondLengthMin = 0.4f;
	    static float bondLengthMax = 1.9f;		//	角氰栏肺 舅酒晨.

	    static float bondVanDerWaalsMin = 0.4f;
        static float bondVanDerWaalsMax = 1.2f;

        /// <summary>
        /// Called by the contructor after creating the <see cref="Atom"/> objects to identify
        /// covalently bonded atoms. Uses a simple distance heuristic of six angstroms.
        /// </summary>
        private void CreateBondsImmediately()
        {
            if (atoms.Count <= 0)
                return;

            for (int i = 0; i < this.atoms.Count - 1; i++)
            {
                Atom atom1 = this.atoms[i];

                if (atom1 is Water) continue;

                for (int j = i + 1; j < this.atoms.Count; j++)
                {
                    Atom atom2 = this.atoms[j];

                    if (atom2 is Water) continue;

                    float distance = (atom1.position - atom2.position).magnitude;
                    float fmin = bondLengthMin;
                    float fmax = bondLengthMax;
                    if (atom1.atomName.StartsWith("H") && atom2.atomName.StartsWith("H"))
                    {
                        fmin = bondVanDerWaalsMin;
                        fmax = bondVanDerWaalsMax;
                    }
                    if (distance > fmin && distance < fmax && atom1.partIndex == atom2.partIndex)
                    {
                        if (!(atom1.bonds.ContainsKey(atom2) || atom2.bonds.ContainsKey(atom1)))
                        {
                            Bond b = Bond.CreateBond(this, atom1, atom2, -1, bonds.Count, this.bondgroup.transform);
                            bonds.Add(b);
                        }
                    }
                }
            }

            Atom.SetBFactorColors(this.atoms);
        }

        /// <summary>
        /// Called by the contructor after creating the <see cref="Atom"/> objects to identify
        /// covalently bonded atoms. Uses a simple distance heuristic of six angstroms.
        /// </summary>
        private async UniTask CreateBondsProgressively()
        {
            if (atoms.Count <= 0) 
                return;

            //float bondDis = 3.6f;
            float bondDis = bondLengthMax;
            int[,] searchIndex = new int[,] { { 0, 0, 1 }, { 0, 1, 0 }, { 0, 1, 1 }, { 0, -1, 1 }, { 1, -1, -1 }, { 1, -1, 0 }, { 1, -1, 1 }, { 1, 0, -1 }, { 1, 0, 0 }, { 1, 0, 1 }, { 1, 1, -1 }, { 1, 1, 0 }, { 1, 1, 1 } };
            Vector3 minPos = atoms[0].position;
            Vector3 maxPos = atoms[0].position;

            for (int i = 0; i < this.atoms.Count; i++)
            {
                Atom atom = atoms[i];
                minPos.x = atom.position.x < minPos.x ? atom.position.x : minPos.x;
                minPos.y = atom.position.y < minPos.y ? atom.position.y : minPos.y;
                minPos.z = atom.position.z < minPos.z ? atom.position.z : minPos.z;
                maxPos.x = atom.position.x > maxPos.x ? atom.position.x : maxPos.x;
                maxPos.y = atom.position.y > maxPos.y ? atom.position.y : maxPos.y;
                maxPos.z = atom.position.z > maxPos.z ? atom.position.z : maxPos.z;
            }


            int X = (int)(((maxPos.x - minPos.x) / bondDis) + 1);
            int Y = (int)(((maxPos.y - minPos.y) / bondDis) + 1);
            int Z = (int)(((maxPos.z - minPos.z) / bondDis) + 1);
            List<int>[, ,] atomGrids = new List<int>[X, Y, Z];

            //Debug.Log("grid:" + X + "," + Y + "," + Z);

            for (int i = 0; i < this.atoms.Count; i++)
            {
                Atom atom = atoms[i];
                if (atom is Water) continue;
                Vector3 pos = atom.position;
                int xIndex = (int)((pos.x - minPos.x) / bondDis);
                int yIndex = (int)((pos.y - minPos.y) / bondDis);
                int zIndex = (int)((pos.z - minPos.z) / bondDis);
                if (xIndex < 0)
                {
                    xIndex = 0;
                    //Debug.Log("atom grids error. Atom pos" + pos + " Bounds" + minPos + maxPos);
                }
                if (xIndex > X - 1)
                {
                    xIndex = X - 1;
                    //Debug.Log("atom grids error. Atom pos" + pos + " Bounds" + minPos + maxPos);
                }
                if (yIndex < 0)
                {
                    yIndex = 0;
                    //Debug.Log("atom grids error. Atom pos" + pos + " Bounds" + minPos + maxPos);
                }
                if (yIndex > Y - 1)
                {
                    yIndex = Y - 1;
                    //Debug.Log("atom grids error. Atom pos" + pos + " Bounds" + minPos + maxPos);
                }
                if (zIndex < 0)
                {
                    zIndex = 0;
                    //Debug.Log("atom grids error. Atom pos" + pos + " Bounds" + minPos + maxPos);
                }
                if (zIndex > Z - 1)
                {
                    zIndex = Z - 1;
                    //Debug.Log("atom grids error. Atom pos" + pos + " Bounds" + minPos + maxPos);
                }

                if (atomGrids[xIndex, yIndex, zIndex] == null)
                {
                    atomGrids[xIndex, yIndex, zIndex] = new List<int>();
                }
                atomGrids[xIndex, yIndex, zIndex].Add(i);

                if (xIndex > 0)
                {
                    if (atomGrids[xIndex - 1, yIndex, zIndex] == null)
                    {
                        atomGrids[xIndex - 1, yIndex, zIndex] = new List<int>();
                    }
                    atomGrids[xIndex-1, yIndex, zIndex].Add(i);
                }
                if (yIndex > 0)
                {
                    if (atomGrids[xIndex, yIndex - 1, zIndex] == null)
                    {
                        atomGrids[xIndex, yIndex - 1, zIndex] = new List<int>();
                    }
                    atomGrids[xIndex, yIndex-1, zIndex].Add(i);
                }
                if (xIndex > 0 && yIndex>0)
                {
                    if (atomGrids[xIndex - 1, yIndex - 1, zIndex] == null)
                    {
                        atomGrids[xIndex - 1, yIndex - 1, zIndex] = new List<int>();
                    }
                    atomGrids[xIndex - 1, yIndex-1, zIndex].Add(i);
                }
                if (zIndex > 0)
                {
                    if (atomGrids[xIndex, yIndex, zIndex - 1] == null)
                    {
                        atomGrids[xIndex, yIndex, zIndex - 1] = new List<int>();
                    }
                    atomGrids[xIndex, yIndex, zIndex-1].Add(i);
                }
                if (zIndex > 0 && xIndex > 0)
                {
                    if (atomGrids[xIndex - 1, yIndex, zIndex - 1] == null)
                    {
                        atomGrids[xIndex - 1, yIndex, zIndex - 1] = new List<int>();
                    }
                    atomGrids[xIndex - 1, yIndex, zIndex-1].Add(i);
                }
                if (zIndex > 0 && yIndex > 0)
                {
                    if (atomGrids[xIndex, yIndex - 1, zIndex - 1] == null)
                    {
                        atomGrids[xIndex, yIndex - 1, zIndex - 1] = new List<int>();
                    }
                    atomGrids[xIndex, yIndex - 1, zIndex-1].Add(i);
                }
                if (zIndex > 0 && xIndex > 0 && yIndex > 0)
                {
                    if (atomGrids[xIndex - 1, yIndex - 1, zIndex - 1] == null)
                    {
                        atomGrids[xIndex - 1, yIndex - 1, zIndex - 1] = new List<int>();
                    }
                    atomGrids[xIndex - 1, yIndex - 1, zIndex-1].Add(i);
                }
            }

            for (int i = 0; i < X; i++)
                for (int j = 0; j < Y; j++)
                    for (int k = 0; k < Z; k++)
                    {
                        if (atomGrids[i, j, k] == null) continue;
                        List<int> grid = atomGrids[i, j, k];
                        for (int ai = 0; ai < grid.Count - 1; ai++)
                        {
                            Atom atomi = this.atoms[grid[ai]];
                            for (int aj = ai + 1; aj < grid.Count; aj++)
                            {
                                Atom atomj = this.atoms[grid[aj]];
                                float distance = (atomi.position - atomj.position).magnitude;
                                float fmin = bondLengthMin;
                                float fmax = bondLengthMax;
                                if (atomi.atomName.StartsWith("H") && atomj.atomName.StartsWith("H"))
                                {
                                    fmin = bondVanDerWaalsMin;
                                    fmax = bondVanDerWaalsMax;
                                }
                                if (distance > fmin && distance < fmax && atomi.partIndex == atomj.partIndex)
                                {
                                    if (!(atomi.bonds.ContainsKey(atomj) || atomj.bonds.ContainsKey(atomi)))
                                    {
                                        Bond bone = Bond.CreateBond(this, atomi, atomj, -1, bonds.Count, this.bondgroup.transform);
                                        bonds.Add(bone);
                                        if (bonds.Count > 0 && (bonds.Count % 50 == 0))
                                            await UniTask.NextFrame();
                                    }
                                }
                            }
                        }

                    }

            atomGrids = null;

            Atom.SetBFactorColors(this.atoms);

        }


        private async UniTask CreateBondsProgressively_old()
        {
            if (atoms.Count <= 0)
                return;

            float bondDis = 3.6f;
            int[,] searchIndex = new int[,] { { 0, 0, 1 }, { 0, 1, 0 }, { 0, 1, 1 }, { 0, -1, 1 }, { 1, -1, -1 }, { 1, -1, 0 }, { 1, -1, 1 }, { 1, 0, -1 }, { 1, 0, 0 }, { 1, 0, 1 }, { 1, 1, -1 }, { 1, 1, 0 }, { 1, 1, 1 } };
            Vector3 minPos = atoms[0].position;
            Vector3 maxPos = atoms[0].position;

            for (int i = 0; i < this.atoms.Count; i++)
            {
                Atom atom = atoms[i];
                minPos.x = atom.position.x < minPos.x ? atom.position.x : minPos.x;
                minPos.y = atom.position.y < minPos.y ? atom.position.y : minPos.y;
                minPos.z = atom.position.z < minPos.z ? atom.position.z : minPos.z;
                maxPos.x = atom.position.x > maxPos.x ? atom.position.x : maxPos.x;
                maxPos.y = atom.position.y > maxPos.y ? atom.position.y : maxPos.y;
                maxPos.z = atom.position.z > maxPos.z ? atom.position.z : maxPos.z;
            }


            int X = (int)(((maxPos.x - minPos.x) / bondDis) + 1);
            int Y = (int)(((maxPos.y - minPos.y) / bondDis) + 1);
            int Z = (int)(((maxPos.z - minPos.z) / bondDis) + 1);
            List<int>[, ,] atomGrids = new List<int>[X, Y, Z];

            //Debug.Log("grid:" + X + "," + Y + "," + Z);

            for (int i = 0; i < this.atoms.Count; i++)
            {
                Atom atom = atoms[i];
                if (atom is Water) continue;
                Vector3 pos = atom.position;
                int xIndex = (int)((pos.x - minPos.x) / bondDis);
                int yIndex = (int)((pos.y - minPos.y) / bondDis);
                int zIndex = (int)((pos.z - minPos.z) / bondDis);
                if (xIndex < 0)
                {
                    xIndex = 0;
                    //Debug.Log("atom grids error. Atom pos" + pos + " Bounds" + minPos + maxPos);
                }
                if (xIndex > X - 1)
                {
                    xIndex = X - 1;
                    //Debug.Log("atom grids error. Atom pos" + pos + " Bounds" + minPos + maxPos);
                }
                if (yIndex < 0)
                {
                    yIndex = 0;
                    //Debug.Log("atom grids error. Atom pos" + pos + " Bounds" + minPos + maxPos);
                }
                if (yIndex > Y - 1)
                {
                    yIndex = Y - 1;
                    //Debug.Log("atom grids error. Atom pos" + pos + " Bounds" + minPos + maxPos);
                }
                if (zIndex < 0)
                {
                    zIndex = 0;
                    //Debug.Log("atom grids error. Atom pos" + pos + " Bounds" + minPos + maxPos);
                }
                if (zIndex > Z - 1)
                {
                    zIndex = Z - 1;
                    //Debug.Log("atom grids error. Atom pos" + pos + " Bounds" + minPos + maxPos);
                }

                if (atomGrids[xIndex, yIndex, zIndex] == null)
                {
                    atomGrids[xIndex, yIndex, zIndex] = new List<int>();
                }
                atomGrids[xIndex, yIndex, zIndex].Add(i);
            }

            for (int i = 0; i < X; i++)
                for (int j = 0; j < Y; j++)
                    for (int k = 0; k < Z; k++)
                    {
                        if (atomGrids[i, j, k] == null) continue;
                        List<int> grid = atomGrids[i, j, k];
                        for (int ai = 0; ai < grid.Count - 1; ai++)
                        {
                            Atom atomi = this.atoms[grid[ai]];
                            for (int aj = ai + 1; aj < grid.Count; aj++)
                            {
                                Atom atomj = this.atoms[grid[aj]];
                                float distance = (atomi.position - atomj.position).magnitude;
                                float fmin = bondLengthMin;
                                float fmax = bondLengthMax;
                                if (atomi.atomName.StartsWith("H") && atomj.atomName.StartsWith("H"))
                                {
                                    fmin = bondVanDerWaalsMin;
                                    fmax = bondVanDerWaalsMax;
                                }
                                if (distance > fmin && distance < fmax && atomi.partIndex == atomj.partIndex)
                                {
                                    if (!(atomi.bonds.ContainsKey(atomj) || atomj.bonds.ContainsKey(atomi)))
                                    {
                                        Bond bone = Bond.CreateBond(this, atomi, atomj, -1, bonds.Count, this.bondgroup.transform);
                                        bonds.Add(bone);
                                        if (bonds.Count > 0 && (bonds.Count % 50 == 0))
                                            await UniTask.NextFrame();
                                    }
                                }
                            }
                        }

                        for (int gi = 0; gi < grid.Count; gi++)
                        {
                            Atom atomi = this.atoms[grid[gi]];
                            for (int sj = 0; sj < searchIndex.Length / 3; sj++)
                            {
                                int i1 = i + searchIndex[sj, 0];
                                int j1 = j + searchIndex[sj, 1];
                                int k1 = k + searchIndex[sj, 2];
                                if (i1 >= X || j1 >= Y || k1 >= Z || i1 < 0 || j1 < 0 || k1 < 0)
                                {
                                    continue;
                                }
                                List<int> gridj = atomGrids[i1, j1, k1];
                                if (gridj == null)
                                {
                                    continue;
                                }
                                for (int gj = 0; gj < gridj.Count; gj++)
                                {
                                    Atom atomj = this.atoms[gridj[gj]];
                                    float distance = (atomi.position - atomj.position).magnitude;
                                    float fmin = bondLengthMin;
                                    float fmax = bondLengthMax;
                                    if (atomi.atomName.StartsWith("H") && atomj.atomName.StartsWith("H"))
                                    {
                                        fmin = bondVanDerWaalsMin;
                                        fmax = bondVanDerWaalsMax;
                                    }
                                    if (distance > fmin && distance < fmax && atomi.partIndex == atomj.partIndex)
                                    {
                                        if (!(atomi.bonds.ContainsKey(atomj) || atomj.bonds.ContainsKey(atomi)))
                                        {
                                            Bond bone = Bond.CreateBond(this, atomi, atomj, -1, bonds.Count, this.bondgroup.transform);
                                            bonds.Add(bone);
                                            if (bonds.Count > 0 && (bonds.Count % 50 == 0))
                                                await UniTask.NextFrame();
                                        }
                                    }
                                }
                            }
                        }

                    }

            atomGrids = null;

            //for (int i = 0; i < this.atoms.Count - 1; i++)
            //{
            //    Atom atom1 = this.atoms[i];

            //    if (atom1 is Water) continue;

            //    for (int j = i + 1; j < this.atoms.Count; j++)
            //    {
            //        Atom atom2 = this.atoms[j];

            //        if (atom2 is Water) continue;

            //        float sqrdistance = (atom1.Position - atom2.Position).sqrMagnitude;

            //        if (sqrdistance < 3.6f)
            //        {
            //            if (!(atom1.bonds.ContainsKey(atom2) || atom2.bonds.ContainsKey(atom1)))
            //            {
            //                Bond b = Bond.CreateBond(this, atom1, atom2, -1, bonds.Count, this.bondgroup.transform);
            //                bonds.Add(b);
            //            }
            //        }
            //    }

            //    if (i > 0 && (i % 10 == 0)) yield return waitframe;
            //}

            Atom.SetBFactorColors(this.atoms);

        }

        /// <summary>
        /// Called by the constructor to create <see cref="Residue"/> objects and group their
        /// constituent atoms.
        /// </summary>
        private void CreateResiduesImmediately()
        {
            if (atoms.Count <= 0) 
                return;

            foreach (Atom atom in this.atoms)
            {
                Chain chain = atom.chain;

                Residue residue = null;
                if (chain.residues.ContainsKey(atom.residueSequenceNumber))
                {
                    residue = chain.residues[atom.residueSequenceNumber];
                }
                else
                {
                    residue = Residue.CreateResidue(this, atom.residueSequenceNumber, atom.residueName, atom.chainIdentifier,
                        residues.Count, chain.transform);
                    chain.residues.Add(atom.residueSequenceNumber, residue);
                    this.residues.Add(residue);
                }
                residue.Chain = chain;

                residue.Atoms.Add(atom);
                atom.residue = residue;
            }
        }

        /// <summary>
        /// Called by the constructor to create <see cref="Residue"/> objects and group their
        /// constituent atoms.
        /// </summary>
        private async UniTask CreateResiduesProgressively()
        {
            if (atoms.Count <= 0)
                return;

            for (int i = 0; i < atoms.Count; i++)
            {
                Atom atom = atoms[i];
                Chain chain = atom.chain;

                Residue residue = null;
                if (chain.residues.ContainsKey(atom.residueSequenceNumber))
                {
                    residue = chain.residues[atom.residueSequenceNumber];
                }
                else
                {
                    residue = Residue.CreateResidue(this, atom.residueSequenceNumber, atom.residueName, atom.chainIdentifier,
                        residues.Count, chain.transform);
                    chain.residues.Add(atom.residueSequenceNumber, residue);
                    this.residues.Add(residue);
                }
                residue.Chain = chain;

                residue.Atoms.Add(atom);
                atom.residue = residue;

                if (i > 0 && (i % 100 == 0)) 
                    await UniTask.NextFrame();
            }

        }

        /// <summary>
        /// Called by the constructor to create <see cref="Chain"/> objects and group their
        /// constituent residues (amino acids).
        /// </summary>
        private void CreateChains()
        {
            //Chain chain = null;
            //Chain waters = null;

            //foreach (Residue residue in this.residues)
            //{
            //    if (residue.ChainIdentifier == "")
            //    {
            //        if (waters == null) waters = new Chain("");

            //        waters.Residues.Add(residue);
            //        residue.Chain = waters;
            //    }
            //    else
            //    {
            //        if (chain == null || residue.ChainIdentifier != chain.ChainIdentifier)
            //        {
            //            chain = new Chain(residue.ChainIdentifier);
            //            this.chains.Add(chain);
            //        }

            //        chain.Residues.Add(residue);
            //        residue.Chain = chain;
            //    }
            //}

            //if (waters != null) this.chains.Add(waters);

            //Chain.SetChainColors(this.chains);


            foreach (Atom atom in this.atoms)
            {
                string chainIdentifier = atom.chainIdentifier;
                Chain chain = null;
                if (chains.ContainsKey(chainIdentifier))
                {
                    chain = chains[chainIdentifier];
                }
                else
                {
                    chain = Chain.CreateChain(this, atom.chainIdentifier, this.chains.Count, this.chaingroup.transform);
                    chains.Add(chainIdentifier, chain);
                }
                atom.chain = chain;
            }

            Chain.SetChainColors(this.chains);

        }



        /// <summary>
        /// Called by the constructor to set secondary structure related properties on the residues
        /// and atoms that compose each structure.
        /// </summary>
        private void SetStructureInfo()
        {
            foreach (Atom atom in this.atoms)
            {
                if (atom is ChainAtom)
                {
                    atom.structureColor = Color.gray;
                }
            }

            Dictionary<int, int> residue_Number_index = new Dictionary<int, int>();
            for (int i = 0; i < residues.Count; i++)
            {
                Residue r = residues[i];
                float percent = (float)i / (float)residues.Count;
#if UNITY_5_3_OR_NEWER
                r.StructureColor = Color.HSVToRGB(percent, 1.0f, 1.0f);
#endif
                residue_Number_index[r.ResidueSequenceNumber] = i;
            }

            foreach (Residue residue in this.residues)
            {
                foreach (Structure structure in this.structures)
                {
                    if (residue.ChainIdentifier == structure.ChainIdentifier &&
                        residue.ResidueSequenceNumber >= structure.StartResidueSequenceNumber &&
                        residue.ResidueSequenceNumber <= structure.EndResidueSequenceNumber)
                    {
                        if (structure is Sheet) residue.IsSheet = true;
                        else if (structure is Helix) residue.IsHelix = true;

                        residue.StructureColor = residues[residue_Number_index[structure.StartResidueSequenceNumber]].StructureColor;

                        foreach (Atom atom in residue.Atoms)
                            atom.structureColor = structure.Color;

                        break;
                    }
                }
            }
            residue_Number_index.Clear();
            Residue previousResidue = null;

            foreach (Residue residue in this.residues)
            {
                CAlpha cAlpha = null;
                ChainAtom carbonylOxygen = null;

                foreach (Atom atom in residue.Atoms)
                    if (atom is CAlpha)
                        cAlpha = (CAlpha)atom;

                if (cAlpha != null)
                {
                    foreach (Atom atom in residue.Atoms)
                    {
                        if (atom is ChainAtom && atom.atomName == "O")
                            carbonylOxygen = (ChainAtom)atom;
                    }
                }

                if (cAlpha == null || carbonylOxygen == null)
                {
                    if (previousResidue != null)
                    {
                        previousResidue.IsStructureEnd = true;
                        previousResidue = null;
                    }

                    continue;
                }
                else
                {
                    residue.CAlphaPosition = cAlpha.position;
                    residue.CarbonylOxygenPosition = carbonylOxygen.position;
                }

                if (previousResidue != null && previousResidue.Chain != residue.Chain)
                {
                    previousResidue.IsStructureEnd = true;
                    previousResidue = null;
                }

                if (previousResidue != null)
                {
                    previousResidue.NextResidue = residue;
                    residue.PreviousResidue = previousResidue;

                    if (previousResidue.Chain != residue.Chain ||
                        previousResidue.IsSheet != residue.IsSheet ||
                        previousResidue.IsHelix != residue.IsHelix)
                    {
                        previousResidue.IsStructureEnd = true;
                        residue.IsStructureStart = true;
                    }
                }
                else
                {
                    residue.IsStructureStart = true;
                }

                previousResidue = residue;
            }

            if (previousResidue != null)
                previousResidue.IsStructureEnd = true;
        }

        /// <summary>
        /// Called by the constructor to create <see cref="Ribbon"/> objects which are used to
        /// compute the spline curves for secondary struction representations.
        /// </summary>
        private void CreateRibbons()
        {
            Ribbon currentRibbon = null;
            Residue previousResidue = null;

            foreach (Residue residue in this.residues)
            {
                if (residue.CAlphaPosition == null)
                {
                    currentRibbon = null;
                }
                else
                {
                    if (currentRibbon == null ||
                        residue.ChainIdentifier != previousResidue.ChainIdentifier)
                    {
                        currentRibbon = new Ribbon();
                        this.ribbons.Add(currentRibbon);
                    }

                    residue.Ribbon = currentRibbon;
                    currentRibbon.Residues.Add(residue);

                    previousResidue = residue;
                }
            }

            foreach (Ribbon ribbon in this.ribbons)
            {
                ribbon.CreateControlPoints();
            }
        }

        public void InitDefaultScheme()
        {
            scheme.showparts.Clear();
            if (type == MolType.Conformation)
            {
                scheme.showparts.Add(true);
                for (int i = 1; i < partCunnt; i++)
                {
                    if (i==1)
                        scheme.showparts.Add(true);
                    else
                        scheme.showparts.Add(false);
                }
            }
            else
            {
                for (int i = 0; i < partCunnt; i++)
                    scheme.showparts.Add(true);
            }
        }

        public static List<string> opinions = null;
        public void OnUILayout()
        {
            Molecule mol = this;

            int oldsel = mol.scheme.style;
            if (opinions == null)
            {
                opinions = new List<string>();
                int maxsel = (int)MoleculeStyle.Max;
                for (int i = 0; i < maxsel; i++)
                {
                    MoleculeStyle style = (MoleculeStyle)i;
                    opinions.Add(style.ToString());
                }
            }

            int newsel = GUILayout.SelectionGrid(oldsel, opinions.ToArray(), 3);
            if (newsel != oldsel)
            {
                mol.scheme.style = newsel;
                mol.Represent();
            }

            GUILayout.BeginHorizontal();

            bool newshowText = GUILayout.Toggle(mol.scheme.showtext != 0, "Text");
            if (newshowText != (mol.scheme.showtext != 0))
            {
                mol.scheme.showtext = newshowText ? 1 : 0;
                mol.Represent();
            }

            //bool newshowPocket = GUILayout.Toggle(mol.scheme.showpocket != 0, "Pocket");
            //if (newshowPocket != (mol.scheme.showpocket != 0))
            //{
            //    mol.scheme.showpocket = newshowPocket ? 1 : 0;
            //    mol.Represent();
            //}

            //bool newshowPharma = GUILayout.Toggle(mol.scheme.showpharma != 0, "Pharma");
            //if (newshowPharma != (mol.scheme.showpharma != 0))
            //{
            //    mol.scheme.showpharma = newshowPharma ? 1 : 0;
            //    mol.Represent();
            //}

            GUILayout.EndHorizontal();
        }


    }
}
