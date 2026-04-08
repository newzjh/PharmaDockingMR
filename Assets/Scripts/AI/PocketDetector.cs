using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using System.Linq;
using UnityEngine.Rendering;

namespace AIDrugDiscovery
{
    public enum FPocketImplementationMode
    {
        LegacyApproximate = 0,
        OfficialStyleCPU = 1,
        OfficialStyleGPU = 2
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
        public FPocketImplementationMode implementationMode = FPocketImplementationMode.LegacyApproximate;
        public float singleLinkageThreshold = 4.5f;

        
        private List<FPocketAtom> atoms;
        private List<FPocketAlphaSphere> alphaSpheres;
        [ContextMenu("Run FPocket CPU Version")]
        public void RunFPocketCPU()
        {
            if (implementationMode == FPocketImplementationMode.OfficialStyleCPU)
            {
                RunFPocketOfficialCPU();
                return;
            }

            
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
            if (implementationMode == FPocketImplementationMode.OfficialStyleGPU)
            {
                RunFPocketOfficialGPU();
                return;
            }

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
        private void RunFPocketOfficialCPU()
        {
            atoms = LoadAtomsFromPDBQT(pdbqtFilePath);
            if (atoms.Count < 4)
            {
                Debug.LogError("Not enough atoms to build alpha spheres.");
                return;
            }

            alphaSpheres = GenerateAlphaSpheresFPocketDirCPU(atoms);
            List<FPocketResult> pockets = DetectPocketsFPocketDir(alphaSpheres);
            PrintPocketResults(pockets);
        }

        private async void RunFPocketOfficialGPU()
        {
            if (fpocketComputeShader == null)
            {
                Debug.LogError("Compute shader is not assigned.");
                return;
            }

            atoms = LoadAtomsFromPDBQT(pdbqtFilePath);
            if (atoms.Count < 4)
            {
                Debug.LogError("Not enough atoms to build alpha spheres.");
                return;
            }

            int atomCount = atoms.Count;
            int threadGroups = Mathf.CeilToInt((float)atomCount / 256f);

            ComputeBuffer atomBuffer = null;
            ComputeBuffer neighborCountsBuffer = null;
            ComputeBuffer neighborIndicesBuffer = null;
            ComputeBuffer alphaSphereBuffer = null;
            ComputeBuffer pocketResultBuffer = null;
            ComputeBuffer sphereCountBuffer = null;
            ComputeBuffer clusterCountBuffer = null;

            try
            {
                atomBuffer = InitAtomBuffer(atoms);
                BuildNeighborBuffers(atoms, out int[] neighborCounts, out int[] neighborIndices, FPocketDirDefaults.MaxNeighbors);
                neighborCountsBuffer = new ComputeBuffer(atomCount, sizeof(int), ComputeBufferType.Structured);
                neighborIndicesBuffer = new ComputeBuffer(atomCount * FPocketDirDefaults.MaxNeighbors, sizeof(int), ComputeBufferType.Structured);
                neighborCountsBuffer.SetData(neighborCounts);
                neighborIndicesBuffer.SetData(neighborIndices);
                alphaSphereBuffer = InitAlphaSphereBuffer();
                pocketResultBuffer = InitPocketResultBuffer();
                sphereCountBuffer = new ComputeBuffer(1, sizeof(int));
                clusterCountBuffer = new ComputeBuffer(1, sizeof(int));
                int[] initCount = { 0 };
                sphereCountBuffer.SetData(initCount);
                clusterCountBuffer.SetData(initCount);
                SetShaderConstantsFPocketDir(fpocketComputeShader, atomCount, 0);

                int kernel1 = fpocketComputeShader.FindKernel("CSGenerateAlphaSpheresFPocketDir");
                fpocketComputeShader.SetBuffer(kernel1, "atomBuffer", atomBuffer);
                fpocketComputeShader.SetBuffer(kernel1, "atomNeighborCounts", neighborCountsBuffer);
                fpocketComputeShader.SetBuffer(kernel1, "atomNeighborIndices", neighborIndicesBuffer);
                fpocketComputeShader.SetBuffer(kernel1, "alphaSphereBuffer", alphaSphereBuffer);
                fpocketComputeShader.SetBuffer(kernel1, "pocketResultBuffer", pocketResultBuffer);
                fpocketComputeShader.SetBuffer(kernel1, "sphereCountBuffer", sphereCountBuffer);
                fpocketComputeShader.SetBuffer(kernel1, "clusterCountBuffer", clusterCountBuffer);
                fpocketComputeShader.Dispatch(kernel1, threadGroups, 1, 1);

                int[] generatedCountData = { 0 };
                sphereCountBuffer.GetData(generatedCountData);
                int generatedSphereCount = Mathf.Clamp(generatedCountData[0], 0, FPocketConstants.MAX_ALPHA_SPHERES);
                SetShaderConstantsFPocketDir(fpocketComputeShader, atomCount, generatedSphereCount);

                FPocketAlphaSphereCS[] data = null;
                var request = await AsyncGPUReadback.RequestAsync(alphaSphereBuffer);
                if (!request.hasError)
                    data = request.GetData<FPocketAlphaSphereCS>().ToArray();
                if (data == null)
                    return;

                List<FPocketAlphaSphere> validSpheres = new List<FPocketAlphaSphere>();
                for (int sphereIdx = 0; sphereIdx < generatedSphereCount && sphereIdx < data.Length; sphereIdx++)
                {
                    var sphere = data[sphereIdx];
                    if (sphere.radius <= 0)
                        continue;

                    validSpheres.Add(new FPocketAlphaSphere
                    {
                        center = sphere.center,
                        radius = sphere.radius,
                        nb_atoms = sphere.nb_atoms,
                        hydrophobicity = sphere.hydrophobicity,
                        polarity = sphere.polarity,
                        visited = sphere.visited,
                        parent_atoms = new[] { sphere.parent_atom1, sphere.parent_atom2, sphere.parent_atom3, sphere.parent_atom4 }
                    });
                }

                List<FPocketResult> pockets = DetectPocketsFPocketDir(validSpheres);
                PrintPocketResults(pockets);
            }
            catch (Exception e)
            {
                Debug.LogError($"Official-style GPU fpocket failed: {e.Message}\n{e.StackTrace}");
            }
            finally
            {
                ReleaseBuffers(atomBuffer, neighborCountsBuffer, neighborIndicesBuffer, alphaSphereBuffer, pocketResultBuffer, sphereCountBuffer, clusterCountBuffer);
            }
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
            List<FPocketAlphaSphere> alphaSpheres = new List<FPocketAlphaSphere>();
            Dictionary<Vector3Int, List<int>> spatialHash = BuildAtomSpatialHash(atoms, FPocketDirDefaults.MaxAsphereRadius);

            for (int atomIdx = 0; atomIdx < atoms.Count; atomIdx++)
            {
                List<int> nearbyAtoms = GetNearbyAtomIndices(atomIdx, atoms, spatialHash, FPocketDirDefaults.MaxAsphereRadius);
                if (nearbyAtoms.Count < 3)
                    continue;

                int nearbyLimit = Mathf.Min(nearbyAtoms.Count, FPocketDirDefaults.MaxNeighbors);
                for (int j = 0; j < nearbyLimit - 2; j++)
                {
                    int atomJ = nearbyAtoms[j];
                    if (atomJ <= atomIdx)
                        continue;

                    for (int k = j + 1; k < nearbyLimit - 1; k++)
                    {
                        int atomK = nearbyAtoms[k];
                        if (atomK <= atomJ)
                            continue;

                        for (int l = k + 1; l < nearbyLimit; l++)
                        {
                            int atomL = nearbyAtoms[l];
                            if (atomL <= atomK)
                                continue;

                            (Vector3 center, float radius) = ComputeCircumsphere(
                                atoms[atomIdx].pos,
                                atoms[atomJ].pos,
                                atoms[atomK].pos,
                                atoms[atomL].pos);
                            if (radius < FPocketDirDefaults.MinAsphereRadius || radius > FPocketDirDefaults.MaxAsphereRadius)
                                continue;

                            if (!IsEmptySphere(center, radius, atoms, atomIdx, atomJ, atomK, atomL))
                                continue;

                            int apol = 0;
                            if (atoms[atomIdx].electroneg < 2.8f) apol++;
                            if (atoms[atomJ].electroneg < 2.8f) apol++;
                            if (atoms[atomK].electroneg < 2.8f) apol++;
                            if (atoms[atomL].electroneg < 2.8f) apol++;
                            float isApolar = apol >= FPocketDirDefaults.MinApolNeigh ? 1f : 0f;

                            alphaSpheres.Add(new FPocketAlphaSphere
                            {
                                center = center,
                                radius = radius,
                                nb_atoms = 4,
                                hydrophobicity = isApolar,
                                polarity = 1f - isApolar,
                                visited = 0,
                                parent_atoms = new[] { atomIdx, atomJ, atomK, atomL }
                            });

                            if (alphaSpheres.Count >= FPocketConstants.MAX_ALPHA_SPHERES)
                                return alphaSpheres;
                        }
                    }
                }
            }

            return alphaSpheres;
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
                return pockets;

            List<List<int>> clusters = ClusterSpheresByDistance(spheres, FPocketDirDefaults.ClustMaxDist);
            clusters = RefineClustersByBarycenter(spheres, clusters, FPocketDirDefaults.RefineClustDist);

            FPocketDesc[] preFinalDescs = clusters.Select(c => ComputeDescriptorsFPocketDir(spheres, c, true)).ToArray();
            clusters = FinalClusterFPocketDir(spheres, clusters, preFinalDescs);

            FPocketDesc[] finalDescs = clusters.Select(c => ComputeDescriptorsFPocketDir(spheres, c, true)).ToArray();
            NormalizeDescriptorsFPocketDir(finalDescs);

            List<(FPocketResult result, FPocketDesc desc)> results = new List<(FPocketResult, FPocketDesc)>(clusters.Count);
            for (int i = 0; i < clusters.Count; i++)
            {
                FPocketDesc d = finalDescs[i];
                if (d.nb_asph < FPocketDirDefaults.MinPocketNbAsph)
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
                    hydrophobic_score = d.apolar_asphere_prop,
                    polar_score = d.polarity_score_norm,
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

        private List<List<int>> RefineClustersByBarycenter(List<FPocketAlphaSphere> spheres, List<List<int>> clusters, float dist)
        {
            int m = clusters.Count;
            if (m <= 1)
                return clusters;

            float distSqr = dist * dist;
            Vector3[] bary = new Vector3[m];
            for (int i = 0; i < m; i++)
                bary[i] = ComputeClusterCenter(spheres, clusters[i]);

            int[] parent = new int[m];
            int[] rank = new int[m];
            for (int i = 0; i < m; i++) parent[i] = i;

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

            for (int i = 0; i < m - 1; i++)
            {
                for (int j = i + 1; j < m; j++)
                {
                    Vector3 delta = bary[i] - bary[j];
                    if (delta.sqrMagnitude <= distSqr)
                        Union(i, j);
                }
            }

            Dictionary<int, List<int>> merged = new Dictionary<int, List<int>>();
            for (int i = 0; i < m; i++)
            {
                int r = Find(i);
                if (!merged.TryGetValue(r, out var lst))
                {
                    lst = new List<int>();
                    merged[r] = lst;
                }
                lst.AddRange(clusters[i]);
            }

            return merged.Values.ToList();
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

            HashSet<int> contactedAtoms = new HashSet<int>();

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
                            contactedAtoms.Add(atomIdx);
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

            foreach (int atomIdx in contactedAtoms)
            {
                int aa = atoms[atomIdx].aaIndex;
                if (aa < 0) continue;
                if (AAPropsByIndex.TryGetValue(aa, out AAProps props))
                {
                    d.hydrophobicity_score += props.hydrophobicity;
                    d.polarity_score += props.polarity;
                }
            }

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

        private void BuildNeighborBuffers(List<FPocketAtom> sourceAtoms, out int[] neighborCounts, out int[] neighborIndices, int maxNeighbors)
        {
            neighborCounts = new int[sourceAtoms.Count];
            neighborIndices = Enumerable.Repeat(-1, sourceAtoms.Count * maxNeighbors).ToArray();

            Dictionary<Vector3Int, List<int>> spatialHash = BuildAtomSpatialHash(sourceAtoms, FPocketDirDefaults.MaxAsphereRadius);
            for (int atomIdx = 0; atomIdx < sourceAtoms.Count; atomIdx++)
            {
                List<int> nearby = GetNearbyAtomIndices(atomIdx, sourceAtoms, spatialHash, FPocketDirDefaults.MaxAsphereRadius);
                if (nearby.Count > maxNeighbors)
                    nearby = nearby.Take(maxNeighbors).ToList();

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
                        string atomNameRaw = line.Substring(12, 2).Trim().ToUpper();
                        string atomSymbol = ExtractAtomSymbol(atomNameRaw);
                        string resName = line.Length >= 20 ? line.Substring(17, 3).Trim().ToUpperInvariant() : "";
                        int aaIndex = GetAaIndex(resName);

                        
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
                            res_id = 0
                            ,
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
        private void PrintPocketResults(List<FPocketResult> pockets)
        {
            var validPockets = pockets.OrderByDescending(_ => _.score).ToList();

            Debug.Log($"Pocket detection finished with {validPockets.Count} ranked pockets.");

            foreach (var p in validPockets)
            {
                Debug.Log($"Pocket {p.id}: score={p.score:F3}, volume={p.volume:F2}, alphaSpheres={p.nb_alpha_spheres}, atoms={p.nb_atoms}, center={p.center}");
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
