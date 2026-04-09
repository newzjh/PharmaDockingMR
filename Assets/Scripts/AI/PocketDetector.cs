using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using System.Linq;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace AIDrugDiscovery
{
    public enum FPocketImplementationMode
    {
        LegacyGPU = 0,
        OfficialStyleCPU = 1,
        LegacyCPU = 3,
    }


    
    public static class FPocketConstants
    {
        
        public const float PROBE_RADIUS = 1.4f;        
        public const float MIN_ALPHA_SPHERE_RADIUS = 0.8f; 
        public const float MAX_ALPHA_SPHERE_RADIUS = 6.0f; 
        public const float SPHERE_ATOM_EPS = 0.1f;     

        
        public static readonly Dictionary<string, float> VdwRadii = new Dictionary<string, float>
    {
        { "H", 1.20f }, { "C", 1.70f }, { "N", 1.55f }, { "O", 1.52f },
        { "S", 1.80f }, { "P", 1.80f }, { "F", 1.47f }, { "CL", 1.75f },
        { "BR", 1.85f }, { "I", 1.98f }, { "OTHER", 1.60f }
    };

        
        public static readonly Dictionary<string, float> HydrophobicWeights = new Dictionary<string, float>
    {
        { "C", 1.0f }, { "H", 1.0f }, { "N", 0.0f }, { "O", 0.0f },
        { "S", 0.2f }, { "P", 0.1f }, { "F", 0.8f }, { "CL", 0.7f },
        { "BR", 0.6f }, { "I", 0.5f }, { "OTHER", 0.0f }
    };

        
        public const int DBSCAN_MIN_POINTS = 5;
        public const float DBSCAN_EPS = 3.5f;

        
        public const float MIN_POCKET_VOLUME = 10.0f;
        public const int MAX_ALPHA_SPHERES = 200000;
        public const int MAX_POCKETS = 100;
        public const float OFFICIAL_NEIGHBOR_CUTOFF = 7.5f;
        public const int OFFICIAL_MAX_NEARBY_ATOMS = 24;
        public const int OFFICIAL_MIN_NEARBY_ATOMS = 6;
        public const float OFFICIAL_DUPLICATE_CENTER_EPS = 0.35f;
        public const float OFFICIAL_DUPLICATE_RADIUS_EPS = 0.2f;

        
        public const int THREAD_GROUP_SIZE_X = 32; 
        public const int THREAD_GROUP_SIZE_Y = 32; 
    }

    public static class FPocketDirDefaults
    {
        public const float MinAsphereRadius = 3.0f;
        public const float MaxAsphereRadius = 6.0f;
        public const float ClustMaxDist = 1.73f;
        public const float RefineClustDist = 4.5f;
        public const float RefineMinApolarProp = 0.0f;
        public const float SlClustMaxDist = 2.5f;
        public const int SlClustMinNumNeigh = 2;
        public const int MinApolNeigh = 3;
        public const int McIter = 3000;
        public const float VolumeCorrect = -1.6f;
        public const int MinPocketNbAsph = 36;
        public const int MaxNeighbors = 24;
    }

    
    [Serializable]
    public struct FPocketAtom
    {
        public int id;                 
        public Vector3 pos;            
        public string name;            
        public float vdw_radius;       
        public float hydrophobicity;   
        public int res_id;             
        public int aaIndex;
        public float electroneg;
    }

    
    [Serializable]
    public struct FPocketAlphaSphere
    {
        public Vector3 center;         
        public float radius;           
        public int nb_atoms;           
        public float hydrophobicity;   
        public float polarity;         
        public int visited;            
        public int[] parent_atoms;     
    }

    
    [Serializable]
    public struct FPocketResult
    {
        public int id;                 
        public Vector3 center;         
        public float volume;           
        public float score;            
        public float hydrophobic_score;
        public float polar_score;      
        public float depth_score;      
        public int nb_alpha_spheres;   
        public int nb_atoms;           
        public float density;          
    }

    
    [StructLayout(LayoutKind.Sequential)]
    public struct FPocketAtomCS
    {
        public int id;
        public Vector3 pos;
        public float vdw_radius;
        public float hydrophobicity;
        public float electroneg;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FPocketAlphaSphereCS
    {
        public Vector3 center;
        public float radius;
        public int nb_atoms;
        public float hydrophobicity;
        public float polarity;
        public int visited;
        public int parent_atom1; 
        public int parent_atom2;
        public int parent_atom3;
        public int parent_atom4;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FPocketResultCS
    {
        public int id;
        public Vector3 center;
        public float volume;
        public float score;
        public float hydrophobic_score;
        public float polar_score;
        public float depth_score;
        public int nb_alpha_spheres;
        public int nb_atoms;
        public float density;
        public int lockFlag; 
    }

    public class PocketDetector : MonoBehaviour
    {
        [Header("Settings")]
        public string pdbqtFilePath;   

        [Header("Settings")]
        public ComputeShader fpocketComputeShader; 

        [Header("Settings")]
        public bool useGeneratedSphereCountDispatch = true;
        public bool onlyProcessGeneratedSphereRange = true;
        public bool useSpatialHashDbscan = true;
        public FPocketImplementationMode implementationMode = FPocketImplementationMode.OfficialStyleCPU;
        public float singleLinkageThreshold = 4.5f;

        
        private List<FPocketAtom> atoms;
        private List<FPocketAlphaSphere> alphaSpheres;
        private int4[] fpocketDirVneigh;
        private int fpocketDirLastHeavyAtomCount;
        private int fpocketDirLastTetCount;
        private int fpocketDirLastValidTetCount;
        [ContextMenu("Run FPocket CPU Version")]
        public void RunFPocketCPU()
        {
            atoms = LoadAtomsFromPDBQT(pdbqtFilePath);
            if (atoms.Count < 3)
            {
                Debug.LogError("Pocket detection requires at least three atoms in the input structure.");
                return;
            }
            Debug.Log($"Loaded {atoms.Count} atoms from {Path.GetFileName(pdbqtFilePath)}.");

            alphaSpheres = GenerateAlphaSpheresFromAtomTriples(atoms);
            Debug.Log($"Generated {alphaSpheres.Count} raw alpha spheres.");

            List<FPocketAlphaSphere> validSpheres = FilterAlphaSpheres(alphaSpheres);
            Debug.Log($"Retained {validSpheres.Count} alpha spheres after geometric filtering.");

            List<List<FPocketAlphaSphere>> clusters = DBSCANCluster(validSpheres);
            Debug.Log($"Clustered filtered alpha spheres into {clusters.Count} candidate pockets.");

            
            List<FPocketResult> pockets = ComputePocketFeatures(clusters);

            
            List<FPocketResult> finalPockets = pockets
                .Where(p =>
                    p.volume >= FPocketConstants.MIN_POCKET_VOLUME &&
                    p.score >= 0.5f 
                )
                .OrderByDescending(p => p.score)
                .ToList();

            
            finalPockets = RemoveOverlappingPockets(finalPockets, 0.7f);

            PrintPocketResults(finalPockets);
        }
        [ContextMenu("Run FPocket GPU Version (No Overflow)")]
        public async void RunFPocketGPU()
        {
            if (fpocketComputeShader == null)
            {
                Debug.LogError("FPocket compute shader is not assigned.");
                return;
            }

            
            atoms = LoadAtomsFromPDBQT(pdbqtFilePath);
            if (atoms.Count < 3)
            {
                Debug.LogError("Pocket detection requires at least three atoms in the input structure.");
                return;
            }
            int atomCount = atoms.Count;
            Debug.Log($"Loaded {atomCount} atoms from {Path.GetFileName(pdbqtFilePath)}.");

            int threadGroupsX = Mathf.CeilToInt((float)atomCount / FPocketConstants.THREAD_GROUP_SIZE_X);
            int threadGroupsY = Mathf.CeilToInt((float)atomCount / FPocketConstants.THREAD_GROUP_SIZE_Y);
            Debug.Log($"Dispatching alpha-sphere generation with {threadGroupsX} x {threadGroupsY} thread groups.");

            
            ComputeBuffer atomBuffer = null;
            ComputeBuffer alphaSphereBuffer = null;
            ComputeBuffer pocketResultBuffer = null;
            ComputeBuffer sphereCountBuffer = null;
            ComputeBuffer clusterCountBuffer = null;

            try
            {
                atomBuffer = InitAtomBuffer(atoms);
                alphaSphereBuffer = InitAlphaSphereBuffer();
                pocketResultBuffer = InitPocketResultBuffer();
                sphereCountBuffer = new ComputeBuffer(1, sizeof(int));
                clusterCountBuffer = new ComputeBuffer(1, sizeof(int));

                
                int[] initCount = { 0 };
                sphereCountBuffer.SetData(initCount);
                clusterCountBuffer.SetData(initCount);

                
                SetShaderConstants(fpocketComputeShader, atomCount, 0);

                
                int kernel1 = fpocketComputeShader.FindKernel("CSGenerateAlphaSpheres");
                fpocketComputeShader.SetBuffer(kernel1, "atomBuffer", atomBuffer);
                fpocketComputeShader.SetBuffer(kernel1, "alphaSphereBuffer", alphaSphereBuffer);
                fpocketComputeShader.SetBuffer(kernel1, "pocketResultBuffer", pocketResultBuffer);
                fpocketComputeShader.SetBuffer(kernel1, "sphereCountBuffer", sphereCountBuffer);
                fpocketComputeShader.SetBuffer(kernel1, "clusterCountBuffer", clusterCountBuffer);
                fpocketComputeShader.Dispatch(kernel1, threadGroupsX, threadGroupsY, 1);

                int[] generatedCountData = { 0 };
                sphereCountBuffer.GetData(generatedCountData);
                int generatedSphereCount = Mathf.Clamp(generatedCountData[0], 0, FPocketConstants.MAX_ALPHA_SPHERES);
                int filterSphereCount = useGeneratedSphereCountDispatch ? generatedSphereCount : FPocketConstants.MAX_ALPHA_SPHERES;
                int postProcessSphereCount = onlyProcessGeneratedSphereRange ? generatedSphereCount : FPocketConstants.MAX_ALPHA_SPHERES;
                SetShaderConstants(fpocketComputeShader, atomCount, filterSphereCount);

                
                int kernel2 = fpocketComputeShader.FindKernel("CSFilterAlphaSpheres");
                fpocketComputeShader.SetBuffer(kernel2, "atomBuffer", atomBuffer);
                fpocketComputeShader.SetBuffer(kernel2, "alphaSphereBuffer", alphaSphereBuffer);
                fpocketComputeShader.SetBuffer(kernel2, "pocketResultBuffer", pocketResultBuffer);
                fpocketComputeShader.SetBuffer(kernel2, "sphereCountBuffer", sphereCountBuffer);
                fpocketComputeShader.SetBuffer(kernel2, "clusterCountBuffer", clusterCountBuffer);
                int threadGroupsFilter = Mathf.CeilToInt(Mathf.Max(1, filterSphereCount) / 256f);
                fpocketComputeShader.Dispatch(kernel2, threadGroupsFilter, 1, 1);

                //FPocketAlphaSphereCS[] data = new FPocketAlphaSphereCS[FPocketConstants.MAX_ALPHA_SPHERES];
                //alphaSphereBuffer.GetData(data);
                var req = await AsyncGPUReadback.RequestAsync(alphaSphereBuffer);
                var data = req.GetData<FPocketAlphaSphereCS>().ToArray();
                List<FPocketAlphaSphere> validSpheres = new();
                int sphereLoopCount = Mathf.Min(postProcessSphereCount, data.Length);
                for (int sphereIdx = 0; sphereIdx < sphereLoopCount; sphereIdx++)
                {
                    var sphere = data[sphereIdx];
                    if (sphere.radius > 0)
                    {
                        FPocketAlphaSphere newsphere = new FPocketAlphaSphere();
                        newsphere.center = sphere.center;
                        newsphere.radius = sphere.radius;
                        newsphere.nb_atoms = sphere.nb_atoms;
                        newsphere.hydrophobicity = sphere.hydrophobicity;
                        newsphere.polarity = sphere.polarity;
                        newsphere.visited = sphere.visited;
                        newsphere.parent_atoms = new int[] { sphere.parent_atom1, sphere.parent_atom2, sphere.parent_atom3 };
                        validSpheres.Add(newsphere);
                    }
                }

                
                List<List<FPocketAlphaSphere>> clusters = DBSCANCluster(validSpheres);
                Debug.Log($"GPU pre-processing produced {validSpheres.Count} valid alpha spheres across {clusters.Count} clusters.");

                
                List<FPocketResult> pockets = ComputePocketFeatures(clusters);

                
                List<FPocketResult> finalPockets = pockets
                    .Where(p =>
                        p.volume >= FPocketConstants.MIN_POCKET_VOLUME &&
                        p.score >= 0.5f 
                    )
                    .OrderByDescending(p => p.score)
                    .ToList();

                
                finalPockets = RemoveOverlappingPockets(finalPockets, 0.7f);

                PrintPocketResults(finalPockets);
                //int kernel3 = fpocketComputeShader.FindKernel("CSDBSCANCluster");
                //fpocketComputeShader.SetBuffer(kernel3, "alphaSphereBuffer", alphaSphereBuffer);
                //fpocketComputeShader.SetBuffer(kernel3, "pocketResultBuffer", pocketResultBuffer);
                //fpocketComputeShader.SetBuffer(kernel3, "clusterCountBuffer", clusterCountBuffer);
                //fpocketComputeShader.Dispatch(kernel3, threadGroupsFilter, 1, 1);
                //int kernel4 = fpocketComputeShader.FindKernel("CSCalculatePocketScores");
                //fpocketComputeShader.SetBuffer(kernel4, "atomBuffer", atomBuffer);
                //fpocketComputeShader.SetBuffer(kernel4, "pocketResultBuffer", pocketResultBuffer);
                //int threadGroupsScore = Mathf.CeilToInt(FPocketConstants.MAX_POCKETS / 256f);
                //fpocketComputeShader.Dispatch(kernel4, threadGroupsScore, 1, 1);
                //ReadAndPrintGPUResults(pocketResultBuffer);
            }
            catch (Exception e)
            {
                Debug.LogError($"GPU pocket detection failed: {e.Message}\n{e.StackTrace}");
            }
            finally
            {
                
                ReleaseBuffers(atomBuffer, alphaSphereBuffer, pocketResultBuffer, sphereCountBuffer, clusterCountBuffer);
            }
        }

        #region Official-style CPU/GPU logic
        public void RunFPocketOfficialCPU()
        {
            atoms = LoadAtomsFromPDBQT(pdbqtFilePath);
            if (atoms.Count < 4)
            {
                Debug.LogError("Not enough atoms to build alpha spheres.");
                return;
            }

            alphaSpheres = GenerateAlphaSpheresFPocketDirCPU(atoms);
            var pockets = DetectPocketsFPocketDir(alphaSpheres);
            PrintPocketResults(pockets, flipZ: true);
        }

        private List<FPocketAlphaSphere> GenerateAlphaSpheresFromAtomTriples(List<FPocketAtom> atoms)
        {
            List<FPocketAlphaSphere> alphaSpheres = new List<FPocketAlphaSphere>();
            int atomCount = atoms.Count;

            
            for (int i = 0; i < atomCount - 2; i++)
            {
                for (int j = i + 1; j < atomCount - 1; j++)
                {
                    for (int k = j + 1; k < atomCount; k++)
                    {
                        FPocketAtom a1 = atoms[i];
                        FPocketAtom a2 = atoms[j];
                        FPocketAtom a3 = atoms[k];

                        
                        (Vector3 center, float radius) = ComputeCircumsphere(a1.pos, a2.pos, a3.pos);
                        if (radius < FPocketConstants.MIN_ALPHA_SPHERE_RADIUS || radius > FPocketConstants.MAX_ALPHA_SPHERE_RADIUS)
                            continue;

                        
                        bool isEmpty = IsEmptySphere(center, radius, atoms, i, j, k);
                        if (!isEmpty) continue;

                        
                        if (radius < FPocketConstants.PROBE_RADIUS) continue;
                        if (!IsSphereCenterOutsideMolecule(center, atoms)) continue;

                        
                        (int nbAtoms, float totalHydro) = CountEnclosedAtoms(center, radius, atoms);

                        
                        FPocketAlphaSphere sphere = new FPocketAlphaSphere
                        {
                            center = center,
                            radius = radius,
                            nb_atoms = nbAtoms,
                            hydrophobicity = nbAtoms > 0 ? totalHydro / nbAtoms : 0f,
                            polarity = 1f - (nbAtoms > 0 ? totalHydro / nbAtoms : 0f),
                            visited = 0,
                            parent_atoms = new[] { i, j, k } 
                        };

                        alphaSpheres.Add(sphere);

                        
                        if (alphaSpheres.Count >= FPocketConstants.MAX_ALPHA_SPHERES)
                            goto ExitTripleLoop;
                    }
                }
            }
            ExitTripleLoop:

            return alphaSpheres;
        }

        private List<FPocketAlphaSphere> GenerateAlphaSpheresFPocketDirCPU(List<FPocketAtom> atoms)
        {
            fpocketDirVneigh = null;
            fpocketDirLastHeavyAtomCount = 0;
            fpocketDirLastTetCount = 0;
            fpocketDirLastValidTetCount = 0;
            if (atoms == null || atoms.Count < 4)
                return new List<FPocketAlphaSphere>();

            List<FPocketAtom> heavyAtoms = new List<FPocketAtom>(atoms.Count);
            List<int> heavyToAtom = new List<int>(atoms.Count);
            for (int i = 0; i < atoms.Count; i++)
            {
                string an = atoms[i].name;
                if (!string.IsNullOrEmpty(an) && (an[0] == 'H' || an[0] == 'h'))
                    continue;

                heavyToAtom.Add(i);
                FPocketAtom ha = atoms[i];
                ha.pos = Round3(ha.pos);
                heavyAtoms.Add(ha);
            }

            if (heavyAtoms.Count < 4)
                return new List<FPocketAlphaSphere>();

            fpocketDirLastHeavyAtomCount = heavyAtoms.Count;
            BuildDelaunayTetrahedra(heavyAtoms, out List<int4> tetVerts, out List<int4> tetNeigh);
            fpocketDirLastTetCount = tetVerts.Count;
            if (tetVerts.Count == 0)
            {
                Debug.Log($"FPocketDirCPU: heavy_atoms={fpocketDirLastHeavyAtomCount}, tets=0 (delaunay failed), spheres=0");
                return new List<FPocketAlphaSphere>();
            }

            int atomCount = heavyAtoms.Count;
            int tetCount = tetVerts.Count;
            fpocketDirLastTetCount = tetCount;

            NativeArray<float3> positions = new NativeArray<float3>(atomCount, Allocator.TempJob);
            NativeArray<float> electroneg = new NativeArray<float>(atomCount, Allocator.TempJob);
            for (int i = 0; i < atomCount; i++)
            {
                Vector3 p = heavyAtoms[i].pos;
                positions[i] = new float3(p.x, p.y, p.z);
                electroneg[i] = heavyAtoms[i].electroneg;
            }

            NativeArray<int4> tetVertsNA = new NativeArray<int4>(tetVerts.ToArray(), Allocator.TempJob);
            NativeArray<int4> tetNeighNA = new NativeArray<int4>(tetNeigh.ToArray(), Allocator.TempJob);
            NativeArray<byte> validNA = new NativeArray<byte>(tetCount, Allocator.TempJob);

            var markJob = new MarkValidVoronoiVerticesJob
            {
                positions = positions,
                tetVerts = tetVertsNA,
                minRadius = FPocketDirDefaults.MinAsphereRadius,
                maxRadius = FPocketDirDefaults.MaxAsphereRadius,
                valid = validNA
            };
            markJob.Schedule(tetCount, 64).Complete();

            int[] tetToSphere = new int[tetCount];
            int maxSpheres = FPocketConstants.MAX_ALPHA_SPHERES;
            int sphereCount = 0;
            for (int i = 0; i < tetCount; i++)
            {
                if (validNA[i] == 0 || sphereCount >= maxSpheres)
                {
                    tetToSphere[i] = -1;
                    continue;
                }

                tetToSphere[i] = sphereCount;
                sphereCount++;
            }
            fpocketDirLastValidTetCount = sphereCount;
            if (sphereCount == 0)
            {
                Debug.Log($"FPocketDirCPU: heavy_atoms={fpocketDirLastHeavyAtomCount}, tets={fpocketDirLastTetCount}, kept_spheres=0 (all filtered), spheres=0");
                tetNeighNA.Dispose();
                tetVertsNA.Dispose();
                electroneg.Dispose();
                positions.Dispose();
                validNA.Dispose();
                return new List<FPocketAlphaSphere>();
            }

            NativeArray<int> tetToSphereNA = new NativeArray<int>(tetToSphere, Allocator.TempJob);
            NativeArray<float3> sphereCentersNA = new NativeArray<float3>(sphereCount, Allocator.TempJob);
            NativeArray<float> sphereRadiiNA = new NativeArray<float>(sphereCount, Allocator.TempJob);
            NativeArray<byte> sphereApolarNA = new NativeArray<byte>(sphereCount, Allocator.TempJob);
            NativeArray<int4> sphereParentsNA = new NativeArray<int4>(sphereCount, Allocator.TempJob);

            var writeSpheresJob = new WriteVoronoiVerticesJob
            {
                positions = positions,
                electroneg = electroneg,
                tetVerts = tetVertsNA,
                tetToSphere = tetToSphereNA,
                minRadius = FPocketDirDefaults.MinAsphereRadius,
                maxRadius = FPocketDirDefaults.MaxAsphereRadius,
                minApolNeigh = FPocketDirDefaults.MinApolNeigh,
                sphereCenters = sphereCentersNA,
                sphereRadii = sphereRadiiNA,
                sphereIsApolar = sphereApolarNA,
                sphereParents = sphereParentsNA
            };
            writeSpheresJob.Schedule(tetCount, 64).Complete();

            NativeArray<int4> sphereVneighNA = new NativeArray<int4>(sphereCount, Allocator.TempJob);
            for (int i = 0; i < sphereCount; i++)
                sphereVneighNA[i] = new int4(-1, -1, -1, -1);

            var writeVneighJob = new WriteVoronoiVneighJob
            {
                tetNeigh = tetNeighNA,
                tetToSphere = tetToSphereNA,
                outVneigh = sphereVneighNA
            };
            writeVneighJob.Schedule(tetCount, 64).Complete();

            List<FPocketAlphaSphere> spheres = new List<FPocketAlphaSphere>(sphereCount);
            for (int i = 0; i < sphereCount; i++)
            {
                float3 c = sphereCentersNA[i];
                int4 parentsHeavy = sphereParentsNA[i];
                spheres.Add(new FPocketAlphaSphere
                {
                    center = new Vector3(c.x, c.y, c.z),
                    radius = sphereRadiiNA[i],
                    nb_atoms = 4,
                    hydrophobicity = sphereApolarNA[i] != 0 ? 1f : 0f,
                    polarity = sphereApolarNA[i] != 0 ? 0f : 1f,
                    visited = 0,
                    parent_atoms = new[]
                    {
                        heavyToAtom[parentsHeavy.x],
                        heavyToAtom[parentsHeavy.y],
                        heavyToAtom[parentsHeavy.z],
                        heavyToAtom[parentsHeavy.w]
                    }
                });
            }

            fpocketDirVneigh = sphereVneighNA.ToArray();

            sphereVneighNA.Dispose();
            sphereParentsNA.Dispose();
            sphereApolarNA.Dispose();
            sphereRadiiNA.Dispose();
            sphereCentersNA.Dispose();
            tetToSphereNA.Dispose();
            validNA.Dispose();
            tetNeighNA.Dispose();
            tetVertsNA.Dispose();
            electroneg.Dispose();
            positions.Dispose();

            return spheres;
        }

        private static Vector3 Round3(Vector3 v)
        {
            return new Vector3(
                Mathf.Round(v.x * 1000f) / 1000f,
                Mathf.Round(v.y * 1000f) / 1000f,
                Mathf.Round(v.z * 1000f) / 1000f
            );
        }

        private struct AlphaSphereOut
        {
            public float cx;
            public float cy;
            public float cz;
            public float radius;
            public int a;
            public int b;
            public int c;
            public int d;
            public byte isApolar;
        }

        private static bool TryCircumsphere4(float3 p1, float3 p2, float3 p3, float3 p4, out float3 center, out float radius)
        {
            float3 r1 = p2 - p1;
            float3 r2 = p3 - p1;
            float3 r3 = p4 - p1;

            float a11 = 2f * r1.x;
            float a12 = 2f * r1.y;
            float a13 = 2f * r1.z;
            float b1 = math.lengthsq(p2) - math.lengthsq(p1);

            float a21 = 2f * r2.x;
            float a22 = 2f * r2.y;
            float a23 = 2f * r2.z;
            float b2 = math.lengthsq(p3) - math.lengthsq(p1);

            float a31 = 2f * r3.x;
            float a32 = 2f * r3.y;
            float a33 = 2f * r3.z;
            float b3 = math.lengthsq(p4) - math.lengthsq(p1);

            float det = a11 * (a22 * a33 - a23 * a32) - a12 * (a21 * a33 - a23 * a31) + a13 * (a21 * a32 - a22 * a31);
            if (math.abs(det) < 1e-6f)
            {
                center = default;
                radius = 0f;
                return false;
            }

            float detX = b1 * (a22 * a33 - a23 * a32) - a12 * (b2 * a33 - a23 * b3) + a13 * (b2 * a32 - a22 * b3);
            float detY = a11 * (b2 * a33 - a23 * b3) - b1 * (a21 * a33 - a23 * a31) + a13 * (a21 * b3 - b2 * a31);
            float detZ = a11 * (a22 * b3 - b2 * a32) - a12 * (a21 * b3 - b2 * a31) + b1 * (a21 * a32 - a22 * a31);

            center = new float3(detX / det, detY / det, detZ / det);
            radius = math.distance(center, p1);
            return true;
        }

        private static bool TryCircumsphere4Precise(float3 p1, float3 p2, float3 p3, float3 p4, out float3 center, out float radius)
        {
            double r1x = (double)p2.x - p1.x;
            double r1y = (double)p2.y - p1.y;
            double r1z = (double)p2.z - p1.z;
            double r2x = (double)p3.x - p1.x;
            double r2y = (double)p3.y - p1.y;
            double r2z = (double)p3.z - p1.z;
            double r3x = (double)p4.x - p1.x;
            double r3y = (double)p4.y - p1.y;
            double r3z = (double)p4.z - p1.z;

            double a11 = 2.0 * r1x;
            double a12 = 2.0 * r1y;
            double a13 = 2.0 * r1z;
            double b1 = (double)p2.x * p2.x + (double)p2.y * p2.y + (double)p2.z * p2.z
                        - ((double)p1.x * p1.x + (double)p1.y * p1.y + (double)p1.z * p1.z);

            double a21 = 2.0 * r2x;
            double a22 = 2.0 * r2y;
            double a23 = 2.0 * r2z;
            double b2 = (double)p3.x * p3.x + (double)p3.y * p3.y + (double)p3.z * p3.z
                        - ((double)p1.x * p1.x + (double)p1.y * p1.y + (double)p1.z * p1.z);

            double a31 = 2.0 * r3x;
            double a32 = 2.0 * r3y;
            double a33 = 2.0 * r3z;
            double b3 = (double)p4.x * p4.x + (double)p4.y * p4.y + (double)p4.z * p4.z
                        - ((double)p1.x * p1.x + (double)p1.y * p1.y + (double)p1.z * p1.z);

            double det = a11 * (a22 * a33 - a23 * a32) - a12 * (a21 * a33 - a23 * a31) + a13 * (a21 * a32 - a22 * a31);
            if (Math.Abs(det) < 1e-12)
            {
                center = default;
                radius = 0f;
                return false;
            }

            double detX = b1 * (a22 * a33 - a23 * a32) - a12 * (b2 * a33 - a23 * b3) + a13 * (b2 * a32 - a22 * b3);
            double detY = a11 * (b2 * a33 - a23 * b3) - b1 * (a21 * a33 - a23 * a31) + a13 * (a21 * b3 - b2 * a31);
            double detZ = a11 * (a22 * b3 - b2 * a32) - a12 * (a21 * b3 - b2 * a31) + b1 * (a21 * a32 - a22 * a31);

            double cx = detX / det;
            double cy = detY / det;
            double cz = detZ / det;
            center = new float3((float)cx, (float)cy, (float)cz);

            double dx = cx - p1.x;
            double dy = cy - p1.y;
            double dz = cz - p1.z;
            radius = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            return true;
        }

        private static bool TryCircumsphere4PreciseDouble(float3 p1, float3 p2, float3 p3, float3 p4, out double cx, out double cy, out double cz, out double radius)
        {
            double r1x = (double)p2.x - p1.x;
            double r1y = (double)p2.y - p1.y;
            double r1z = (double)p2.z - p1.z;
            double r2x = (double)p3.x - p1.x;
            double r2y = (double)p3.y - p1.y;
            double r2z = (double)p3.z - p1.z;
            double r3x = (double)p4.x - p1.x;
            double r3y = (double)p4.y - p1.y;
            double r3z = (double)p4.z - p1.z;

            double a11 = 2.0 * r1x;
            double a12 = 2.0 * r1y;
            double a13 = 2.0 * r1z;
            double b1 = (double)p2.x * p2.x + (double)p2.y * p2.y + (double)p2.z * p2.z
                        - ((double)p1.x * p1.x + (double)p1.y * p1.y + (double)p1.z * p1.z);

            double a21 = 2.0 * r2x;
            double a22 = 2.0 * r2y;
            double a23 = 2.0 * r2z;
            double b2 = (double)p3.x * p3.x + (double)p3.y * p3.y + (double)p3.z * p3.z
                        - ((double)p1.x * p1.x + (double)p1.y * p1.y + (double)p1.z * p1.z);

            double a31 = 2.0 * r3x;
            double a32 = 2.0 * r3y;
            double a33 = 2.0 * r3z;
            double b3 = (double)p4.x * p4.x + (double)p4.y * p4.y + (double)p4.z * p4.z
                        - ((double)p1.x * p1.x + (double)p1.y * p1.y + (double)p1.z * p1.z);

            double det = a11 * (a22 * a33 - a23 * a32) - a12 * (a21 * a33 - a23 * a31) + a13 * (a21 * a32 - a22 * a31);
            if (Math.Abs(det) < 1e-18)
            {
                cx = default;
                cy = default;
                cz = default;
                radius = default;
                return false;
            }

            double detX = b1 * (a22 * a33 - a23 * a32) - a12 * (b2 * a33 - a23 * b3) + a13 * (b2 * a32 - a22 * b3);
            double detY = a11 * (b2 * a33 - a23 * b3) - b1 * (a21 * a33 - a23 * a31) + a13 * (a21 * b3 - b2 * a31);
            double detZ = a11 * (a22 * b3 - b2 * a32) - a12 * (a21 * b3 - b2 * a31) + b1 * (a21 * a32 - a22 * a31);

            cx = detX / det;
            cy = detY / det;
            cz = detZ / det;

            double dx = cx - p1.x;
            double dy = cy - p1.y;
            double dz = cz - p1.z;
            radius = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            return true;
        }

        private struct FaceKey : IEquatable<FaceKey>
        {
            public int a;
            public int b;
            public int c;

            public FaceKey(int x, int y, int z)
            {
                if (x > y) (x, y) = (y, x);
                if (y > z) (y, z) = (z, y);
                if (x > y) (x, y) = (y, x);
                a = x;
                b = y;
                c = z;
            }

            public bool Equals(FaceKey other) => a == other.a && b == other.b && c == other.c;
            public override bool Equals(object obj) => obj is FaceKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = a;
                    h = (h * 397) ^ b;
                    h = (h * 397) ^ c;
                    return h;
                }
            }
        }

        private struct FaceInfo
        {
            public int v0;
            public int v1;
            public int v2;
        }

        private struct BowyerTetra
        {
            public int4 v;
            public Vector3 center;
            public double r2;
        }

        private void BuildDelaunayTetrahedra(List<FPocketAtom> atoms, out List<int4> tetVerts, out List<int4> tetNeigh)
        {
            tetVerts = new List<int4>();
            tetNeigh = new List<int4>();

            int n = atoms.Count;
            if (n < 4)
                return;

            Vector3 min = atoms[0].pos;
            Vector3 max = atoms[0].pos;
            for (int i = 1; i < n; i++)
            {
                Vector3 p = atoms[i].pos;
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }

            Vector3 mid = (min + max) * 0.5f;
            float dx = max.x - min.x;
            float dy = max.y - min.y;
            float dz = max.z - min.z;
            float delta = Mathf.Max(dx, Mathf.Max(dy, dz));
            if (delta <= 0f) delta = 1f;
            float d = delta * 16f;

            int s0 = n;
            int s1 = n + 1;
            int s2 = n + 2;
            int s3 = n + 3;

            Vector3[] pts = new Vector3[n + 4];
            for (int i = 0; i < n; i++) pts[i] = atoms[i].pos;

            float joggle = delta * 1e-5f;
            if (joggle > 0f)
            {
                for (int i = 0; i < n; i++)
                    pts[i] = pts[i] + Joggle3(i, joggle);
            }
            pts[s0] = new Vector3(mid.x - d, mid.y - d, mid.z - d);
            pts[s1] = new Vector3(mid.x + d, mid.y - d, mid.z + d);
            pts[s2] = new Vector3(mid.x - d, mid.y + d, mid.z + d);
            pts[s3] = new Vector3(mid.x + d, mid.y + d, mid.z - d);

            List<int4> tets = new List<int4>(Mathf.Max(16, n * 4));
            int4 super = new int4(s0, s1, s2, s3);
            if (Orient3D(pts[super.x], pts[super.y], pts[super.z], pts[super.w]) < 0.0)
                super = new int4(super.x, super.z, super.y, super.w);
            tets.Add(super);

            int[] order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            var rng = new System.Random(1337);
            for (int i = n - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            for (int oi = 0; oi < n; oi++)
            {
                int pi = order[oi];
                Vector3 p = pts[pi];
                bool[] bad = new bool[tets.Count];
                int badCount = 0;

                for (int ti = 0; ti < tets.Count; ti++)
                {
                    int4 tv = tets[ti];
                    double val = InSphere(pts[tv.x], pts[tv.y], pts[tv.z], pts[tv.w], p);
                    if (val > 1e-12)
                    {
                        bad[ti] = true;
                        badCount++;
                    }
                }

                if (badCount == 0)
                    continue;

                Dictionary<FaceKey, FaceInfo> boundary = new Dictionary<FaceKey, FaceInfo>(badCount * 4);

                void ToggleFace(int a, int b, int c)
                {
                    FaceKey k = new FaceKey(a, b, c);
                    if (boundary.ContainsKey(k))
                        boundary.Remove(k);
                    else
                        boundary.Add(k, new FaceInfo { v0 = a, v1 = b, v2 = c });
                }

                for (int ti = 0; ti < tets.Count; ti++)
                {
                    if (!bad[ti]) continue;
                    int4 v = tets[ti];
                    ToggleFace(v.y, v.z, v.w);
                    ToggleFace(v.x, v.z, v.w);
                    ToggleFace(v.x, v.y, v.w);
                    ToggleFace(v.x, v.y, v.z);
                }

                List<int4> newTets = new List<int4>(tets.Count - badCount + boundary.Count);
                for (int ti = 0; ti < tets.Count; ti++)
                {
                    if (!bad[ti]) newTets.Add(tets[ti]);
                }

                foreach (FaceInfo f in boundary.Values)
                {
                    int4 tv = new int4(f.v0, f.v1, f.v2, pi);
                    if (Orient3D(pts[tv.x], pts[tv.y], pts[tv.z], pts[tv.w]) < 0.0)
                        tv = new int4(tv.x, tv.z, tv.y, tv.w);
                    newTets.Add(tv);
                }

                tets = newTets;
            }

            List<int4> finalVerts = new List<int4>(tets.Count);
            for (int i = 0; i < tets.Count; i++)
            {
                int4 v = tets[i];
                if (v.x >= n || v.y >= n || v.z >= n || v.w >= n)
                    continue;
                if (v.x < 0 || v.y < 0 || v.z < 0 || v.w < 0)
                    continue;
                finalVerts.Add(v);
            }

            if (finalVerts.Count == 0)
            {
                BuildDelaunayTetrahedraCircumsphere(atoms, out tetVerts, out tetNeigh);
                return;
            }

            int4[] neighArr = new int4[finalVerts.Count];
            for (int i = 0; i < neighArr.Length; i++) neighArr[i] = new int4(-1, -1, -1, -1);
            Dictionary<FaceKey, (int tet, int slot)> faceMap = new Dictionary<FaceKey, (int, int)>(finalVerts.Count * 2);

            void SetNeighbor(int tetIdx, int slot, int neighborTet)
            {
                int4 n4 = neighArr[tetIdx];
                if (slot == 0) n4.x = neighborTet;
                else if (slot == 1) n4.y = neighborTet;
                else if (slot == 2) n4.z = neighborTet;
                else n4.w = neighborTet;
                neighArr[tetIdx] = n4;
            }

            for (int ti = 0; ti < finalVerts.Count; ti++)
            {
                int4 v = finalVerts[ti];
                int3 f0 = new int3(v.y, v.z, v.w);
                int3 f1 = new int3(v.x, v.z, v.w);
                int3 f2 = new int3(v.x, v.y, v.w);
                int3 f3 = new int3(v.x, v.y, v.z);

                void ProcessFace(int3 f, int slot)
                {
                    FaceKey k = new FaceKey(f.x, f.y, f.z);
                    if (faceMap.TryGetValue(k, out var other))
                    {
                        SetNeighbor(ti, slot, other.tet);
                        SetNeighbor(other.tet, other.slot, ti);
                        faceMap.Remove(k);
                    }
                    else
                    {
                        faceMap.Add(k, (ti, slot));
                    }
                }

                ProcessFace(f0, 0);
                ProcessFace(f1, 1);
                ProcessFace(f2, 2);
                ProcessFace(f3, 3);
            }

            tetVerts.AddRange(finalVerts);
            tetNeigh.AddRange(neighArr);
        }

        private void BuildDelaunayTetrahedraCircumsphere(List<FPocketAtom> atoms, out List<int4> tetVerts, out List<int4> tetNeigh)
        {
            tetVerts = new List<int4>();
            tetNeigh = new List<int4>();

            int n = atoms.Count;
            if (n < 4)
                return;

            Vector3 min = atoms[0].pos;
            Vector3 max = atoms[0].pos;
            for (int i = 1; i < n; i++)
            {
                Vector3 p = atoms[i].pos;
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }

            Vector3 mid = (min + max) * 0.5f;
            float dx = max.x - min.x;
            float dy = max.y - min.y;
            float dz = max.z - min.z;
            float delta = Mathf.Max(dx, Mathf.Max(dy, dz));
            if (delta <= 0f) delta = 1f;
            float d = delta * 16f;

            int s0 = n;
            int s1 = n + 1;
            int s2 = n + 2;
            int s3 = n + 3;

            Vector3[] pts = new Vector3[n + 4];
            for (int i = 0; i < n; i++) pts[i] = atoms[i].pos;
            pts[s0] = new Vector3(mid.x - d, mid.y - d, mid.z - d);
            pts[s1] = new Vector3(mid.x + d, mid.y - d, mid.z + d);
            pts[s2] = new Vector3(mid.x - d, mid.y + d, mid.z + d);
            pts[s3] = new Vector3(mid.x + d, mid.y + d, mid.z - d);

            List<BowyerTetra> tets = new List<BowyerTetra>(Mathf.Max(16, n * 4));
            if (!TryCircumsphere4Double(pts[s0], pts[s1], pts[s2], pts[s3], out Vector3 sc, out double sr2))
                return;
            tets.Add(new BowyerTetra { v = new int4(s0, s1, s2, s3), center = sc, r2 = sr2 });

            int[] order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            var rng = new System.Random(1337);
            for (int i = n - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            for (int oi = 0; oi < n; oi++)
            {
                int pi = order[oi];
                Vector3 p = pts[pi];
                bool[] bad = new bool[tets.Count];
                int badCount = 0;

                for (int ti = 0; ti < tets.Count; ti++)
                {
                    BowyerTetra t = tets[ti];
                    Vector3 dc = p - t.center;
                    double dist2 = (double)dc.x * dc.x + (double)dc.y * dc.y + (double)dc.z * dc.z;
                    double tol = 1e-8 * (t.r2 + 1.0);
                    if (dist2 <= t.r2 + tol)
                    {
                        bad[ti] = true;
                        badCount++;
                    }
                }

                if (badCount == 0)
                    continue;

                Dictionary<FaceKey, FaceInfo> boundary = new Dictionary<FaceKey, FaceInfo>(badCount * 4);
                void ToggleFace(int a, int b, int c)
                {
                    FaceKey k = new FaceKey(a, b, c);
                    if (boundary.ContainsKey(k))
                        boundary.Remove(k);
                    else
                        boundary.Add(k, new FaceInfo { v0 = a, v1 = b, v2 = c });
                }

                for (int ti = 0; ti < tets.Count; ti++)
                {
                    if (!bad[ti]) continue;
                    int4 v = tets[ti].v;
                    ToggleFace(v.y, v.z, v.w);
                    ToggleFace(v.x, v.z, v.w);
                    ToggleFace(v.x, v.y, v.w);
                    ToggleFace(v.x, v.y, v.z);
                }

                List<BowyerTetra> newTets = new List<BowyerTetra>(tets.Count - badCount + boundary.Count);
                for (int ti = 0; ti < tets.Count; ti++)
                {
                    if (!bad[ti]) newTets.Add(tets[ti]);
                }

                foreach (FaceInfo f in boundary.Values)
                {
                    int4 tv = new int4(f.v0, f.v1, f.v2, pi);
                    if (!TryCircumsphere4Double(pts[tv.x], pts[tv.y], pts[tv.z], pts[tv.w], out Vector3 cc, out double rr2))
                        continue;
                    newTets.Add(new BowyerTetra { v = tv, center = cc, r2 = rr2 });
                }

                tets = newTets;
            }

            List<int4> finalVerts = new List<int4>(tets.Count);
            for (int i = 0; i < tets.Count; i++)
            {
                int4 v = tets[i].v;
                if (v.x >= n || v.y >= n || v.z >= n || v.w >= n)
                    continue;
                if (v.x < 0 || v.y < 0 || v.z < 0 || v.w < 0)
                    continue;
                finalVerts.Add(v);
            }

            if (finalVerts.Count == 0)
                return;

            int4[] neighArr = new int4[finalVerts.Count];
            for (int i = 0; i < neighArr.Length; i++) neighArr[i] = new int4(-1, -1, -1, -1);
            Dictionary<FaceKey, (int tet, int slot)> faceMap = new Dictionary<FaceKey, (int, int)>(finalVerts.Count * 2);

            void SetNeighbor(int tetIdx, int slot, int neighborTet)
            {
                int4 n4 = neighArr[tetIdx];
                if (slot == 0) n4.x = neighborTet;
                else if (slot == 1) n4.y = neighborTet;
                else if (slot == 2) n4.z = neighborTet;
                else n4.w = neighborTet;
                neighArr[tetIdx] = n4;
            }

            for (int ti = 0; ti < finalVerts.Count; ti++)
            {
                int4 v = finalVerts[ti];
                int3 f0 = new int3(v.y, v.z, v.w);
                int3 f1 = new int3(v.x, v.z, v.w);
                int3 f2 = new int3(v.x, v.y, v.w);
                int3 f3 = new int3(v.x, v.y, v.z);

                void ProcessFace(int3 f, int slot)
                {
                    FaceKey k = new FaceKey(f.x, f.y, f.z);
                    if (faceMap.TryGetValue(k, out var other))
                    {
                        SetNeighbor(ti, slot, other.tet);
                        SetNeighbor(other.tet, other.slot, ti);
                        faceMap.Remove(k);
                    }
                    else
                    {
                        faceMap.Add(k, (ti, slot));
                    }
                }

                ProcessFace(f0, 0);
                ProcessFace(f1, 1);
                ProcessFace(f2, 2);
                ProcessFace(f3, 3);
            }

            tetVerts.AddRange(finalVerts);
            tetNeigh.AddRange(neighArr);
        }

        private static double Orient3D(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            double adx = (double)a.x - d.x;
            double ady = (double)a.y - d.y;
            double adz = (double)a.z - d.z;
            double bdx = (double)b.x - d.x;
            double bdy = (double)b.y - d.y;
            double bdz = (double)b.z - d.z;
            double cdx = (double)c.x - d.x;
            double cdy = (double)c.y - d.y;
            double cdz = (double)c.z - d.z;
            return adx * (bdy * cdz - bdz * cdy) - ady * (bdx * cdz - bdz * cdx) + adz * (bdx * cdy - bdy * cdx);
        }

        private static double Det3(double ax, double ay, double az, double bx, double by, double bz, double cx, double cy, double cz)
        {
            return ax * (by * cz - bz * cy) - ay * (bx * cz - bz * cx) + az * (bx * cy - by * cx);
        }

        private static double InSphere(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 e)
        {
            double aex = (double)a.x - e.x;
            double aey = (double)a.y - e.y;
            double aez = (double)a.z - e.z;
            double bex = (double)b.x - e.x;
            double bey = (double)b.y - e.y;
            double bez = (double)b.z - e.z;
            double cex = (double)c.x - e.x;
            double cey = (double)c.y - e.y;
            double cez = (double)c.z - e.z;
            double dex = (double)d.x - e.x;
            double dey = (double)d.y - e.y;
            double dez = (double)d.z - e.z;

            double alift = aex * aex + aey * aey + aez * aez;
            double blift = bex * bex + bey * bey + bez * bez;
            double clift = cex * cex + cey * cey + cez * cez;
            double dlift = dex * dex + dey * dey + dez * dez;

            double det = alift * Det3(bex, bey, bez, cex, cey, cez, dex, dey, dez)
                         - blift * Det3(aex, aey, aez, cex, cey, cez, dex, dey, dez)
                         + clift * Det3(aex, aey, aez, bex, bey, bez, dex, dey, dez)
                         - dlift * Det3(aex, aey, aez, bex, bey, bez, cex, cey, cez);

            double orient = Orient3D(a, b, c, d);
            if (orient < 0.0) det = -det;
            return det;
        }

        private static Vector3 Joggle3(int i, float scale)
        {
            uint h = (uint)(i * 2654435761);
            float u1 = ((h = h * 1664525u + 1013904223u) & 0x00FFFFFFu) / 16777215f;
            float u2 = ((h = h * 1664525u + 1013904223u) & 0x00FFFFFFu) / 16777215f;
            float u3 = ((h = h * 1664525u + 1013904223u) & 0x00FFFFFFu) / 16777215f;
            float x = (u1 * 2f - 1f) * scale;
            float y = (u2 * 2f - 1f) * scale;
            float z = (u3 * 2f - 1f) * scale;
            return new Vector3(x, y, z);
        }

        private static bool TryCircumsphere4Double(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, out Vector3 center, out double r2)
        {
            double r1x = p2.x - p1.x;
            double r1y = p2.y - p1.y;
            double r1z = p2.z - p1.z;
            double r2x = p3.x - p1.x;
            double r2y = p3.y - p1.y;
            double r2z = p3.z - p1.z;
            double r3x = p4.x - p1.x;
            double r3y = p4.y - p1.y;
            double r3z = p4.z - p1.z;

            double a11 = 2.0 * r1x;
            double a12 = 2.0 * r1y;
            double a13 = 2.0 * r1z;
            double b1 = (double)p2.x * p2.x + (double)p2.y * p2.y + (double)p2.z * p2.z
                        - ((double)p1.x * p1.x + (double)p1.y * p1.y + (double)p1.z * p1.z);

            double a21 = 2.0 * r2x;
            double a22 = 2.0 * r2y;
            double a23 = 2.0 * r2z;
            double b2 = (double)p3.x * p3.x + (double)p3.y * p3.y + (double)p3.z * p3.z
                        - ((double)p1.x * p1.x + (double)p1.y * p1.y + (double)p1.z * p1.z);

            double a31 = 2.0 * r3x;
            double a32 = 2.0 * r3y;
            double a33 = 2.0 * r3z;
            double b3 = (double)p4.x * p4.x + (double)p4.y * p4.y + (double)p4.z * p4.z
                        - ((double)p1.x * p1.x + (double)p1.y * p1.y + (double)p1.z * p1.z);

            double det = a11 * (a22 * a33 - a23 * a32) - a12 * (a21 * a33 - a23 * a31) + a13 * (a21 * a32 - a22 * a31);
            if (Math.Abs(det) < 1e-12)
            {
                center = default;
                r2 = 0.0;
                return false;
            }

            double detX = b1 * (a22 * a33 - a23 * a32) - a12 * (b2 * a33 - a23 * b3) + a13 * (b2 * a32 - a22 * b3);
            double detY = a11 * (b2 * a33 - a23 * b3) - b1 * (a21 * a33 - a23 * a31) + a13 * (a21 * b3 - b2 * a31);
            double detZ = a11 * (a22 * b3 - b2 * a32) - a12 * (a21 * b3 - b2 * a31) + b1 * (a21 * a32 - a22 * a31);

            double cx = detX / det;
            double cy = detY / det;
            double cz = detZ / det;
            center = new Vector3((float)cx, (float)cy, (float)cz);

            double dx = cx - p1.x;
            double dy = cy - p1.y;
            double dz = cz - p1.z;
            r2 = dx * dx + dy * dy + dz * dz;
            return true;
        }

        private struct MarkValidVoronoiVerticesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> positions;
            [ReadOnly] public NativeArray<int4> tetVerts;
            public float minRadius;
            public float maxRadius;
            public NativeArray<byte> valid;

            public void Execute(int i)
            {
                int4 t = tetVerts[i];
                if (t.x < 0 || t.y < 0 || t.z < 0 || t.w < 0)
                {
                    valid[i] = 0;
                    return;
                }

                float3 p1 = positions[t.x];
                float3 p2 = positions[t.y];
                float3 p3 = positions[t.z];
                float3 p4 = positions[t.w];
                if (!TryCircumsphere4PreciseDouble(p1, p2, p3, p4, out double cx, out double cy, out double cz, out double r))
                {
                    valid[i] = 0;
                    return;
                }

                const double tol = 1e-5;
                if (r + tol < minRadius || r - tol > maxRadius)
                {
                    valid[i] = 0;
                    return;
                }

                double dx1 = cx - p1.x;
                double dy1 = cy - p1.y;
                double dz1 = cz - p1.z;
                double dx2 = cx - p2.x;
                double dy2 = cy - p2.y;
                double dz2 = cz - p2.z;
                double dx3 = cx - p3.x;
                double dy3 = cy - p3.y;
                double dz3 = cz - p3.z;
                double dx4 = cx - p4.x;
                double dy4 = cy - p4.y;
                double dz4 = cz - p4.z;
                double d1 = Math.Sqrt(dx1 * dx1 + dy1 * dy1 + dz1 * dz1);
                double d2 = Math.Sqrt(dx2 * dx2 + dy2 * dy2 + dz2 * dz2);
                double d3 = Math.Sqrt(dx3 * dx3 + dy3 * dy3 + dz3 * dz3);
                double d4 = Math.Sqrt(dx4 * dx4 + dy4 * dy4 + dz4 * dz4);
                if (Math.Abs(d1 - d2) > tol || Math.Abs(d1 - d3) > tol || Math.Abs(d1 - d4) > tol)
                {
                    valid[i] = 0;
                    return;
                }

                valid[i] = 1;
            }
        }

        private struct WriteVoronoiVerticesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> positions;
            [ReadOnly] public NativeArray<float> electroneg;
            [ReadOnly] public NativeArray<int4> tetVerts;
            [ReadOnly] public NativeArray<int> tetToSphere;
            public float minRadius;
            public float maxRadius;
            public int minApolNeigh;
            [NativeDisableParallelForRestriction] public NativeArray<float3> sphereCenters;
            [NativeDisableParallelForRestriction] public NativeArray<float> sphereRadii;
            [NativeDisableParallelForRestriction] public NativeArray<byte> sphereIsApolar;
            [NativeDisableParallelForRestriction] public NativeArray<int4> sphereParents;

            public void Execute(int i)
            {
                int sphereIdx = tetToSphere[i];
                if (sphereIdx < 0)
                    return;

                int4 t = tetVerts[i];
                float3 p1 = positions[t.x];
                float3 p2 = positions[t.y];
                float3 p3 = positions[t.z];
                float3 p4 = positions[t.w];
                if (!TryCircumsphere4PreciseDouble(p1, p2, p3, p4, out double cx, out double cy, out double cz, out double r))
                    return;

                const double tol = 1e-5;
                if (r + tol < minRadius || r - tol > maxRadius)
                    return;

                float3 c = new float3((float)cx, (float)cy, (float)cz);
                float rf = (float)r;
                int apol = 0;
                if (electroneg[t.x] < 2.8f) apol++;
                if (electroneg[t.y] < 2.8f) apol++;
                if (electroneg[t.z] < 2.8f) apol++;
                if (electroneg[t.w] < 2.8f) apol++;

                sphereCenters[sphereIdx] = c;
                sphereRadii[sphereIdx] = rf;
                sphereIsApolar[sphereIdx] = (byte)(apol >= minApolNeigh ? 1 : 0);
                sphereParents[sphereIdx] = t;
            }
        }

        private struct WriteVoronoiVneighJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int4> tetNeigh;
            [ReadOnly] public NativeArray<int> tetToSphere;
            [NativeDisableParallelForRestriction] public NativeArray<int4> outVneigh;

            public void Execute(int i)
            {
                int sphereIdx = tetToSphere[i];
                if (sphereIdx < 0)
                    return;

                int4 tn = tetNeigh[i];
                int n0 = tn.x >= 0 ? tetToSphere[tn.x] : -1;
                int n1 = tn.y >= 0 ? tetToSphere[tn.y] : -1;
                int n2 = tn.z >= 0 ? tetToSphere[tn.z] : -1;
                int n3 = tn.w >= 0 ? tetToSphere[tn.w] : -1;
                outVneigh[sphereIdx] = new int4(n0, n1, n2, n3);
            }
        }

        private int4[] BuildVneighFromSphereParents(List<FPocketAlphaSphere> spheres)
        {
            int n = spheres.Count;
            int4[] vneigh = new int4[n];
            for (int i = 0; i < n; i++) vneigh[i] = new int4(-1, -1, -1, -1);

            Dictionary<FaceKey, (int sphere, int slot)> map = new Dictionary<FaceKey, (int, int)>(n * 2);

            void Set(int i, int slot, int j)
            {
                int4 vn = vneigh[i];
                if (slot == 0) vn.x = j;
                else if (slot == 1) vn.y = j;
                else if (slot == 2) vn.z = j;
                else vn.w = j;
                vneigh[i] = vn;
            }

            for (int i = 0; i < n; i++)
            {
                int[] p = spheres[i].parent_atoms;
                if (p == null || p.Length < 4) continue;
                int a = p[0], b = p[1], c = p[2], d = p[3];

                void ProcessFace(int slot, int x, int y, int z)
                {
                    FaceKey key = new FaceKey(x, y, z);
                    if (map.TryGetValue(key, out var other))
                    {
                        Set(i, slot, other.sphere);
                        Set(other.sphere, other.slot, i);
                        map.Remove(key);
                    }
                    else
                    {
                        map.Add(key, (i, slot));
                    }
                }

                ProcessFace(0, b, c, d);
                ProcessFace(1, a, c, d);
                ProcessFace(2, a, b, d);
                ProcessFace(3, a, b, c);
            }

            return vneigh;
        }

        private struct CountAlphaSpheresFPocketDirJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> positions;
            [ReadOnly] public NativeArray<float> electroneg;
            [ReadOnly] public NativeArray<int> neighborCounts;
            [ReadOnly] public NativeArray<int> neighborIndices;
            public int maxNeighbors;
            public float minRadius;
            public float maxRadius;
            public float sphereAtomEps;
            public int minApolNeigh;
            public NativeArray<int> outCounts;

            public void Execute(int atomIdx)
            {
                int neighborCount = neighborCounts[atomIdx];
                if (neighborCount > maxNeighbors) neighborCount = maxNeighbors;
                if (neighborCount < 3) { outCounts[atomIdx] = 0; return; }

                int baseOffset = atomIdx * maxNeighbors;
                float3 p1 = positions[atomIdx];
                int localCount = 0;

                for (int j = 0; j < neighborCount - 2; j++)
                {
                    int atomJ = neighborIndices[baseOffset + j];
                    if (atomJ < 0 || atomJ <= atomIdx)
                        continue;
                    float3 p2 = positions[atomJ];

                    for (int k = j + 1; k < neighborCount - 1; k++)
                    {
                        int atomK = neighborIndices[baseOffset + k];
                        if (atomK < 0 || atomK <= atomJ)
                            continue;
                        float3 p3 = positions[atomK];

                        for (int l = k + 1; l < neighborCount; l++)
                        {
                            int atomL = neighborIndices[baseOffset + l];
                            if (atomL < 0 || atomL <= atomK)
                                continue;
                            float3 p4 = positions[atomL];

                            if (!TryCircumsphere4(p1, p2, p3, p4, out float3 center, out float radius))
                                continue;
                            if (radius < minRadius || radius > maxRadius)
                                continue;

                            float rr = radius - sphereAtomEps;
                            float rrSq = rr * rr;
                            bool empty = true;
                            for (int t = 0; t < positions.Length; t++)
                            {
                                if (t == atomIdx || t == atomJ || t == atomK || t == atomL)
                                    continue;
                                float3 d = positions[t] - center;
                                if (math.dot(d, d) < rrSq)
                                {
                                    empty = false;
                                    break;
                                }
                            }
                            if (!empty)
                                continue;

                            int apol = 0;
                            if (electroneg[atomIdx] < 2.8f) apol++;
                            if (electroneg[atomJ] < 2.8f) apol++;
                            if (electroneg[atomK] < 2.8f) apol++;
                            if (electroneg[atomL] < 2.8f) apol++;
                            localCount++;
                        }
                    }
                }

                outCounts[atomIdx] = localCount;
            }
        }

        private struct WriteAlphaSpheresFPocketDirJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> positions;
            [ReadOnly] public NativeArray<float> electroneg;
            [ReadOnly] public NativeArray<int> neighborCounts;
            [ReadOnly] public NativeArray<int> neighborIndices;
            public int maxNeighbors;
            public float minRadius;
            public float maxRadius;
            public float sphereAtomEps;
            public int minApolNeigh;
            [ReadOnly] public NativeArray<int> writeOffsets;
            [ReadOnly] public NativeArray<int> writeCounts;
            [NativeDisableParallelForRestriction] public NativeArray<AlphaSphereOut> outSpheres;

            public void Execute(int atomIdx)
            {
                int toWrite = writeCounts[atomIdx];
                if (toWrite <= 0)
                    return;

                int neighborCount = neighborCounts[atomIdx];
                if (neighborCount > maxNeighbors) neighborCount = maxNeighbors;
                if (neighborCount < 3)
                    return;

                int baseOffset = atomIdx * maxNeighbors;
                float3 p1 = positions[atomIdx];
                int dstOffset = writeOffsets[atomIdx];
                int written = 0;

                for (int j = 0; j < neighborCount - 2 && written < toWrite; j++)
                {
                    int atomJ = neighborIndices[baseOffset + j];
                    if (atomJ < 0 || atomJ <= atomIdx)
                        continue;
                    float3 p2 = positions[atomJ];

                    for (int k = j + 1; k < neighborCount - 1 && written < toWrite; k++)
                    {
                        int atomK = neighborIndices[baseOffset + k];
                        if (atomK < 0 || atomK <= atomJ)
                            continue;
                        float3 p3 = positions[atomK];

                        for (int l = k + 1; l < neighborCount && written < toWrite; l++)
                        {
                            int atomL = neighborIndices[baseOffset + l];
                            if (atomL < 0 || atomL <= atomK)
                                continue;
                            float3 p4 = positions[atomL];

                            if (!TryCircumsphere4(p1, p2, p3, p4, out float3 center, out float radius))
                                continue;
                            if (radius < minRadius || radius > maxRadius)
                                continue;

                            float rr = radius - sphereAtomEps;
                            float rrSq = rr * rr;
                            bool empty = true;
                            for (int t = 0; t < positions.Length; t++)
                            {
                                if (t == atomIdx || t == atomJ || t == atomK || t == atomL)
                                    continue;
                                float3 d = positions[t] - center;
                                if (math.dot(d, d) < rrSq)
                                {
                                    empty = false;
                                    break;
                                }
                            }
                            if (!empty)
                                continue;

                            int apol = 0;
                            if (electroneg[atomIdx] < 2.8f) apol++;
                            if (electroneg[atomJ] < 2.8f) apol++;
                            if (electroneg[atomK] < 2.8f) apol++;
                            if (electroneg[atomL] < 2.8f) apol++;

                            AlphaSphereOut s;
                            s.cx = center.x;
                            s.cy = center.y;
                            s.cz = center.z;
                            s.radius = radius;
                            s.a = atomIdx;
                            s.b = atomJ;
                            s.c = atomK;
                            s.d = atomL;
                            s.isApolar = (byte)(apol >= minApolNeigh ? 1 : 0);
                            outSpheres[dstOffset + written] = s;
                            written++;
                        }
                    }
                }
            }
        }

        private (Vector3 center, float radius) ComputeCircumsphere(Vector3 p1, Vector3 p2, Vector3 p3)
        {
            
            Vector3 v1 = p2 - p1;
            Vector3 v2 = p3 - p1;

            
            Vector3 n = Vector3.Cross(v1, v2);
            if (n.magnitude < 1e-6) 
                return (Vector3.zero, 0f);

            
            float a11 = 2 * (p2.x - p1.x);
            float a12 = 2 * (p2.y - p1.y);
            float a13 = 2 * (p2.z - p1.z);
            float b1 = p2.sqrMagnitude - p1.sqrMagnitude;

            float a21 = 2 * (p3.x - p1.x);
            float a22 = 2 * (p3.y - p1.y);
            float a23 = 2 * (p3.z - p1.z);
            float b2 = p3.sqrMagnitude - p1.sqrMagnitude;

            float a31 = n.x;
            float a32 = n.y;
            float a33 = n.z;
            float b3 = Vector3.Dot(n, p1);

            
            float det = a11 * (a22 * a33 - a23 * a32) - a12 * (a21 * a33 - a23 * a31) + a13 * (a21 * a32 - a22 * a31);
            if (Mathf.Abs(det) < 1e-6)
                return (Vector3.zero, 0f);

            float detX = b1 * (a22 * a33 - a23 * a32) - a12 * (b2 * a33 - a23 * b3) + a13 * (b2 * a32 - a22 * b3);
            float detY = a11 * (b2 * a33 - a23 * b3) - b1 * (a21 * a33 - a23 * a31) + a13 * (a21 * b3 - b2 * a31);
            float detZ = a11 * (a22 * b3 - b2 * a32) - a12 * (a21 * b3 - b2 * a31) + b1 * (a21 * a32 - a22 * a31);

            Vector3 center = new Vector3(detX / det, detY / det, detZ / det);
            float radius = Vector3.Distance(center, p1);

            return (center, radius);
        }

        private (Vector3 center, float radius) ComputeCircumsphere(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4)
        {
            Vector3 r1 = p2 - p1;
            Vector3 r2 = p3 - p1;
            Vector3 r3 = p4 - p1;

            float a11 = 2f * r1.x;
            float a12 = 2f * r1.y;
            float a13 = 2f * r1.z;
            float b1 = p2.sqrMagnitude - p1.sqrMagnitude;

            float a21 = 2f * r2.x;
            float a22 = 2f * r2.y;
            float a23 = 2f * r2.z;
            float b2 = p3.sqrMagnitude - p1.sqrMagnitude;

            float a31 = 2f * r3.x;
            float a32 = 2f * r3.y;
            float a33 = 2f * r3.z;
            float b3 = p4.sqrMagnitude - p1.sqrMagnitude;

            float det = a11 * (a22 * a33 - a23 * a32) - a12 * (a21 * a33 - a23 * a31) + a13 * (a21 * a32 - a22 * a31);
            if (Mathf.Abs(det) < 1e-6f)
                return (Vector3.zero, 0f);

            float detX = b1 * (a22 * a33 - a23 * a32) - a12 * (b2 * a33 - a23 * b3) + a13 * (b2 * a32 - a22 * b3);
            float detY = a11 * (b2 * a33 - a23 * b3) - b1 * (a21 * a33 - a23 * a31) + a13 * (a21 * b3 - b2 * a31);
            float detZ = a11 * (a22 * b3 - b2 * a32) - a12 * (a21 * b3 - b2 * a31) + b1 * (a21 * a32 - a22 * a31);

            Vector3 center = new Vector3(detX / det, detY / det, detZ / det);
            float radius = Vector3.Distance(center, p1);
            return (center, radius);
        }
        private bool IsEmptySphere(Vector3 center, float radius, List<FPocketAtom> atoms, int i, int j, int k)
        {
            float radiusSq = (radius - FPocketConstants.SPHERE_ATOM_EPS) * (radius - FPocketConstants.SPHERE_ATOM_EPS);

            foreach (var atom in atoms)
            {
                
                if (atom.id == i || atom.id == j || atom.id == k)
                    continue;

                
                float distSq = (atom.pos - center).sqrMagnitude;

                
                if (distSq < radiusSq)
                    return false;
            }

            return true;
        }

        private bool IsEmptySphere(Vector3 center, float radius, List<FPocketAtom> atoms, int i, int j, int k, int l)
        {
            float radiusSq = (radius - FPocketConstants.SPHERE_ATOM_EPS) * (radius - FPocketConstants.SPHERE_ATOM_EPS);

            foreach (var atom in atoms)
            {
                if (atom.id == i || atom.id == j || atom.id == k || atom.id == l)
                    continue;

                float distSq = (atom.pos - center).sqrMagnitude;
                if (distSq < radiusSq)
                    return false;
            }

            return true;
        }
        private bool IsSphereCenterOutsideMolecule(Vector3 center, List<FPocketAtom> atoms)
        {
            foreach (var atom in atoms)
            {
                float dist = Vector3.Distance(center, atom.pos);
                
                if (dist < atom.vdw_radius + FPocketConstants.PROBE_RADIUS)
                    return false;
            }
            return true;
        }
        private (int nbAtoms, float totalHydro) CountEnclosedAtoms(Vector3 center, float radius, List<FPocketAtom> atoms)
        {
            int nbAtoms = 0;
            float totalHydro = 0f;

            foreach (var atom in atoms)
            {
                float dist = Vector3.Distance(center, atom.pos);
                
                if (dist < radius + atom.vdw_radius)
                {
                    nbAtoms++;
                    totalHydro += atom.hydrophobicity;
                }
            }

            return (nbAtoms, totalHydro);
        }
        private List<FPocketAlphaSphere> FilterAlphaSpheres(List<FPocketAlphaSphere> spheres)
        {
            return spheres.Where(s =>
                s.radius >= FPocketConstants.MIN_ALPHA_SPHERE_RADIUS &&
                s.radius <= FPocketConstants.MAX_ALPHA_SPHERE_RADIUS &&
                s.nb_atoms >= 1 &&
                s.hydrophobicity >= 0.1f
            ).ToList();
        }

        private struct FPocketDesc
        {
            public int nb_asph;
            public float apolar_asphere_prop;
            public float mean_loc_hyd_dens;
            public int polarity_score;
            public float hydrophobicity_score;
            public float as_density;
            public float as_max_dst;
            public float mean_asph_ray;
            public float masph_sacc;
            public float volume;

            public float as_max_dst_norm;
            public float as_density_norm;
            public float polarity_score_norm;
            public float mean_loc_hyd_dens_norm;
            public float nas_norm;
            public float prop_asapol_norm;
        }

        private List<FPocketResult> DetectPocketsFPocketDir(List<FPocketAlphaSphere> spheres)
        {
            List<FPocketResult> pockets = new List<FPocketResult>();
            if (spheres == null || spheres.Count == 0)
            {
                Debug.Log($"FPocketDir: no spheres (heavy_atoms={fpocketDirLastHeavyAtomCount}, tets={fpocketDirLastTetCount}, kept_spheres={fpocketDirLastValidTetCount})");
                return pockets;
            }

            if (fpocketDirVneigh == null || fpocketDirVneigh.Length != spheres.Count)
                fpocketDirVneigh = BuildVneighFromSphereParents(spheres);

            List<List<int>> clusters = ClusterSpheresByAdjacencyFPocketDir(spheres, FPocketDirDefaults.ClustMaxDist);
            clusters = RefineClustersByBarycenter(spheres, clusters, FPocketDirDefaults.RefineClustDist);

            FPocketDesc[] preFinalDescs = clusters.Select(c => ComputeDescriptorsFPocketDir(spheres, c, false)).ToArray();
            int preGeMin = 0;
            int preMaxAsph = 0;
            for (int i = 0; i < preFinalDescs.Length; i++)
            {
                int nb = preFinalDescs[i].nb_asph;
                if (nb > preMaxAsph) preMaxAsph = nb;
                if (nb >= FPocketDirDefaults.MinPocketNbAsph) preGeMin++;
            }
            clusters = FinalClusterFPocketDir(spheres, clusters, preFinalDescs);

            FPocketDesc[] finalDescs = clusters.Select(c => ComputeDescriptorsFPocketDir(spheres, c, true)).ToArray();
            NormalizeDescriptorsFPocketDir(finalDescs);

            List<(FPocketResult result, FPocketDesc desc)> results = new List<(FPocketResult, FPocketDesc)>(clusters.Count);
            int maxAsph = 0;
            int geMin = 0;
            for (int i = 0; i < clusters.Count; i++)
            {
                FPocketDesc d = finalDescs[i];
                if (d.nb_asph > maxAsph) maxAsph = d.nb_asph;
                if (d.nb_asph >= FPocketDirDefaults.MinPocketNbAsph) geMin++;
                if (d.nb_asph < FPocketDirDefaults.MinPocketNbAsph)
                    continue;
                if (d.apolar_asphere_prop < FPocketDirDefaults.RefineMinApolarProp)
                    continue;

                Vector3 center = ComputeClusterCenter(spheres, clusters[i]);
                int nbAtoms = CountUniqueContactAtoms(clusters[i], spheres);
                float score = ScorePocketFPocketDir(d);

                results.Add((new FPocketResult
                {
                    id = 0,
                    center = center,
                    volume = d.volume,
                    score = score,
                    hydrophobic_score = d.hydrophobicity_score,
                    polar_score = d.polarity_score,
                    depth_score = d.masph_sacc,
                    nb_alpha_spheres = d.nb_asph,
                    nb_atoms = nbAtoms,
                    density = d.as_density
                }, d));
            }

            results.Sort((a, b) => b.result.score.CompareTo(a.result.score));
            for (int i = 0; i < results.Count; i++)
            {
                FPocketResult r = results[i].result;
                r.id = i;
                pockets.Add(r);
            }

            int rawDegSum = 0;
            int rawDegNonNeg = 0;
            int filteredEdges = 0;
            int vneighAny = 0;
            int sharedFacePairs = 0;
            if (fpocketDirVneigh != null && fpocketDirVneigh.Length == spheres.Count)
            {
                float distSqr = FPocketDirDefaults.ClustMaxDist * FPocketDirDefaults.ClustMaxDist;
                for (int i = 0; i < spheres.Count; i++)
                {
                    int4 vn = fpocketDirVneigh[i];
                    int deg = 0;
                    void Add(int j)
                    {
                        if (j < 0) return;
                        deg++;
                        if (j >= 0) vneighAny = 1;
                        if (j > i)
                        {
                            Vector3 d = spheres[i].center - spheres[j].center;
                            if (d.sqrMagnitude <= distSqr) filteredEdges++;
                        }
                    }

                    Add(vn.x);
                    Add(vn.y);
                    Add(vn.z);
                    Add(vn.w);
                    rawDegSum += deg;
                    rawDegNonNeg++;
                }

                Dictionary<FaceKey, int> faceCounts = new Dictionary<FaceKey, int>(spheres.Count * 2);
                for (int i = 0; i < spheres.Count; i++)
                {
                    int[] p = spheres[i].parent_atoms;
                    if (p == null || p.Length < 4) continue;
                    FaceKey f0 = new FaceKey(p[1], p[2], p[3]);
                    FaceKey f1 = new FaceKey(p[0], p[2], p[3]);
                    FaceKey f2 = new FaceKey(p[0], p[1], p[3]);
                    FaceKey f3 = new FaceKey(p[0], p[1], p[2]);
                    void Inc(FaceKey k)
                    {
                        if (faceCounts.TryGetValue(k, out int c)) faceCounts[k] = c + 1;
                        else faceCounts[k] = 1;
                    }
                    Inc(f0); Inc(f1); Inc(f2); Inc(f3);
                }
                foreach (var kv in faceCounts)
                {
                    if (kv.Value >= 2) sharedFacePairs += kv.Value / 2;
                }
            }

            float avgDeg = rawDegNonNeg > 0 ? (float)rawDegSum / rawDegNonNeg : 0f;
            Debug.Log($"FPocketDir: heavy_atoms={fpocketDirLastHeavyAtomCount}, tets={fpocketDirLastTetCount}, kept_spheres={fpocketDirLastValidTetCount}, spheres={spheres.Count}, pre_clusters={preFinalDescs.Length}, pre_geMin={preGeMin}, pre_max_nb_asph={preMaxAsph}, clusters={clusters.Count}, max_nb_asph={maxAsph}, geMin={geMin}, minRequired={FPocketDirDefaults.MinPocketNbAsph}, vneigh_len={(fpocketDirVneigh == null ? -1 : fpocketDirVneigh.Length)}, avg_deg={avgDeg:F2}, filtered_edges={filteredEdges}, shared_face_pairs={sharedFacePairs}, has_any_vneigh={vneighAny}, kept_pockets={pockets.Count}");

            if (pockets.Count == 12)
            {
                List<(int nb, float apolProp, float densPer, Vector3 center)> near = new List<(int, float, float, Vector3)>();
                for (int i = 0; i < clusters.Count; i++)
                {
                    FPocketDesc d = finalDescs[i];
                    if (d.nb_asph >= 30 && d.nb_asph < FPocketDirDefaults.MinPocketNbAsph)
                    {
                        float densPer = d.nb_asph > 0 ? d.as_density / d.nb_asph : 0f;
                        Vector3 c = ComputeClusterCenter(spheres, clusters[i]);
                        c.z = -c.z;
                        near.Add((d.nb_asph, d.apolar_asphere_prop, densPer, c));
                    }
                }
                near.Sort((a, b) => b.nb.CompareTo(a.nb));
                int take = Mathf.Min(5, near.Count);
                for (int i = 0; i < take; i++)
                {
                    var x = near[i];
                    Debug.Log($"FPocketDir: near_miss nb_asph={x.nb}, apol_prop={x.apolProp:F3}, densPer={x.densPer:F4}, center={x.center}");
                }
            }

            return pockets;
        }

        private float ScorePocketFPocketDir(FPocketDesc d)
        {
            return -0.65784f
                   + 29.78270f * d.nas_norm
                   - 4.06632f * d.prop_asapol_norm
                   + 11.72346f * d.mean_loc_hyd_dens_norm
                   + 1.16349f * d.polarity_score
                   - 2.06835f * d.as_density;
        }

        private List<List<int>> ClusterSpheresByDistance(List<FPocketAlphaSphere> spheres, float dist)
        {
            int n = spheres.Count;
            if (n == 0)
                return new List<List<int>>();

            float distSqr = dist * dist;
            Dictionary<Vector3Int, List<int>> spatialIndex = BuildSpatialHash(spheres, dist);

            int[] parent = new int[n];
            int[] rank = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;

            int Find(int x)
            {
                while (parent[x] != x)
                {
                    parent[x] = parent[parent[x]];
                    x = parent[x];
                }
                return x;
            }

            void Union(int a, int b)
            {
                int ra = Find(a);
                int rb = Find(b);
                if (ra == rb) return;
                if (rank[ra] < rank[rb]) parent[ra] = rb;
                else if (rank[ra] > rank[rb]) parent[rb] = ra;
                else
                {
                    parent[rb] = ra;
                    rank[ra]++;
                }
            }

            for (int i = 0; i < n; i++)
            {
                Vector3 center = spheres[i].center;
                Vector3Int cell = GetSpatialCell(center, dist);
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    Vector3Int neighborCell = new Vector3Int(cell.x + dx, cell.y + dy, cell.z + dz);
                    if (!spatialIndex.TryGetValue(neighborCell, out var bucket))
                        continue;

                    for (int b = 0; b < bucket.Count; b++)
                    {
                        int j = bucket[b];
                        if (j <= i) continue;
                        Vector3 delta = center - spheres[j].center;
                        if (delta.sqrMagnitude <= distSqr)
                            Union(i, j);
                    }
                }
            }

            Dictionary<int, List<int>> groups = new Dictionary<int, List<int>>();
            for (int i = 0; i < n; i++)
            {
                int r = Find(i);
                if (!groups.TryGetValue(r, out var lst))
                {
                    lst = new List<int>();
                    groups[r] = lst;
                }
                lst.Add(i);
            }

            return groups.Values.ToList();
        }

        private List<List<int>> ClusterSpheresByAdjacencyFPocketDir(List<FPocketAlphaSphere> spheres, float dist)
        {
            if (fpocketDirVneigh != null && fpocketDirVneigh.Length == spheres.Count)
                return ClusterSpheresByVneighJobs(spheres, fpocketDirVneigh, dist);
            return ClusterSpheresByAdjacencyFPocketDirJobs(spheres, dist);
        }

        private struct CountVneighEdgesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> centers;
            [ReadOnly] public NativeArray<int4> vneigh;
            public float distSqr;
            public NativeArray<int> outCounts;

            void MaybeCount(int j, ref int i, ref float3 ci, ref int count)
            {
                if (j <= i) return;
                float3 d = ci - centers[j];
                if (math.lengthsq(d) <= distSqr) count++;
            }

            public void Execute(int i)
            {
                float3 ci = centers[i];
                int4 vn = vneigh[i];
                int count = 0;

                if (vn.x >= 0) MaybeCount(vn.x, ref i, ref ci, ref count);
                if (vn.y >= 0) MaybeCount(vn.y, ref i, ref ci, ref count);
                if (vn.z >= 0) MaybeCount(vn.z, ref i, ref ci, ref count);
                if (vn.w >= 0) MaybeCount(vn.w, ref i, ref ci, ref count);

                outCounts[i] = count;
            }
        }

        private struct WriteVneighEdgesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> centers;
            [ReadOnly] public NativeArray<int4> vneigh;
            [ReadOnly] public NativeArray<int> offsets;
            public float distSqr;
            [NativeDisableParallelForRestriction] public NativeArray<int2> edges;

            void MaybeWrite(int j, ref int i, ref float3 ci, ref int o, ref int w)
            {
                if (j <= i) return;
                float3 d = ci - centers[j];
                if (math.lengthsq(d) <= distSqr)
                {
                    edges[o + w] = new int2(i, j);
                    w++;
                }
            }

            public void Execute(int i)
            {
                float3 ci = centers[i];
                int4 vn = vneigh[i];
                int o = offsets[i];
                int w = 0;

                if (vn.x >= 0) MaybeWrite(vn.x, ref i, ref ci, ref o, ref w);
                if (vn.y >= 0) MaybeWrite(vn.y, ref i, ref ci, ref o, ref w);
                if (vn.z >= 0) MaybeWrite(vn.z, ref i, ref ci, ref o, ref w);
                if (vn.w >= 0) MaybeWrite(vn.w, ref i, ref ci, ref o, ref w);
            }
        }

        private List<List<int>> ClusterSpheresByVneighJobs(List<FPocketAlphaSphere> spheres, int4[] vneigh, float dist)
        {
            int n = spheres.Count;
            if (n == 0)
                return new List<List<int>>();

            float distSqr = dist * dist;

            NativeArray<float3> centersNA = new NativeArray<float3>(n, Allocator.TempJob);
            NativeArray<int4> vneighNA = new NativeArray<int4>(vneigh, Allocator.TempJob);
            for (int i = 0; i < n; i++)
            {
                Vector3 c = spheres[i].center;
                centersNA[i] = new float3(c.x, c.y, c.z);
            }

            NativeArray<int> countsNA = new NativeArray<int>(n, Allocator.TempJob);
            var countJob = new CountVneighEdgesJob { centers = centersNA, vneigh = vneighNA, distSqr = distSqr, outCounts = countsNA };
            countJob.Schedule(n, 64).Complete();

            NativeArray<int> offsetsNA = new NativeArray<int>(n, Allocator.TempJob);
            int total = 0;
            for (int i = 0; i < n; i++)
            {
                offsetsNA[i] = total;
                total += countsNA[i];
            }

            NativeArray<int2> edgesNA = new NativeArray<int2>(total, Allocator.TempJob);
            var writeJob = new WriteVneighEdgesJob { centers = centersNA, vneigh = vneighNA, offsets = offsetsNA, distSqr = distSqr, edges = edgesNA };
            writeJob.Schedule(n, 64).Complete();

            int2[] edges = edgesNA.ToArray();

            edgesNA.Dispose();
            offsetsNA.Dispose();
            countsNA.Dispose();
            vneighNA.Dispose();
            centersNA.Dispose();

            int[] parent = new int[n];
            int[] rank = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;

            int Find(int x)
            {
                while (parent[x] != x)
                {
                    parent[x] = parent[parent[x]];
                    x = parent[x];
                }
                return x;
            }

            void Union(int a, int b)
            {
                int ra = Find(a);
                int rb = Find(b);
                if (ra == rb) return;
                if (rank[ra] < rank[rb]) parent[ra] = rb;
                else if (rank[ra] > rank[rb]) parent[rb] = ra;
                else
                {
                    parent[rb] = ra;
                    rank[ra]++;
                }
            }

            for (int i = 0; i < edges.Length; i++)
            {
                int2 e = edges[i];
                Union(e.x, e.y);
            }

            Dictionary<int, List<int>> groups = new Dictionary<int, List<int>>();
            for (int i = 0; i < n; i++)
            {
                int r = Find(i);
                if (!groups.TryGetValue(r, out var lst))
                {
                    lst = new List<int>();
                    groups[r] = lst;
                }
                lst.Add(i);
            }

            return groups.Values.ToList();
        }

        private struct TripleEntry
        {
            public uint key;
            public int sphere;
        }

        private struct BuildTripleEntriesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int4> parents;
            [NativeDisableParallelForRestriction] public NativeArray<TripleEntry> entries;

            public void Execute(int i)
            {
                int4 p = parents[i];
                int baseIdx = i * 4;

                entries[baseIdx + 0] = new TripleEntry { key = HashTriple(p.x, p.y, p.z), sphere = i };
                entries[baseIdx + 1] = new TripleEntry { key = HashTriple(p.x, p.y, p.w), sphere = i };
                entries[baseIdx + 2] = new TripleEntry { key = HashTriple(p.x, p.z, p.w), sphere = i };
                entries[baseIdx + 3] = new TripleEntry { key = HashTriple(p.y, p.z, p.w), sphere = i };
            }

            private static uint HashTriple(int a, int b, int c)
            {
                if (a > b) (a, b) = (b, a);
                if (b > c) (b, c) = (c, b);
                if (a > b) (a, b) = (b, a);
                return (uint)math.hash(new int3(a, b, c));
            }
        }

        private List<List<int>> ClusterSpheresByAdjacencyFPocketDirJobs(List<FPocketAlphaSphere> spheres, float dist)
        {
            int n = spheres.Count;
            if (n == 0)
                return new List<List<int>>();

            float distSqr = dist * dist;

            NativeArray<int4> parentsNA = new NativeArray<int4>(n, Allocator.TempJob);
            for (int i = 0; i < n; i++)
            {
                int[] pa = spheres[i].parent_atoms;
                if (pa != null && pa.Length >= 4)
                    parentsNA[i] = new int4(pa[0], pa[1], pa[2], pa[3]);
                else
                    parentsNA[i] = new int4(-1, -1, -1, -1);
            }

            NativeArray<TripleEntry> entriesNA = new NativeArray<TripleEntry>(n * 4, Allocator.TempJob);
            var job = new BuildTripleEntriesJob { parents = parentsNA, entries = entriesNA };
            job.Schedule(n, 64).Complete();

            TripleEntry[] entries = entriesNA.ToArray();
            entriesNA.Dispose();
            parentsNA.Dispose();

            Array.Sort(entries, (a, b) => a.key.CompareTo(b.key));

            int[] parent = new int[n];
            int[] rank = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;

            int Find(int x)
            {
                while (parent[x] != x)
                {
                    parent[x] = parent[parent[x]];
                    x = parent[x];
                }
                return x;
            }

            void Union(int a, int b)
            {
                int ra = Find(a);
                int rb = Find(b);
                if (ra == rb) return;
                if (rank[ra] < rank[rb]) parent[ra] = rb;
                else if (rank[ra] > rank[rb]) parent[rb] = ra;
                else
                {
                    parent[rb] = ra;
                    rank[ra]++;
                }
            }

            int start = 0;
            while (start < entries.Length)
            {
                uint key = entries[start].key;
                int end = start + 1;
                while (end < entries.Length && entries[end].key == key) end++;

                int groupSize = end - start;
                if (groupSize > 1)
                {
                    for (int i = start; i < end - 1; i++)
                    {
                        int si = entries[i].sphere;
                        Vector3 ci = spheres[si].center;
                        for (int j = i + 1; j < end; j++)
                        {
                            int sj = entries[j].sphere;
                            Vector3 delta = ci - spheres[sj].center;
                            if (delta.sqrMagnitude <= distSqr)
                                Union(si, sj);
                        }
                    }
                }

                start = end;
            }

            Dictionary<int, List<int>> clustersByRoot = new Dictionary<int, List<int>>();
            for (int i = 0; i < n; i++)
            {
                int r = Find(i);
                if (!clustersByRoot.TryGetValue(r, out List<int> lst))
                {
                    lst = new List<int>();
                    clustersByRoot[r] = lst;
                }
                lst.Add(i);
            }

            return clustersByRoot.Values.ToList();
        }

        private List<List<int>> RefineClustersByBarycenter(List<FPocketAlphaSphere> spheres, List<List<int>> clusters, float dist)
        {
            int m = clusters.Count;
            if (m <= 1)
                return clusters;

            float distSqr = dist * dist;
            Vector3[] bary = new Vector3[m];
            for (int i = 0; i < m; i++)
                bary[i] = ComputeClusterCenter(spheres, clusters[i]);

            List<List<int>> work = new List<List<int>>(m);
            for (int i = 0; i < m; i++)
                work.Add(new List<int>(clusters[i]));

            bool[] alive = new bool[m];
            for (int i = 0; i < m; i++)
                alive[i] = work[i].Count > 0;

            for (int i = 0; i < m; i++)
            {
                if (!alive[i]) continue;

                for (int j = i + 1; j < m; j++)
                {
                    if (!alive[j]) continue;

                    Vector3 delta = bary[i] - bary[j];
                    if (delta.sqrMagnitude < distSqr)
                    {
                        work[i].AddRange(work[j]);
                        work[j].Clear();
                        alive[j] = false;
                    }
                }
            }

            List<List<int>> merged = new List<List<int>>();
            for (int i = 0; i < m; i++)
            {
                if (alive[i] && work[i].Count > 0)
                    merged.Add(work[i]);
            }

            return merged;
        }

        private struct PocketPair
        {
            public int pid1;
            public int pid2;
            public int dist;
        }

        private List<List<int>> FinalClusterFPocketDir(List<FPocketAlphaSphere> spheres, List<List<int>> pockets, FPocketDesc[] preDescs)
        {
            int n = pockets.Count;
            if (n <= 1)
                return pockets;

            float distSqr = FPocketDirDefaults.SlClustMaxDist * FPocketDirDefaults.SlClustMaxDist;

            float[] densPerSphere = new float[n];
            for (int i = 0; i < n; i++)
            {
                int nb = Mathf.Max(1, preDescs[i].nb_asph);
                densPerSphere[i] = preDescs[i].as_density / nb;
                if (float.IsNaN(densPerSphere[i])) densPerSphere[i] = 0f;
            }

            List<PocketPair> pairs = new List<PocketPair>((n * (n - 1)) / 2);
            for (int i = 0; i < n - 1; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    int cnt = 0;
                    List<int> pi = pockets[i];
                    List<int> pj = pockets[j];
                    for (int a = 0; a < pi.Count; a++)
                    {
                        Vector3 ca = spheres[pi[a]].center;
                        for (int b = 0; b < pj.Count; b++)
                        {
                            Vector3 delta = ca - spheres[pj[b]].center;
                            if (delta.sqrMagnitude < distSqr)
                                cnt++;
                        }
                    }
                    pairs.Add(new PocketPair { pid1 = i, pid2 = j, dist = -cnt });
                }
            }

            pairs.Sort((a, b) => a.dist.CompareTo(b.dist));

            int[] rep = new int[n];
            for (int i = 0; i < n; i++) rep[i] = i;

            for (int idx = 0; idx < pairs.Count; idx++)
            {
                PocketPair p = pairs[idx];
                if (p.dist > -FPocketDirDefaults.SlClustMinNumNeigh)
                    break;

                int r1 = rep[p.pid1];
                int r2 = rep[p.pid2];
                if (r1 == r2)
                    continue;

                if (densPerSphere[r1] < 0.1f && densPerSphere[r2] < 0.1f)
                {
                    for (int k = 0; k < n; k++)
                    {
                        if (rep[k] == r2)
                            rep[k] = r1;
                    }
                    pockets[r1].AddRange(pockets[r2]);
                    pockets[r2].Clear();
                }
            }

            Dictionary<int, List<int>> merged = new Dictionary<int, List<int>>();
            for (int i = 0; i < n; i++)
            {
                int r = rep[i];
                if (!merged.TryGetValue(r, out var lst))
                {
                    lst = new List<int>();
                    merged[r] = lst;
                }
                lst.AddRange(pockets[i]);
            }

            return merged.Values.Where(l => l.Count > 0).ToList();
        }

        private Vector3 ComputeClusterCenter(List<FPocketAlphaSphere> spheres, List<int> clusterIndices)
        {
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < clusterIndices.Count; i++)
                sum += spheres[clusterIndices[i]].center;
            return sum / Mathf.Max(1, clusterIndices.Count);
        }

        private int CountUniqueContactAtoms(List<int> cluster, List<FPocketAlphaSphere> spheres)
        {
            HashSet<int> set = new HashSet<int>();
            for (int i = 0; i < cluster.Count; i++)
            {
                int[] parents = spheres[cluster[i]].parent_atoms;
                if (parents == null) continue;
                for (int j = 0; j < parents.Length; j++)
                {
                    int a = parents[j];
                    if (a >= 0) set.Add(a);
                }
            }
            return set.Count;
        }

        private FPocketDesc ComputeDescriptorsFPocketDir(List<FPocketAlphaSphere> spheres, List<int> clusterIndices, bool doVolume)
        {
            FPocketDesc d = new FPocketDesc();
            int nvert = clusterIndices.Count;
            d.nb_asph = nvert;
            if (nvert <= 0)
                return d;

            float meanRadius = 0f;
            float masphSacc = 0f;
            float asDensitySum = 0f;
            float asMaxDst = -1f;

            int nApol = 0;
            float mlhdSum = 0f;

            Dictionary<int, int> residueAaIndex = new Dictionary<int, int>();

            for (int i = 0; i < nvert; i++)
            {
                FPocketAlphaSphere si = spheres[clusterIndices[i]];
                meanRadius += si.radius;

                if (si.parent_atoms != null)
                {
                    Vector3 bary = Vector3.zero;
                    int bc = 0;
                    for (int p = 0; p < si.parent_atoms.Length; p++)
                    {
                        int atomIdx = si.parent_atoms[p];
                        if (atomIdx >= 0 && atomIdx < atoms.Count)
                        {
                            bary += atoms[atomIdx].pos;
                            bc++;
                            int resId = atoms[atomIdx].res_id;
                            if (!residueAaIndex.ContainsKey(resId))
                                residueAaIndex[resId] = atoms[atomIdx].aaIndex;
                        }
                    }
                    if (bc > 0)
                    {
                        bary /= bc;
                        masphSacc += Vector3.Distance(si.center, bary) / Mathf.Max(1e-6f, si.radius);
                    }
                }

                if (si.hydrophobicity >= 0.5f)
                    nApol++;
            }

            for (int i = 0; i < nvert; i++)
            {
                FPocketAlphaSphere vi = spheres[clusterIndices[i]];
                for (int j = i + 1; j < nvert; j++)
                {
                    FPocketAlphaSphere vj = spheres[clusterIndices[j]];
                    float dst = Vector3.Distance(vi.center, vj.center);
                    if (dst > asMaxDst) asMaxDst = dst;
                    asDensitySum += dst;
                }
            }

            if (nApol > 0)
            {
                for (int i = 0; i < nvert; i++)
                {
                    FPocketAlphaSphere vi = spheres[clusterIndices[i]];
                    if (vi.hydrophobicity < 0.5f)
                        continue;

                    int napol = 0;
                    for (int j = 0; j < nvert; j++)
                    {
                        if (j == i) continue;
                        FPocketAlphaSphere vj = spheres[clusterIndices[j]];
                        if (vj.hydrophobicity < 0.5f) continue;

                        float overlap = Vector3.Distance(vi.center, vj.center) - (vi.radius + vj.radius);
                        if (overlap <= 0f)
                            napol++;
                    }
                    mlhdSum += napol;
                }
                d.mean_loc_hyd_dens = mlhdSum / nApol;
            }
            else
            {
                d.mean_loc_hyd_dens = 0f;
            }

            d.apolar_asphere_prop = (float)nApol / nvert;
            d.mean_asph_ray = meanRadius / nvert;
            d.masph_sacc = masphSacc / nvert;
            d.as_max_dst = asMaxDst < 0f ? 0f : asMaxDst;

            if (nvert >= 2)
                d.as_density = asDensitySum / ((nvert * nvert - nvert) * 0.5f);
            else
                d.as_density = 0f;

            foreach (var kvp in residueAaIndex)
            {
                int aa = kvp.Value;
                if (aa < 0) continue;
                if (AAPropsByIndex.TryGetValue(aa, out AAProps props))
                {
                    d.hydrophobicity_score += props.hydrophobicity;
                    d.polarity_score += props.polarity;
                }
            }

            int nbResIds = residueAaIndex.Count;
            if (nbResIds > 0)
                d.hydrophobicity_score /= nbResIds;

            if (doVolume)
                d.volume = GetVertsVolumePtr(spheres, clusterIndices, FPocketDirDefaults.McIter, FPocketDirDefaults.VolumeCorrect);

            return d;
        }

        private void NormalizeDescriptorsFPocketDir(FPocketDesc[] descs)
        {
            if (descs == null || descs.Length == 0)
                return;

            if (descs.Length == 1)
            {
                FPocketDesc d = descs[0];
                d.as_max_dst_norm = 0f;
                d.as_density_norm = 0f;
                d.polarity_score_norm = 0f;
                d.mean_loc_hyd_dens_norm = 0f;
                d.nas_norm = 0f;
                d.prop_asapol_norm = 0f;
                descs[0] = d;
                return;
            }

            float asMaxDstM = descs[0].as_max_dst;
            float asMaxDstm = descs[0].as_max_dst;
            float densityM = descs[0].as_density;
            float densitym = descs[0].as_density;
            int polarityM = descs[0].polarity_score;
            int polaritym = descs[0].polarity_score;
            float mlhdM = descs[0].mean_loc_hyd_dens;
            float mlhdm = descs[0].mean_loc_hyd_dens;
            int nasM = descs[0].nb_asph;
            int nasm = descs[0].nb_asph;
            float apolPropM = descs[0].apolar_asphere_prop;
            float apolPropm = descs[0].apolar_asphere_prop;

            for (int i = 1; i < descs.Length; i++)
            {
                FPocketDesc d = descs[i];
                if (d.as_max_dst > asMaxDstM) asMaxDstM = d.as_max_dst;
                if (d.as_max_dst < asMaxDstm) asMaxDstm = d.as_max_dst;
                if (d.as_density > densityM) densityM = d.as_density;
                if (d.as_density < densitym) densitym = d.as_density;
                if (d.polarity_score > polarityM) polarityM = d.polarity_score;
                if (d.polarity_score < polaritym) polaritym = d.polarity_score;
                if (d.mean_loc_hyd_dens > mlhdM) mlhdM = d.mean_loc_hyd_dens;
                if (d.mean_loc_hyd_dens < mlhdm) mlhdm = d.mean_loc_hyd_dens;
                if (d.nb_asph > nasM) nasM = d.nb_asph;
                if (d.nb_asph < nasm) nasm = d.nb_asph;
                if (d.apolar_asphere_prop > apolPropM) apolPropM = d.apolar_asphere_prop;
                if (d.apolar_asphere_prop < apolPropm) apolPropm = d.apolar_asphere_prop;
            }

            for (int i = 0; i < descs.Length; i++)
            {
                FPocketDesc d = descs[i];
                if (Mathf.Abs(asMaxDstM - asMaxDstm) > 1e-6f)
                    d.as_max_dst_norm = (d.as_max_dst - asMaxDstm) / (asMaxDstM - asMaxDstm);
                if (Mathf.Abs(densityM - densitym) > 1e-6f)
                    d.as_density_norm = (d.as_density - densitym) / (densityM - densitym);
                if (polarityM - polaritym != 0)
                    d.polarity_score_norm = (float)(d.polarity_score - polaritym) / (polarityM - polaritym);
                if (Mathf.Abs(mlhdM - mlhdm) > 1e-6f)
                    d.mean_loc_hyd_dens_norm = (d.mean_loc_hyd_dens - mlhdm) / (mlhdM - mlhdm);
                if (nasM - nasm != 0)
                    d.nas_norm = (float)(d.nb_asph - nasm) / (nasM - nasm);
                if (Mathf.Abs(apolPropM - apolPropm) > 1e-6f)
                    d.prop_asapol_norm = (d.apolar_asphere_prop - apolPropm) / (apolPropM - apolPropm);
                descs[i] = d;
            }
        }

        private float GetVertsVolumePtr(List<FPocketAlphaSphere> spheres, List<int> clusterIndices, int niter, float correct)
        {
            int nvert = clusterIndices.Count;
            if (nvert <= 0 || niter <= 0)
                return 0f;

            float xmin = 0f, xmax = 0f, ymin = 0f, ymax = 0f, zmin = 0f, zmax = 0f;
            for (int i = 0; i < nvert; i++)
            {
                FPocketAlphaSphere v = spheres[clusterIndices[i]];
                float r = v.radius;
                if (i == 0)
                {
                    xmin = v.center.x - r + correct;
                    xmax = v.center.x + r + correct;
                    ymin = v.center.y - r + correct;
                    ymax = v.center.y + r + correct;
                    zmin = v.center.z - r + correct;
                    zmax = v.center.z + r + correct;
                }
                else
                {
                    xmin = Mathf.Min(xmin, v.center.x - r + correct);
                    xmax = Mathf.Max(xmax, v.center.x + r + correct);
                    ymin = Mathf.Min(ymin, v.center.y - r + correct);
                    ymax = Mathf.Max(ymax, v.center.y + r + correct);
                    zmin = Mathf.Min(zmin, v.center.z - r + correct);
                    zmax = Mathf.Max(zmax, v.center.z + r + correct);
                }
            }

            float vbox = (xmax - xmin) * (ymax - ymin) * (zmax - zmin);
            if (vbox <= 0f)
                return 0f;

            System.Random rng = new System.Random();
            int nbIn = 0;
            for (int i = 0; i < niter; i++)
            {
                float xr = (float)(xmin + rng.NextDouble() * (xmax - xmin));
                float yr = (float)(ymin + rng.NextDouble() * (ymax - ymin));
                float zr = (float)(zmin + rng.NextDouble() * (zmax - zmin));

                for (int j = 0; j < nvert; j++)
                {
                    FPocketAlphaSphere v = spheres[clusterIndices[j]];
                    float rr = v.radius + correct;
                    float dx = v.center.x - xr;
                    float dy = v.center.y - yr;
                    float dz = v.center.z - zr;
                    if (rr * rr > dx * dx + dy * dy + dz * dz)
                    {
                        nbIn++;
                        break;
                    }
                }
            }

            return ((float)nbIn / niter) * vbox;
        }

        private void BuildNeighborBuffers(List<FPocketAtom> sourceAtoms, out int[] neighborCounts, out int[] neighborIndices, float cutoff, int maxNeighbors)
        {
            neighborCounts = new int[sourceAtoms.Count];
            neighborIndices = Enumerable.Repeat(-1, sourceAtoms.Count * maxNeighbors).ToArray();

            Dictionary<Vector3Int, List<int>> spatialHash = BuildAtomSpatialHash(sourceAtoms, cutoff);
            for (int atomIdx = 0; atomIdx < sourceAtoms.Count; atomIdx++)
            {
                List<int> nearby = GetNearbyAtomIndices(atomIdx, sourceAtoms, spatialHash, cutoff);
                if (nearby.Count > maxNeighbors)
                    nearby = nearby.OrderBy(i => (sourceAtoms[i].pos - sourceAtoms[atomIdx].pos).sqrMagnitude).Take(maxNeighbors).ToList();

                neighborCounts[atomIdx] = nearby.Count;
                for (int j = 0; j < nearby.Count; j++)
                    neighborIndices[atomIdx * maxNeighbors + j] = nearby[j];
            }
        }

        private static readonly Dictionary<int, AAProps> AAPropsByIndex = new Dictionary<int, AAProps>
        {
            { 0, new AAProps { volume = 2f, hydrophobicity = 41f, charge = 0, polarity = 0, func_grp = 2 } },
            { 14, new AAProps { volume = 7f, hydrophobicity = -14f, charge = 1, polarity = 1, func_grp = 5 } },
            { 11, new AAProps { volume = 3f, hydrophobicity = -28f, charge = 0, polarity = 1, func_grp = 3 } },
            { 2, new AAProps { volume = 3f, hydrophobicity = -55f, charge = -1, polarity = 1, func_grp = 3 } },
            { 1, new AAProps { volume = 3f, hydrophobicity = 49f, charge = 0, polarity = 0, func_grp = 6 } },
            { 13, new AAProps { volume = 4f, hydrophobicity = -10f, charge = 0, polarity = 1, func_grp = 3 } },
            { 3, new AAProps { volume = 4f, hydrophobicity = -31f, charge = -1, polarity = 1, func_grp = 3 } },
            { 5, new AAProps { volume = 1f, hydrophobicity = 0f, charge = 0, polarity = 0, func_grp = 2 } },
            { 6, new AAProps { volume = 4f, hydrophobicity = 8f, charge = 1, polarity = 1, func_grp = 1 } },
            { 7, new AAProps { volume = 5f, hydrophobicity = 99f, charge = 0, polarity = 0, func_grp = 2 } },
            { 9, new AAProps { volume = 5f, hydrophobicity = 97f, charge = 0, polarity = 0, func_grp = 2 } },
            { 8, new AAProps { volume = 6f, hydrophobicity = -23f, charge = 1, polarity = 1, func_grp = 5 } },
            { 10, new AAProps { volume = 5f, hydrophobicity = 74f, charge = 0, polarity = 0, func_grp = 5 } },
            { 4, new AAProps { volume = 6f, hydrophobicity = 100f, charge = 0, polarity = 0, func_grp = 1 } },
            { 12, new AAProps { volume = 3f, hydrophobicity = -46f, charge = 0, polarity = 0, func_grp = 2 } },
            { 15, new AAProps { volume = 2f, hydrophobicity = -5f, charge = 0, polarity = 1, func_grp = 4 } },
            { 16, new AAProps { volume = 3f, hydrophobicity = 13f, charge = 0, polarity = 1, func_grp = 4 } },
            { 18, new AAProps { volume = 8f, hydrophobicity = 97f, charge = 0, polarity = 1, func_grp = 1 } },
            { 19, new AAProps { volume = 7f, hydrophobicity = 63f, charge = 0, polarity = 1, func_grp = 1 } },
            { 17, new AAProps { volume = 4f, hydrophobicity = 76f, charge = 0, polarity = 0, func_grp = 2 } },
        };

        private struct AAProps
        {
            public float volume;
            public float hydrophobicity;
            public int charge;
            public int polarity;
            public int func_grp;
        }

        private int GetAaIndex(string resName)
        {
            if (string.IsNullOrWhiteSpace(resName))
                return -1;

            string n = resName.Trim().ToUpperInvariant();
            if (n.Length < 3)
                return -1;

            char l1 = n[0];
            char l2 = n[1];
            char l3 = n[2];

            switch (l1)
            {
                case 'A':
                    if (l2 == 'L') return 0;
                    if (l2 == 'R') return 14;
                    if (l2 == 'S' && l3 == 'P') return 2;
                    return 11;
                case 'C': return 1;
                case 'G':
                    if (l3 == 'U') return 3;
                    if (l3 == 'Y') return 5;
                    return 13;
                case 'H': return 6;
                case 'I': return 7;
                case 'L':
                    if (l2 == 'Y') return 8;
                    return 9;
                case 'M': return 10;
                case 'P':
                    if (l2 == 'H') return 4;
                    return 12;
                case 'S': return 15;
                case 'T':
                    if (l2 == 'H') return 16;
                    if (l2 == 'R') return 18;
                    return 19;
                case 'V': return 17;
            }

            return -1;
        }

        private float GetElectronegativity(string atomSymbol)
        {
            if (string.IsNullOrEmpty(atomSymbol))
                return 10f;

            switch (atomSymbol.ToUpperInvariant())
            {
                case "H": return 2.20f;
                case "C": return 2.55f;
                case "N": return 3.04f;
                case "O": return 3.44f;
                case "F": return 3.98f;
                case "P": return 2.19f;
                case "S": return 2.58f;
                case "CL": return 3.16f;
                case "BR": return 2.96f;
                case "I": return 2.66f;
                default: return 10f;
            }
        }

        private int CountNearbyAtoms(Vector3 center, float cutoff, List<FPocketAtom> sourceAtoms)
        {
            float cutoffSqr = cutoff * cutoff;
            int count = 0;
            for (int i = 0; i < sourceAtoms.Count; i++)
            {
                if ((sourceAtoms[i].pos - center).sqrMagnitude <= cutoffSqr)
                    count++;
            }

            return count;
        }

        private Dictionary<Vector3Int, List<int>> BuildAtomSpatialHash(List<FPocketAtom> sourceAtoms, float cellSize)
        {
            Dictionary<Vector3Int, List<int>> spatialHash = new Dictionary<Vector3Int, List<int>>(sourceAtoms.Count);
            for (int atomIdx = 0; atomIdx < sourceAtoms.Count; atomIdx++)
            {
                Vector3Int cell = GetSpatialCell(sourceAtoms[atomIdx].pos, cellSize);
                if (!spatialHash.TryGetValue(cell, out List<int> bucket))
                {
                    bucket = new List<int>();
                    spatialHash[cell] = bucket;
                }

                bucket.Add(atomIdx);
            }

            return spatialHash;
        }

        private List<int> GetNearbyAtomIndices(int atomIdx, List<FPocketAtom> sourceAtoms, Dictionary<Vector3Int, List<int>> spatialHash, float cutoff)
        {
            List<int> result = new List<int>();
            Vector3 center = sourceAtoms[atomIdx].pos;
            Vector3Int cell = GetSpatialCell(center, cutoff);
            float cutoffSqr = cutoff * cutoff;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        Vector3Int neighborCell = new Vector3Int(cell.x + dx, cell.y + dy, cell.z + dz);
                        if (!spatialHash.TryGetValue(neighborCell, out List<int> bucket))
                            continue;

                        for (int i = 0; i < bucket.Count; i++)
                        {
                            int candidate = bucket[i];
                            if (candidate == atomIdx)
                                continue;

                            Vector3 delta = sourceAtoms[candidate].pos - center;
                            if (delta.sqrMagnitude <= cutoffSqr)
                                result.Add(candidate);
                        }
                    }
                }
            }

            result.Sort();
            return result;
        }
        private List<List<FPocketAlphaSphere>> DBSCANCluster(List<FPocketAlphaSphere> spheres)
        {
            if (!useSpatialHashDbscan)
                return DBSCANClusterLegacy(spheres);

            List<List<FPocketAlphaSphere>> clusters = new List<List<FPocketAlphaSphere>>();
            int sphereCount = spheres.Count;
            if (sphereCount == 0)
                return clusters;

            bool[] visited = new bool[sphereCount];
            bool[] noise = new bool[sphereCount];
            List<int>[] neighborCache = new List<int>[sphereCount];
            float eps = FPocketConstants.DBSCAN_EPS;
            float epsSqr = eps * eps;
            var spatialIndex = BuildSpatialHash(spheres, eps);

            for (int i = 0; i < sphereCount; i++)
            {
                if (visited[i]) continue;

                List<int> neighbors = GetNeighbors(i, spheres, spatialIndex, neighborCache, epsSqr);
                if (neighbors.Count < FPocketConstants.DBSCAN_MIN_POINTS)
                {
                    noise[i] = true;
                    visited[i] = true;
                    FPocketAlphaSphere s = spheres[i];
                    s.visited = 2;
                    spheres[i] = s;
                    continue;
                }

                List<FPocketAlphaSphere> cluster = new List<FPocketAlphaSphere>();
                cluster.Add(spheres[i]);
                visited[i] = true;
                FPocketAlphaSphere core = spheres[i];
                core.visited = 1;
                spheres[i] = core;

                Queue<int> queue = new Queue<int>(neighbors);
                bool[] enqueued = new bool[sphereCount];
                foreach (int neighbor in neighbors)
                    enqueued[neighbor] = true;
                while (queue.Count > 0)
                {
                    int j = queue.Dequeue();
                    enqueued[j] = false;
                    if (visited[j]) continue;

                    visited[j] = true;
                    FPocketAlphaSphere js = spheres[j];
                    js.visited = 1;
                    spheres[j] = js;

                    List<int> jNeighbors = GetNeighbors(j, spheres, spatialIndex, neighborCache, epsSqr);
                    if (jNeighbors.Count >= FPocketConstants.DBSCAN_MIN_POINTS)
                    {
                        foreach (int n in jNeighbors)
                        {
                            if (!visited[n] && !enqueued[n])
                            {
                                queue.Enqueue(n);
                                enqueued[n] = true;
                            }
                        }
                    }

                    cluster.Add(spheres[j]);
                }

                clusters.Add(cluster);
            }

            
            List<List<FPocketAlphaSphere>> prunedClusters = new List<List<FPocketAlphaSphere>>();
            foreach (var cluster in clusters)
            {
                
                if (cluster.Count >= 10)
                {
                    prunedClusters.Add(cluster);
                }
            }
            return prunedClusters;
        }

        private List<List<FPocketAlphaSphere>> DBSCANClusterLegacy(List<FPocketAlphaSphere> spheres)
        {
            List<List<FPocketAlphaSphere>> clusters = new List<List<FPocketAlphaSphere>>();
            HashSet<int> visited = new HashSet<int>();

            for (int i = 0; i < spheres.Count; i++)
            {
                if (visited.Contains(i)) continue;

                List<int> neighbors = FindNeighbors(spheres, i);
                if (neighbors.Count < FPocketConstants.DBSCAN_MIN_POINTS)
                {
                    visited.Add(i);
                    FPocketAlphaSphere s = spheres[i];
                    s.visited = 2;
                    spheres[i] = s;
                    continue;
                }

                List<FPocketAlphaSphere> cluster = new List<FPocketAlphaSphere>();
                cluster.Add(spheres[i]);
                visited.Add(i);
                FPocketAlphaSphere core = spheres[i];
                core.visited = 1;
                spheres[i] = core;

                Queue<int> queue = new Queue<int>(neighbors);
                while (queue.Count > 0)
                {
                    int j = queue.Dequeue();
                    if (visited.Contains(j)) continue;

                    visited.Add(j);
                    FPocketAlphaSphere js = spheres[j];
                    js.visited = 1;
                    spheres[j] = js;

                    List<int> jNeighbors = FindNeighbors(spheres, j);
                    if (jNeighbors.Count >= FPocketConstants.DBSCAN_MIN_POINTS)
                    {
                        foreach (int n in jNeighbors)
                        {
                            if (!visited.Contains(n) && !queue.Contains(n))
                                queue.Enqueue(n);
                        }
                    }

                    cluster.Add(spheres[j]);
                }

                clusters.Add(cluster);
            }

            List<List<FPocketAlphaSphere>> prunedClusters = new List<List<FPocketAlphaSphere>>();
            foreach (var cluster in clusters)
            {
                if (cluster.Count >= 10)
                    prunedClusters.Add(cluster);
            }
            return prunedClusters;
        }

        private List<List<FPocketAlphaSphere>> SingleLinkageCluster(List<FPocketAlphaSphere> spheres)
        {
            List<List<FPocketAlphaSphere>> clusters = new List<List<FPocketAlphaSphere>>();
            if (spheres == null || spheres.Count == 0)
                return clusters;

            bool[] visited = new bool[spheres.Count];
            float thresholdSqr = singleLinkageThreshold * singleLinkageThreshold;

            for (int i = 0; i < spheres.Count; i++)
            {
                if (visited[i])
                    continue;

                List<FPocketAlphaSphere> cluster = new List<FPocketAlphaSphere>();
                Queue<int> queue = new Queue<int>();
                queue.Enqueue(i);
                visited[i] = true;

                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    cluster.Add(spheres[current]);

                    for (int candidate = 0; candidate < spheres.Count; candidate++)
                    {
                        if (visited[candidate])
                            continue;

                        Vector3 delta = spheres[current].center - spheres[candidate].center;
                        if (delta.sqrMagnitude > thresholdSqr)
                            continue;

                        visited[candidate] = true;
                        queue.Enqueue(candidate);
                    }
                }

                if (cluster.Count >= 10)
                    clusters.Add(cluster);
            }

            return clusters;
        }

        private Dictionary<Vector3Int, List<int>> BuildSpatialHash(List<FPocketAlphaSphere> spheres, float cellSize)
        {
            Dictionary<Vector3Int, List<int>> spatialIndex = new Dictionary<Vector3Int, List<int>>(spheres.Count);
            for (int i = 0; i < spheres.Count; i++)
            {
                Vector3Int cell = GetSpatialCell(spheres[i].center, cellSize);
                if (!spatialIndex.TryGetValue(cell, out var bucket))
                {
                    bucket = new List<int>();
                    spatialIndex[cell] = bucket;
                }
                bucket.Add(i);
            }
            return spatialIndex;
        }

        private Vector3Int GetSpatialCell(Vector3 position, float cellSize)
        {
            return new Vector3Int(
                Mathf.FloorToInt(position.x / cellSize),
                Mathf.FloorToInt(position.y / cellSize),
                Mathf.FloorToInt(position.z / cellSize)
            );
        }

        private List<int> GetNeighbors(
            int index,
            List<FPocketAlphaSphere> spheres,
            Dictionary<Vector3Int, List<int>> spatialIndex,
            List<int>[] neighborCache,
            float epsSqr)
        {
            if (neighborCache[index] != null)
                return neighborCache[index];

            List<int> neighbors = new List<int>();
            Vector3 center = spheres[index].center;
            Vector3Int cell = GetSpatialCell(center, FPocketConstants.DBSCAN_EPS);

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        Vector3Int neighborCell = new Vector3Int(cell.x + dx, cell.y + dy, cell.z + dz);
                        if (!spatialIndex.TryGetValue(neighborCell, out var bucket))
                            continue;

                        for (int i = 0; i < bucket.Count; i++)
                        {
                            int candidate = bucket[i];
                            if (candidate == index)
                                continue;

                            Vector3 delta = center - spheres[candidate].center;
                            if (delta.sqrMagnitude < epsSqr)
                                neighbors.Add(candidate);
                        }
                    }
                }
            }

            neighborCache[index] = neighbors;
            return neighbors;
        }
        private List<int> FindNeighbors(List<FPocketAlphaSphere> spheres, int index)
        {
            List<int> neighbors = new List<int>();
            Vector3 center = spheres[index].center;

            for (int i = 0; i < spheres.Count; i++)
            {
                if (i == index) continue;
                if (Vector3.Distance(center, spheres[i].center) < FPocketConstants.DBSCAN_EPS)
                    neighbors.Add(i);
            }

            return neighbors;
        }
        private Vector3 ComputeClusterCenter(List<FPocketAlphaSphere> cluster)
        {
            Vector3 sum = Vector3.zero;
            foreach (var sphere in cluster)
            {
                sum += sphere.center;
            }
            return sum / cluster.Count;
        }
        private float ComputeClusterVolume(List<FPocketAlphaSphere> cluster)
        {
            float totalVolume = 0f;
            foreach (var sphere in cluster)
            {
                
                totalVolume += (4f / 3f) * Mathf.PI * Mathf.Pow(sphere.radius, 3);
            }
            return totalVolume;
        }
        private List<FPocketResult> ComputePocketFeatures(List<List<FPocketAlphaSphere>> clusters)
        {
            List<FPocketResult> pockets = new List<FPocketResult>();
            int pocketId = 0;

            
            List<float> allHydroScores = new List<float>();
            List<float> allDepthScores = new List<float>();
            List<float> allDensities = new List<float>();

            
            List<Tuple<FPocketResult, float, float, float>> pocketFeatures = new List<Tuple<FPocketResult, float, float, float>>();
            foreach (var cluster in clusters)
            {
                if (cluster.Count == 0) continue;

                
                Vector3 center = ComputeClusterCenter(cluster);
                
                float volume = ComputeClusterVolume(cluster);
                int nbAlphaSpheres = cluster.Count;
                float density = nbAlphaSpheres / (volume + 1e-6f); 

                
                float hydroSum = cluster.Sum(s => s.hydrophobicity);
                float hydrophobicScore = hydroSum / nbAlphaSpheres;
                float polarScore = 1f - hydrophobicScore;

                
                float depthScore = ComputeClusterDepthScore(cluster, atoms);

                
                FPocketResult tempPocket = new FPocketResult
                {
                    id = pocketId++,
                    center = center,
                    volume = volume,
                    nb_alpha_spheres = nbAlphaSpheres,
                    hydrophobic_score = hydrophobicScore,
                    polar_score = polarScore,
                    depth_score = depthScore,
                    density = density
                };

                pocketFeatures.Add(Tuple.Create(tempPocket, hydrophobicScore, depthScore, density));
                allHydroScores.Add(hydrophobicScore);
                allDepthScores.Add(depthScore);
                allDensities.Add(density);
            }

            
            float maxHydro = allHydroScores.Count > 0 ? allHydroScores.Max() : 1f;
            float maxDepth = allDepthScores.Count > 0 ? allDepthScores.Max() : 1f;
            float maxDensity = allDensities.Count > 0 ? allDensities.Max() : 1f;
            float minHydro = allHydroScores.Count > 0 ? allHydroScores.Min() : 0f;
            float minDepth = allDepthScores.Count > 0 ? allDepthScores.Min() : 0f;
            float minDensity = allDensities.Count > 0 ? allDensities.Min() : 0f;

            
            foreach (var feature in pocketFeatures)
            {
                FPocketResult pocket = feature.Item1;
                float rawHydro = feature.Item2;
                float rawDepth = feature.Item3;
                float rawDensity = feature.Item4;

                
                float normHydro = NormalizeValue(rawHydro, minHydro, maxHydro);
                float normDepth = NormalizeValue(rawDepth, minDepth, maxDepth);
                float normDensity = NormalizeValue(rawDensity, minDensity, maxDensity);

                
                float finalScore = (normHydro * 0.5f) + (normDepth * 0.3f) + (normDensity * 0.2f);

                
                pocket.score = finalScore;
                pocket.hydrophobic_score = normHydro;
                pocket.depth_score = normDepth;
                pocket.density = normDensity;

                pockets.Add(pocket);
            }

            return pockets;
        }

        private List<FPocketResult> ComputePocketFeaturesOfficialStyle(List<List<FPocketAlphaSphere>> clusters)
        {
            List<FPocketResult> pockets = new List<FPocketResult>();
            int pocketId = 0;

            foreach (var cluster in clusters)
            {
                if (cluster == null || cluster.Count == 0)
                    continue;

                Vector3 center = ComputeClusterCenter(cluster);
                float volume = ComputeClusterVolume(cluster);
                int nbAlphaSpheres = cluster.Count;
                float density = nbAlphaSpheres / Mathf.Max(volume, 1e-4f);
                float meanHydrophobicity = cluster.Average(s => s.hydrophobicity);
                float apolarRatio = cluster.Count(s => s.hydrophobicity >= 0.5f) / (float)nbAlphaSpheres;
                float meanAlphaRadius = cluster.Average(s => s.radius);
                float depthScore = ComputeClusterDepthScore(cluster, atoms);
                float meanPolarity = cluster.Average(s => s.polarity);
                float maxCenterDistance = ComputeClusterMaxCenterDistance(cluster);
                float hydrophobicDensity = meanHydrophobicity * density;
                float normalizedDensity = Mathf.Clamp01(density / 0.12f);
                float normalizedRadius = Mathf.Clamp01(meanAlphaRadius / FPocketConstants.MAX_ALPHA_SPHERE_RADIUS);
                float normalizedDepth = Mathf.Clamp01(depthScore);
                float normalizedSpan = Mathf.Clamp01(maxCenterDistance / 20.0f);
                float normalizedVolume = Mathf.Clamp01(volume / 400.0f);
                float normalizedHydrophobicDensity = Mathf.Clamp01(hydrophobicDensity / 0.08f);

                float score =
                    Mathf.Clamp01(apolarRatio) * 0.24f +
                    Mathf.Clamp01(meanHydrophobicity) * 0.16f +
                    normalizedDepth * 0.17f +
                    normalizedDensity * 0.14f +
                    normalizedRadius * 0.10f +
                    normalizedSpan * 0.09f +
                    normalizedVolume * 0.05f +
                    normalizedHydrophobicDensity * 0.05f;

                score *= Mathf.Lerp(0.80f, 1.05f, 1.0f - Mathf.Clamp01(meanPolarity));

                pockets.Add(new FPocketResult
                {
                    id = pocketId++,
                    center = center,
                    volume = volume,
                    score = score,
                    hydrophobic_score = meanHydrophobicity,
                    polar_score = meanPolarity,
                    depth_score = depthScore,
                    nb_alpha_spheres = nbAlphaSpheres,
                    nb_atoms = cluster.Sum(s => Mathf.Max(1, s.nb_atoms)),
                    density = density
                });
            }
            if (pockets.Count > 0)
            {
                float minScore = pockets.Min(p => p.score);
                float maxScore = pockets.Max(p => p.score);
                float span = Mathf.Max(maxScore - minScore, 1e-5f);
                for (int i = 0; i < pockets.Count; i++)
                {
                    float normalized = (pockets[i].score - minScore) / span;
                    // Re-expand to match the legacy score dynamic range (historically many strong pockets were >0.5).
                    var r = pockets[i];
                    r.score = Mathf.Clamp01(0.15f + normalized * 0.85f);
                    pockets[i] = r;
                }
            }
            return pockets;
        }
        private float NormalizeValue(float value, float min, float max)
        {
            if (Mathf.Abs(max - min) < 1e-6) return 0f;
            return (value - min) / (max - min);
        }
        private List<FPocketResult> RemoveOverlappingPockets(List<FPocketResult> pockets, float iouThreshold)
        {
            List<FPocketResult> keptPockets = new List<FPocketResult>();
            HashSet<int> removedIds = new HashSet<int>();

            
            foreach (var pocket in pockets)
            {
                if (removedIds.Contains(pocket.id)) continue;

                bool keep = true;
                foreach (var kept in keptPockets)
                {
                    
                    float centerDist = Vector3.Distance(pocket.center, kept.center);
                    float avgRadius = (Mathf.Pow(pocket.volume * 3 / (4 * Mathf.PI), 1 / 3f) +
                                       Mathf.Pow(kept.volume * 3 / (4 * Mathf.PI), 1 / 3f)) / 2;

                    
                    if (centerDist < avgRadius * 0.7f)
                    {
                        keep = false;
                        removedIds.Add(pocket.id);
                        break;
                    }
                }

                if (keep)
                {
                    keptPockets.Add(pocket);
                }
            }

            return keptPockets;
        }
        private float ComputeClusterDepthScore(List<FPocketAlphaSphere> cluster, List<FPocketAtom> atoms)
        {
            float totalDepth = 0f;
            int validSpheres = 0;

            foreach (var sphere in cluster)
            {
                
                float minDist = float.MaxValue;
                foreach (var atom in atoms)
                {
                    float distToAtom = Vector3.Distance(sphere.center, atom.pos) - atom.vdw_radius;
                    if (distToAtom < minDist)
                    {
                        minDist = distToAtom;
                    }
                }

                if (minDist > 0)
                {
                    totalDepth += minDist;
                    validSpheres++;
                }
            }

            if (validSpheres == 0) return 0f;

            
            float avgDepth = totalDepth / validSpheres;
            float maxRadius = cluster.Max(s => s.radius);
            return avgDepth / maxRadius;
        }

        private float ComputeClusterMaxCenterDistance(List<FPocketAlphaSphere> cluster)
        {
            float maxDistance = 0f;
            for (int i = 0; i < cluster.Count; i++)
            {
                for (int j = i + 1; j < cluster.Count; j++)
                {
                    float distance = Vector3.Distance(cluster[i].center, cluster[j].center);
                    if (distance > maxDistance)
                        maxDistance = distance;
                }
            }

            return maxDistance;
        }

        #endregion

        #region Shared helpers
        private List<FPocketAtom> LoadAtomsFromPDBQT(string filePath)
        {
            List<FPocketAtom> atoms = new List<FPocketAtom>();
            if (!File.Exists(filePath))
            {
                Debug.LogError($"Pocket input file was not found: {filePath}");
                return atoms;
            }

            string[] lines = File.ReadAllLines(filePath);
            int atomId = 0;

            foreach (string line in lines)
            {
                if (line.StartsWith("ATOM") || line.StartsWith("HETATM"))
                {
                    try
                    {
                        
                        float x = float.Parse(line.Substring(30, 8).Trim());
                        float y = float.Parse(line.Substring(38, 8).Trim());
                        float z = float.Parse(line.Substring(46, 8).Trim());
                        string atomNameRaw = line.Length >= 16 ? line.Substring(12, 4).Trim().ToUpperInvariant() : line.Substring(12).Trim().ToUpperInvariant();
                        string atomSymbol = ExtractAtomSymbol(atomNameRaw);
                        string resName = line.Length >= 20 ? line.Substring(17, 3).Trim().ToUpperInvariant() : "";
                        int aaIndex = GetAaIndex(resName);
                        int resId = 0;
                        if (line.Length >= 26)
                            int.TryParse(line.Substring(22, 4).Trim(), out resId);

                        
                        float vdwRadius = FPocketConstants.VdwRadii.ContainsKey(atomSymbol)
                            ? FPocketConstants.VdwRadii[atomSymbol]
                            : FPocketConstants.VdwRadii["OTHER"];

                        
                        float hydro = FPocketConstants.HydrophobicWeights.ContainsKey(atomSymbol)
                            ? FPocketConstants.HydrophobicWeights[atomSymbol]
                            : FPocketConstants.HydrophobicWeights["OTHER"];

                        atoms.Add(new FPocketAtom
                        {
                            id = atomId++,
                            pos = new Vector3(x, y, z),
                            name = atomSymbol,
                            vdw_radius = vdwRadius,
                            hydrophobicity = hydro,
                            res_id = resId,
                            aaIndex = aaIndex,
                            electroneg = GetElectronegativity(atomSymbol)
                        });
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"Skipped a malformed ATOM/HETATM record while reading {Path.GetFileName(filePath)}: {e.Message}");
                    }
                }
            }

            return atoms;
        }
        private string ExtractAtomSymbol(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return "OTHER";
            if (rawName.StartsWith("CL") || rawName.StartsWith("BR") || rawName.StartsWith("I"))
                return rawName.Substring(0, 2).ToUpper();
            return rawName.Substring(0, 1).ToUpper();
        }
        private float CalculatePocketDepth(Vector3 center)
        {
            float minDist = float.MaxValue;
            foreach (var atom in atoms)
            {
                float dist = Vector3.Distance(center, atom.pos) - atom.vdw_radius;
                if (dist < minDist) minDist = dist;
            }
            return Mathf.Clamp01(minDist / 10f);
        }
        private void PrintPocketResults(List<FPocketResult> pockets, bool flipZ = false)
        {
            var validPockets = pockets.OrderByDescending(_ => _.score).ToList();

            Debug.Log($"Pocket detection finished with {validPockets.Count} ranked pockets.");

            foreach (var p in validPockets)
            {
                Vector3 c = p.center;
                if (flipZ) c.z = -c.z;
                Debug.Log($"Pocket {p.id}: score={p.score:F3}, volume={p.volume:F2}, alphaSpheres={p.nb_alpha_spheres}, atoms={p.nb_atoms}, center={c}");
                Debug.Log($"  hydrophobic={p.hydrophobic_score:F3}, polar={p.polar_score:F3}, depth={p.depth_score:F3}, density={p.density:F3}");
            }
        }
        #endregion

        #region GPU buffer helpers
        private void SetShaderConstants(ComputeShader cs, int atomCount, int generatedSphereCount)
        {
            cs.SetFloat("PROBE_RADIUS", FPocketConstants.PROBE_RADIUS);
            cs.SetFloat("MIN_ALPHA_SPHERE_RADIUS", FPocketConstants.MIN_ALPHA_SPHERE_RADIUS);
            cs.SetFloat("MAX_ALPHA_SPHERE_RADIUS", FPocketConstants.MAX_ALPHA_SPHERE_RADIUS);
            cs.SetFloat("SPHERE_ATOM_EPS", FPocketConstants.SPHERE_ATOM_EPS);
            cs.SetInt("DBSCAN_MIN_POINTS", FPocketConstants.DBSCAN_MIN_POINTS);
            cs.SetFloat("DBSCAN_EPS", FPocketConstants.DBSCAN_EPS);
            cs.SetFloat("MIN_POCKET_VOLUME", FPocketConstants.MIN_POCKET_VOLUME);
            cs.SetInt("atomCount", atomCount); 
            cs.SetInt("generatedSphereCount", generatedSphereCount);
            cs.SetInt("maxAlphaSpheres", FPocketConstants.MAX_ALPHA_SPHERES);
            cs.SetInt("maxPockets", FPocketConstants.MAX_POCKETS);
            
            cs.SetInt("THREAD_GROUP_SIZE_X", FPocketConstants.THREAD_GROUP_SIZE_X);
            cs.SetInt("THREAD_GROUP_SIZE_Y", FPocketConstants.THREAD_GROUP_SIZE_Y);
            cs.SetInt("MIN_APOL_NEIGH", FPocketDirDefaults.MinApolNeigh);
            cs.SetInt("MAX_NEIGHBORS", FPocketDirDefaults.MaxNeighbors);
        }

        private void SetShaderConstantsFPocketDir(ComputeShader cs, int atomCount, int generatedSphereCount)
        {
            cs.SetFloat("PROBE_RADIUS", 0f);
            cs.SetFloat("MIN_ALPHA_SPHERE_RADIUS", FPocketDirDefaults.MinAsphereRadius);
            cs.SetFloat("MAX_ALPHA_SPHERE_RADIUS", FPocketDirDefaults.MaxAsphereRadius);
            cs.SetFloat("SPHERE_ATOM_EPS", FPocketConstants.SPHERE_ATOM_EPS);
            cs.SetInt("DBSCAN_MIN_POINTS", 0);
            cs.SetFloat("DBSCAN_EPS", 0f);
            cs.SetFloat("MIN_POCKET_VOLUME", 0f);
            cs.SetInt("atomCount", atomCount);
            cs.SetInt("generatedSphereCount", generatedSphereCount);
            cs.SetInt("maxAlphaSpheres", FPocketConstants.MAX_ALPHA_SPHERES);
            cs.SetInt("maxPockets", FPocketConstants.MAX_POCKETS);
            cs.SetInt("THREAD_GROUP_SIZE_X", FPocketConstants.THREAD_GROUP_SIZE_X);
            cs.SetInt("THREAD_GROUP_SIZE_Y", FPocketConstants.THREAD_GROUP_SIZE_Y);
            cs.SetInt("MIN_APOL_NEIGH", FPocketDirDefaults.MinApolNeigh);
            cs.SetInt("MAX_NEIGHBORS", FPocketDirDefaults.MaxNeighbors);
        }
        private ComputeBuffer InitAtomBuffer(List<FPocketAtom> atoms)
        {
            int stride = Marshal.SizeOf(typeof(FPocketAtomCS));
            ComputeBuffer buffer = new ComputeBuffer(atoms.Count, stride, ComputeBufferType.Default);
            FPocketAtomCS[] atomCS = atoms.Select(a => new FPocketAtomCS
            {
                id = a.id,
                pos = a.pos,
                vdw_radius = a.vdw_radius,
                hydrophobicity = a.hydrophobicity,
                electroneg = a.electroneg
            }).ToArray();
            buffer.SetData(atomCS);
            return buffer;
        }
        private ComputeBuffer InitAlphaSphereBuffer()
        {
            int stride = Marshal.SizeOf(typeof(FPocketAlphaSphereCS));
            ComputeBuffer buffer = new ComputeBuffer(FPocketConstants.MAX_ALPHA_SPHERES, stride, ComputeBufferType.Default);
            FPocketAlphaSphereCS[] empty = new FPocketAlphaSphereCS[FPocketConstants.MAX_ALPHA_SPHERES];
            for (int i = 0; i < empty.Length; i++)
            {
                empty[i].radius = -1.0f;
                empty[i].visited = 0;
                empty[i].parent_atom1 = empty[i].parent_atom2 = empty[i].parent_atom3 = empty[i].parent_atom4 = -1;
            }
            buffer.SetData(empty);
            return buffer;
        }
        private ComputeBuffer InitPocketResultBuffer()
        {
            int stride = Marshal.SizeOf(typeof(FPocketResultCS));
            ComputeBuffer buffer = new ComputeBuffer(FPocketConstants.MAX_POCKETS, stride, ComputeBufferType.Default);
            FPocketResultCS[] empty = new FPocketResultCS[FPocketConstants.MAX_POCKETS];
            for (int i = 0; i < empty.Length; i++)
            {
                empty[i].id = -1;
                empty[i].lockFlag = 0;
                empty[i].volume = 0f;
            }
            buffer.SetData(empty);
            return buffer;
        }
        private void ReleaseBuffers(params ComputeBuffer[] buffers)
        {
            foreach (var buf in buffers)
            {
                if (buf != null)
                {
                    if (buf.IsValid())
                    {
                        buf.Release();
                    }
                    buf.Dispose();
                }
            }
        }
        private void ReadAndPrintGPUResults(ComputeBuffer pocketBuffer)
        {
            FPocketResultCS[] gpuPockets = new FPocketResultCS[FPocketConstants.MAX_POCKETS];
            pocketBuffer.GetData(gpuPockets);

            
            List<FPocketResultCS> validPockets = gpuPockets.Where(p => p.id != -1 && p.volume >= FPocketConstants.MIN_POCKET_VOLUME).ToList();

            validPockets = validPockets.OrderByDescending(_ => _.score).ToList();

            Debug.Log($"GPU pocket scoring produced {validPockets.Count} ranked pockets.");

            foreach (var p in validPockets)
            {
                Debug.Log($"Pocket {p.id}: score={p.score:F3}, volume={p.volume:F2}, alphaSpheres={p.nb_alpha_spheres}, atoms={p.nb_atoms}, center={p.center}");
                Debug.Log($"  hydrophobic={p.hydrophobic_score:F3}, polar={p.polar_score:F3}, depth={p.depth_score:F3}, density={p.density:F3}");
            }
        }
        #endregion

    }

}
