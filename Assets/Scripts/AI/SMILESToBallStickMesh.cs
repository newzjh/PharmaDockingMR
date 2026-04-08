using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Rendering;
using System.Text;

namespace AIDrugDiscovery
{

    
    // Configures the BallStick renderer used for atom-and-bond previews.
    [System.Serializable]
    public class BallStickConfig
    {
        public float bondLength = 1.5f;    
        public float atomRadius = 0.2f;    
        public float bondRadius = 0.1f;    
        public int sphereSegments = 12;    
        public int cylinderSegments = 8;   
        public int topK = 10;              
    }
    // Expands a precomputed atom/bond graph into BallStick mesh buffers.
    public class SMILESToBallStickMesh : MonoBehaviour
    {
        public ComputeShader ballStickCS;
        public BallStickConfig config;
        
        public int batchSize = 128;              
        public int smilesMaxLength = 256;  
        public int maxAtomLimit = 60;
        public int maxExtraBondCount = 12;
        public bool useSelectedSubsetDispatch = true;
        public bool useLegacySmilesTextureInput = false;
#if SMILES_GRAPH_DEBUG
        public bool enableGraphDebugProbe = false;
        public int debugProbeMeshCount = 4;
#endif

        private ComputeBuffer vertexBufferPosition;
        private ComputeBuffer vertexBufferColor;
        private ComputeBuffer indexBuffer;
        private ComputeBuffer atomCountBuffer; 
        private ComputeBuffer bondCountBuffer;
        private ComputeBuffer selectedIndexBuffer;
        private ComputeBuffer meshAtomStartBuffer;
        private ComputeBuffer meshAtomCountInputBuffer;
        private ComputeBuffer meshBondStartBuffer;
        private ComputeBuffer meshBondCountInputBuffer;
        private ComputeBuffer atomTypeInputBuffer;
        private ComputeBuffer atomPositionInputBuffer;
        private ComputeBuffer bondInputBuffer;
#if SMILES_GRAPH_DEBUG
        private ComputeBuffer graphDebugBuffer;
#endif
        private ComputeBuffer dummySmilesInputBuffer;
#if SMILES_GRAPH_DEBUG
        private ComputeBuffer dummyGraphDebugBuffer;
#endif
        private Texture2D dummySmilesInputTexture;
        private int maxVertexCount;
        private int maxIndexCount;
        private int allocatedBatchSize;
        private int maxBondLimit;
#if SMILES_GRAPH_DEBUG
        private const int GraphDebugStride = 16;
#endif

        private int[] BuildSmilesData(string smiles)
        {
            int[] smilesData = new int[smilesMaxLength];
            if (string.IsNullOrEmpty(smiles))
                return smilesData;

            int copyLength = Mathf.Min(smiles.Length, smilesMaxLength - 1);
            for (int i = 0; i < copyLength; i++)
                smilesData[i] = smiles[i];
            return smilesData;
        }

        public void Awake()
        {
            EnsureBuffers(batchSize);
            dummySmilesInputBuffer = new ComputeBuffer(1, sizeof(int));
            dummySmilesInputBuffer.SetData(new[] { 0 });
#if SMILES_GRAPH_DEBUG
            dummyGraphDebugBuffer = new ComputeBuffer(1, sizeof(int));
            dummyGraphDebugBuffer.SetData(new[] { 0 });
#endif
            dummySmilesInputTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            dummySmilesInputTexture.SetPixel(0, 0, Color.clear);
            dummySmilesInputTexture.Apply();
        }

        private void EnsureBuffers(int requiredBatchSize)
        {
            if (requiredBatchSize <= 0)
                requiredBatchSize = 1;

            if (vertexBufferPosition != null && allocatedBatchSize >= requiredBatchSize)
                return;

            vertexBufferPosition?.Release();
            vertexBufferColor?.Release();
            indexBuffer?.Release();
            atomCountBuffer?.Release();
            bondCountBuffer?.Release();
            ReleaseInputBuffers();

            int verticesPerAtom = (config.sphereSegments + 1) * (config.sphereSegments + 1);
            int verticesPerBond = 2 * (config.cylinderSegments + 1);
            int indicesPerAtom = config.sphereSegments * config.sphereSegments * 6;
            int indicesPerBond = config.cylinderSegments * 6;

            allocatedBatchSize = requiredBatchSize;
            maxBondLimit = maxAtomLimit + maxExtraBondCount;
            maxVertexCount = allocatedBatchSize * (maxAtomLimit * verticesPerAtom + maxBondLimit * verticesPerBond);
            maxIndexCount = allocatedBatchSize * (maxAtomLimit * indicesPerAtom + maxBondLimit * indicesPerBond);

            
            vertexBufferPosition = new ComputeBuffer(maxVertexCount * 2, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3)));
            vertexBufferColor = new ComputeBuffer(maxVertexCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector4)));
            indexBuffer = new ComputeBuffer(maxIndexCount, sizeof(int));
            atomCountBuffer = new ComputeBuffer(allocatedBatchSize, sizeof(int));
            bondCountBuffer = new ComputeBuffer(allocatedBatchSize, sizeof(int));
        }

        private void ReleaseInputBuffers()
        {
            selectedIndexBuffer?.Release();
            meshAtomStartBuffer?.Release();
            meshAtomCountInputBuffer?.Release();
            meshBondStartBuffer?.Release();
            meshBondCountInputBuffer?.Release();
            atomTypeInputBuffer?.Release();
            atomPositionInputBuffer?.Release();
            bondInputBuffer?.Release();
#if SMILES_GRAPH_DEBUG
            graphDebugBuffer?.Release();
#endif
        }

        private void AllocateBatchGraphBuffers(int meshCount)
        {
            ReleaseInputBuffers();

            int[] atomStarts = new int[meshCount];
            int[] bondStarts = new int[meshCount];
            for (int meshIdx = 0; meshIdx < meshCount; meshIdx++)
            {
                atomStarts[meshIdx] = meshIdx * maxAtomLimit;
                bondStarts[meshIdx] = meshIdx * maxBondLimit;
            }

            meshAtomStartBuffer = new ComputeBuffer(meshCount, sizeof(int));
            meshAtomCountInputBuffer = new ComputeBuffer(meshCount, sizeof(int));
            meshBondStartBuffer = new ComputeBuffer(meshCount, sizeof(int));
            meshBondCountInputBuffer = new ComputeBuffer(meshCount, sizeof(int));
            atomTypeInputBuffer = new ComputeBuffer(Mathf.Max(1, meshCount * maxAtomLimit), sizeof(int));
            atomPositionInputBuffer = new ComputeBuffer(Mathf.Max(1, meshCount * maxAtomLimit), Marshal.SizeOf(typeof(Vector3)));
            bondInputBuffer = new ComputeBuffer(Mathf.Max(1, meshCount * maxBondLimit), sizeof(int) * 2);
#if SMILES_GRAPH_DEBUG
            graphDebugBuffer = new ComputeBuffer(Mathf.Max(1, meshCount * GraphDebugStride), sizeof(int));
#endif

            meshAtomStartBuffer.SetData(atomStarts);
            meshAtomCountInputBuffer.SetData(new int[meshCount]);
            meshBondStartBuffer.SetData(bondStarts);
            meshBondCountInputBuffer.SetData(new int[meshCount]);
            atomTypeInputBuffer.SetData(new int[Mathf.Max(1, meshCount * maxAtomLimit)]);
            atomPositionInputBuffer.SetData(new Vector3[Mathf.Max(1, meshCount * maxAtomLimit)]);
            bondInputBuffer.SetData(new SmilesMeshBondIndex[Mathf.Max(1, meshCount * maxBondLimit)]);
#if SMILES_GRAPH_DEBUG
            graphDebugBuffer.SetData(new int[Mathf.Max(1, meshCount * GraphDebugStride)]);
#endif
        }

        public bool test = true;
        public async UniTask<List<Mesh>> GenerateBallStickMeshes(List<int> filteredIndices, ComputeBuffer smilesBuffer, int runtimeBatchSize, Texture legacySmilesTexture = null)
        {
            List<Mesh> molMeshes = new List<Mesh>();
            if ((smilesBuffer == null && !(useLegacySmilesTextureInput && legacySmilesTexture != null)) || runtimeBatchSize <= 0 || filteredIndices == null || filteredIndices.Count == 0)
                return molMeshes;

            int generatedMeshCount = useSelectedSubsetDispatch ? filteredIndices.Count : runtimeBatchSize;
            if (generatedMeshCount == 0)
                return molMeshes;

            EnsureBuffers(generatedMeshCount);
            AllocateBatchGraphBuffers(generatedMeshCount);
            int verticesPerAtom = (config.sphereSegments + 1) * (config.sphereSegments + 1);
            int verticesPerBond = 2 * (config.cylinderSegments + 1);
            int indicesPerAtom = config.sphereSegments * config.sphereSegments * 6;
            int indicesPerBond = config.cylinderSegments * 6;

            selectedIndexBuffer = new ComputeBuffer(generatedMeshCount, sizeof(int));
            int[] selectedIndices = new int[generatedMeshCount];
            if (useSelectedSubsetDispatch)
            {
                for (int i = 0; i < generatedMeshCount; i++)
                    selectedIndices[i] = filteredIndices[i];
            }
            else
            {
                for (int i = 0; i < generatedMeshCount; i++)
                    selectedIndices[i] = i;
            }
            selectedIndexBuffer.SetData(selectedIndices);

            int threadGroupX = Mathf.CeilToInt(generatedMeshCount / 32f);
            int kernelGraph = ballStickCS.FindKernel("CSBuildBallStickGraphBatch");
            int kernelLayout = ballStickCS.FindKernel("CSBuildBallStickLayoutBatch");
            int kernelMesh = ballStickCS.FindKernel("CSGenerateBallStickMesh");
            bool useTextureInputForDispatch = (smilesBuffer == null) && useLegacySmilesTextureInput && legacySmilesTexture != null;

            foreach (int kernelId in new[] { kernelGraph, kernelLayout, kernelMesh })
            {
                int shaderSmilesLength = DiffusionGenerator.SMILES_MAX_LENGTH;
                ballStickCS.SetInt("batchSize", runtimeBatchSize);
                ballStickCS.SetInt("selectedCount", generatedMeshCount);
                ballStickCS.SetInt("useSmilesTextureInput", useTextureInputForDispatch ? 1 : 0);
                ballStickCS.SetInt("smilesMaxLength", shaderSmilesLength);
                ballStickCS.SetInt("sphereSegments", config.sphereSegments);
                ballStickCS.SetInt("cylinderSegments", config.cylinderSegments);
                ballStickCS.SetFloat("bondLength", config.bondLength);
                ballStickCS.SetFloat("atomRadius", config.atomRadius);
                ballStickCS.SetFloat("bondRadius", config.bondRadius);
                ballStickCS.SetInt("maxBondCount", maxBondLimit);
                ballStickCS.SetInt("vertexCapacity", maxVertexCount);
                ballStickCS.SetBuffer(kernelId, "smilesInputBuffer", smilesBuffer ?? dummySmilesInputBuffer);
                ballStickCS.SetTexture(kernelId, "smilesInputTexture", legacySmilesTexture ?? dummySmilesInputTexture);
                ballStickCS.SetBuffer(kernelId, "selectedMolIndexBuffer", selectedIndexBuffer);
                ballStickCS.SetBuffer(kernelId, "meshAtomStartBuffer", meshAtomStartBuffer);
                ballStickCS.SetBuffer(kernelId, "meshAtomCountInputBuffer", meshAtomCountInputBuffer);
                ballStickCS.SetBuffer(kernelId, "meshBondStartBuffer", meshBondStartBuffer);
                ballStickCS.SetBuffer(kernelId, "meshBondCountInputBuffer", meshBondCountInputBuffer);
                ballStickCS.SetBuffer(kernelId, "atomTypeInputBuffer", atomTypeInputBuffer);
                ballStickCS.SetBuffer(kernelId, "atomPositionInputBuffer", atomPositionInputBuffer);
                ballStickCS.SetBuffer(kernelId, "bondInputBuffer", bondInputBuffer);
#if SMILES_GRAPH_DEBUG
                ballStickCS.SetInt("enableGraphDebug", enableGraphDebugProbe ? 1 : 0);
                ballStickCS.SetBuffer(kernelId, "graphDebugBuffer", graphDebugBuffer ?? dummyGraphDebugBuffer);
#endif
            }

            ballStickCS.SetBuffer(kernelMesh, "vertexPosNormalBuffer", vertexBufferPosition);
            ballStickCS.SetBuffer(kernelMesh, "vertexOutputBuffer_color", vertexBufferColor);
            ballStickCS.SetBuffer(kernelMesh, "indexOutputBuffer", indexBuffer);

            ballStickCS.Dispatch(kernelGraph, threadGroupX, 1, 1);

#if SMILES_GRAPH_DEBUG
            if (enableGraphDebugProbe)
            {
                List<string> decodedSmiles = null;
                try
                {
                    decodedSmiles = await SmilesMeshPreprocessor.ReadSmilesBatchAsync(
                        smilesBuffer,
                        runtimeBatchSize,
                        DiffusionGenerator.SMILES_MAX_LENGTH,
                        legacySmilesTexture);
                }
                catch
                {
                    decodedSmiles = null;
                }

                int[] probeAtomCounts = (await AsyncGPUReadback.RequestAsync(meshAtomCountInputBuffer)).GetData<int>().ToArray();
                int[] probeBondCounts = (await AsyncGPUReadback.RequestAsync(meshBondCountInputBuffer)).GetData<int>().ToArray();
                SmilesMeshBondIndex[] probeBonds = (await AsyncGPUReadback.RequestAsync(bondInputBuffer)).GetData<SmilesMeshBondIndex>().ToArray();
                int[] probeAtomTypes = (await AsyncGPUReadback.RequestAsync(atomTypeInputBuffer)).GetData<int>().ToArray();
                int[] probeGraphDbg = (graphDebugBuffer != null)
                    ? (await AsyncGPUReadback.RequestAsync(graphDebugBuffer)).GetData<int>().ToArray()
                    : Array.Empty<int>();
                int probeCount = Mathf.Min(generatedMeshCount, Mathf.Max(1, debugProbeMeshCount));
                for (int m = 0; m < probeCount; m++)
                {
                    int molIdxDbg = (selectedIndices != null && m < selectedIndices.Length) ? selectedIndices[m] : m;
                    string smilesDbg = (decodedSmiles != null && molIdxDbg >= 0 && molIdxDbg < decodedSmiles.Count) ? decodedSmiles[molIdxDbg] : string.Empty;
                    string smilesHead = smilesDbg;
                    if (!string.IsNullOrEmpty(smilesHead) && smilesHead.Length > 80)
                        smilesHead = smilesHead.Substring(0, 80) + "...";
                    bool hasDigit = false;
                    for (int si = 0; si < smilesDbg.Length; si++)
                    {
                        char ch = smilesDbg[si];
                        if (ch >= '0' && ch <= '9') { hasDigit = true; break; }
                    }
                    bool hasBranch = !string.IsNullOrEmpty(smilesDbg) && (smilesDbg.IndexOf('(') >= 0 || smilesDbg.IndexOf(')') >= 0);
                    bool hasPercentRing = !string.IsNullOrEmpty(smilesDbg) && smilesDbg.IndexOf('%') >= 0;
                    int ringTokenCount = 0;
                    int ringPairedCount = 0;
                    int ringUnpairedLabels = 0;
                    int cpuAtomCount = 0;
                    int cpuBondCount = 0;
                    int cpuCycleEdge = 0;
                    int cpuRingClose = 0;
                    int cpuRingCloseUnique = 0;
                    if (!string.IsNullOrEmpty(smilesDbg))
                    {
                        Dictionary<int, int> ringCounts = new Dictionary<int, int>();
                        for (int si = 0; si < smilesDbg.Length; si++)
                        {
                            char ch = smilesDbg[si];
                            if (ch == '%' && si + 2 < smilesDbg.Length)
                            {
                                char d1 = smilesDbg[si + 1];
                                char d2 = smilesDbg[si + 2];
                                if (d1 >= '0' && d1 <= '9' && d2 >= '0' && d2 <= '9')
                                {
                                    int label = (d1 - '0') * 10 + (d2 - '0');
                                    ringTokenCount++;
                                    ringCounts.TryGetValue(label, out int cur);
                                    ringCounts[label] = cur + 1;
                                    si += 2;
                                    continue;
                                }
                            }
                            if (ch >= '0' && ch <= '9')
                            {
                                int label = ch - '0';
                                ringTokenCount++;
                                ringCounts.TryGetValue(label, out int cur);
                                ringCounts[label] = cur + 1;
                            }
                        }
                        foreach (var kv in ringCounts)
                        {
                            ringPairedCount += kv.Value / 2;
                            if ((kv.Value & 1) != 0)
                                ringUnpairedLabels++;
                        }

                        List<(int a, int b)> cpuBonds = new List<(int a, int b)>(64);
                        HashSet<long> cpuEdgeSeen = new HashSet<long>();
                        int[] ringAtom = new int[128];
                        for (int ri = 0; ri < ringAtom.Length; ri++) ringAtom[ri] = -1;
                        int[] branchStack = new int[64];
                        int branchTop = 0;
                        int currentAtomCpu = -1;
                        int pendingBondCpu = 0;
                        bool TryMapAtomCpu(char a0, char a1, out int consumedCpu)
                        {
                            consumedCpu = 0;
                            if (a0 == '\0') return false;
                            if (a0 == 'C' && a1 == 'l') { consumedCpu = 2; return true; }
                            if (a0 == 'B' && a1 == 'r') { consumedCpu = 2; return true; }
                            if (a0 == 'S' && a1 == 'i') { consumedCpu = 2; return true; }
                            if (a0 == 'A' && a1 == 's') { consumedCpu = 2; return true; }
                            if (a0 == 'S' && a1 == 'e') { consumedCpu = 2; return true; }
                            switch (a0)
                            {
                                case 'H':
                                case 'B':
                                case 'C':
                                case 'N':
                                case 'O':
                                case 'F':
                                case 'P':
                                case 'S':
                                case 'I':
                                case 'c':
                                case 'n':
                                case 'o':
                                case 's':
                                case 'p':
                                    consumedCpu = 1;
                                    return true;
                                default:
                                    return false;
                            }
                        }

                        for (int si = 0; si < smilesDbg.Length;)
                        {
                            char c0 = smilesDbg[si];
                            char c1 = (si + 1 < smilesDbg.Length) ? smilesDbg[si + 1] : '\0';
                            if (c0 == '=') { pendingBondCpu = 1; si++; continue; }
                            if (c0 == '#') { pendingBondCpu = 2; si++; continue; }
                            if (c0 == ':') { pendingBondCpu = 3; si++; continue; }
                            if (c0 == '-') { pendingBondCpu = 0; si++; continue; }
                            if (c0 == '.') { currentAtomCpu = -1; pendingBondCpu = 0; si++; continue; }
                            if (c0 == '/' || c0 == '\\' || c0 == '@') { si++; continue; }
                            if (c0 == '(') { if (branchTop < branchStack.Length) branchStack[branchTop++] = currentAtomCpu; si++; continue; }
                            if (c0 == ')') { if (branchTop > 0) currentAtomCpu = branchStack[--branchTop]; si++; continue; }
                            if (c0 == '%')
                            {
                                if (si + 2 < smilesDbg.Length)
                                {
                                    char d1 = smilesDbg[si + 1];
                                    char d2 = smilesDbg[si + 2];
                                    if (d1 >= '0' && d1 <= '9' && d2 >= '0' && d2 <= '9')
                                    {
                                        int ringNumber = (d1 - '0') * 10 + (d2 - '0');
                                        if (currentAtomCpu >= 0)
                                        {
                                            if (ringNumber >= 0 && ringNumber < ringAtom.Length)
                                            {
                                                if (ringAtom[ringNumber] >= 0)
                                                {
                                                    int a = ringAtom[ringNumber];
                                                    int b = currentAtomCpu;
                                                    if (a != b)
                                                    {
                                                        int lo = a < b ? a : b;
                                                        int hi = a < b ? b : a;
                                                        long key = ((long)lo << 32) | (uint)hi;
                                                        cpuRingClose++;
                                                        if (cpuEdgeSeen.Add(key))
                                                        {
                                                            cpuRingCloseUnique++;
                                                            cpuBonds.Add((a, b));
                                                        }
                                                    }
                                                    ringAtom[ringNumber] = -1;
                                                }
                                                else
                                                {
                                                    ringAtom[ringNumber] = currentAtomCpu;
                                                }
                                            }
                                        }
                                        pendingBondCpu = 0;
                                        si += 3;
                                        continue;
                                    }
                                }
                            }
                            if (c0 >= '0' && c0 <= '9')
                            {
                                int ringNumber = c0 - '0';
                                if (currentAtomCpu >= 0)
                                {
                                    if (ringNumber >= 0 && ringNumber < ringAtom.Length)
                                    {
                                        if (ringAtom[ringNumber] >= 0)
                                        {
                                            int a = ringAtom[ringNumber];
                                            int b = currentAtomCpu;
                                            if (a != b)
                                            {
                                                int lo = a < b ? a : b;
                                                int hi = a < b ? b : a;
                                                long key = ((long)lo << 32) | (uint)hi;
                                                cpuRingClose++;
                                                if (cpuEdgeSeen.Add(key))
                                                {
                                                    cpuRingCloseUnique++;
                                                    cpuBonds.Add((a, b));
                                                }
                                            }
                                            ringAtom[ringNumber] = -1;
                                        }
                                        else
                                        {
                                            ringAtom[ringNumber] = currentAtomCpu;
                                        }
                                    }
                                }
                                pendingBondCpu = 0;
                                si++;
                                continue;
                            }
                            if (TryMapAtomCpu(c0, c1, out int consumedCpu))
                            {
                                int newAtom = cpuAtomCount;
                                cpuAtomCount++;
                                if (currentAtomCpu >= 0)
                                {
                                    int a = currentAtomCpu;
                                    int b = newAtom;
                                    int lo = a < b ? a : b;
                                    int hi = a < b ? b : a;
                                    long key = ((long)lo << 32) | (uint)hi;
                                    if (cpuEdgeSeen.Add(key))
                                        cpuBonds.Add((a, b));
                                }
                                currentAtomCpu = newAtom;
                                pendingBondCpu = 0;
                                si += consumedCpu;
                                continue;
                            }
                            si++;
                        }

                        if (cpuBonds.Count > 0)
                        {
                            HashSet<long> uniq = new HashSet<long>();
                            List<(int a, int b)> compact = new List<(int a, int b)>(cpuBonds.Count);
                            for (int bi = 0; bi < cpuBonds.Count; bi++)
                            {
                                var b = cpuBonds[bi];
                                if (b.a == b.b) continue;
                                int lo = b.a < b.b ? b.a : b.b;
                                int hi = b.a < b.b ? b.b : b.a;
                                long key = ((long)lo << 32) | (uint)hi;
                                if (uniq.Add(key))
                                    compact.Add((lo, hi));
                            }
                            cpuBonds = compact;
                        }
                        cpuBondCount = cpuBonds.Count;
                        if (cpuAtomCount > 0 && cpuBondCount > 0)
                        {
                            int[] ufCpu = new int[cpuAtomCount];
                            for (int ui = 0; ui < ufCpu.Length; ui++) ufCpu[ui] = ui;
                            int FindCpu(int x)
                            {
                                while (ufCpu[x] != x)
                                {
                                    ufCpu[x] = ufCpu[ufCpu[x]];
                                    x = ufCpu[x];
                                }
                                return x;
                            }
                            void UnionCpu(int a, int b)
                            {
                                int ra = FindCpu(a);
                                int rb = FindCpu(b);
                                if (ra == rb) { cpuCycleEdge++; return; }
                                ufCpu[rb] = ra;
                            }
                            for (int bi = 0; bi < cpuBonds.Count; bi++)
                            {
                                var b = cpuBonds[bi];
                                if (b.a >= 0 && b.a < cpuAtomCount && b.b >= 0 && b.b < cpuAtomCount && b.a != b.b)
                                    UnionCpu(b.a, b.b);
                            }
                        }
                    }

                    int atomCountDbg = m < probeAtomCounts.Length ? probeAtomCounts[m] : 0;
                    int bondCountDbg = m < probeBondCounts.Length ? probeBondCounts[m] : 0;
                    int atomStartDbg = m * maxAtomLimit;
                    int bondStartDbg = m * maxBondLimit;
                    int reachable = 0;
                    if (atomCountDbg > 0)
                    {
                        bool[] seen = new bool[atomCountDbg];
                        Queue<int> q = new Queue<int>();
                        seen[0] = true;
                        q.Enqueue(0);
                        while (q.Count > 0)
                        {
                            int cur = q.Dequeue();
                            reachable++;
                            for (int bi = 0; bi < bondCountDbg; bi++)
                            {
                                int idx = bondStartDbg + bi;
                                if (idx < 0 || idx >= probeBonds.Length) continue;
                                var b = probeBonds[idx];
                                int n = -1;
                                if (b.AtomA == cur) n = b.AtomB;
                                else if (b.AtomB == cur) n = b.AtomA;
                                if (n < 0 || n >= atomCountDbg || seen[n]) continue;
                                seen[n] = true;
                                q.Enqueue(n);
                            }
                        }
                    }
                    int t0 = (atomStartDbg + 0 < probeAtomTypes.Length) ? probeAtomTypes[atomStartDbg] : -1;
                    int t1 = (atomStartDbg + 1 < probeAtomTypes.Length) ? probeAtomTypes[atomStartDbg + 1] : -1;
                    int t2 = (atomStartDbg + 2 < probeAtomTypes.Length) ? probeAtomTypes[atomStartDbg + 2] : -1;
                    int dbgBase = m * GraphDebugStride;
                    int dbgDigitToken = (dbgBase + 0 < probeGraphDbg.Length) ? probeGraphDbg[dbgBase + 0] : -1;
                    int dbgPercentToken = (dbgBase + 1 < probeGraphDbg.Length) ? probeGraphDbg[dbgBase + 1] : -1;
                    int dbgRingOpen = (dbgBase + 2 < probeGraphDbg.Length) ? probeGraphDbg[dbgBase + 2] : -1;
                    int dbgRingClose = (dbgBase + 3 < probeGraphDbg.Length) ? probeGraphDbg[dbgBase + 3] : -1;
                    int dbgBondPreCompact = (dbgBase + 4 < probeGraphDbg.Length) ? probeGraphDbg[dbgBase + 4] : -1;
                    int dbgBondPostCompact = (dbgBase + 5 < probeGraphDbg.Length) ? probeGraphDbg[dbgBase + 5] : -1;
                    int dbgBondPostConn = (dbgBase + 6 < probeGraphDbg.Length) ? probeGraphDbg[dbgBase + 6] : -1;
                    int dbgSkipSelf = (dbgBase + 8 < probeGraphDbg.Length) ? probeGraphDbg[dbgBase + 8] : -1;
                    int dbgSkipRange = (dbgBase + 9 < probeGraphDbg.Length) ? probeGraphDbg[dbgBase + 9] : -1;
                    int dbgSkipDup = (dbgBase + 10 < probeGraphDbg.Length) ? probeGraphDbg[dbgBase + 10] : -1;
                    int dbgMinEnd = (dbgBase + 11 < probeGraphDbg.Length) ? probeGraphDbg[dbgBase + 11] : -1;
                    int dbgMaxEnd = (dbgBase + 12 < probeGraphDbg.Length) ? probeGraphDbg[dbgBase + 12] : -1;
                    int dbgAtomStart = (dbgBase + 13 < probeGraphDbg.Length) ? probeGraphDbg[dbgBase + 13] : -1;
                    int dbgBondStart = (dbgBase + 14 < probeGraphDbg.Length) ? probeGraphDbg[dbgBase + 14] : -1;
                    int dbgMaxBondCount = (dbgBase + 15 < probeGraphDbg.Length) ? probeGraphDbg[dbgBase + 15] : -1;
                    int validBond = 0;
                    int selfBond = 0;
                    StringBuilder firstBonds = new StringBuilder(160);
                    int showBond = Mathf.Min(bondCountDbg, 12);
                    int cycleEdge = 0;
                    if (atomCountDbg > 0 && bondCountDbg > 0)
                    {
                        int[] uf = new int[atomCountDbg];
                        for (int ui = 0; ui < atomCountDbg; ui++) uf[ui] = ui;
                        int Find(int x)
                        {
                            while (uf[x] != x)
                            {
                                uf[x] = uf[uf[x]];
                                x = uf[x];
                            }
                            return x;
                        }
                        void Union(int a, int b)
                        {
                            int ra = Find(a);
                            int rb = Find(b);
                            if (ra == rb) { cycleEdge++; return; }
                            uf[rb] = ra;
                        }
                        for (int bi = 0; bi < bondCountDbg; bi++)
                        {
                            int idx = bondStartDbg + bi;
                            if (idx < 0 || idx >= probeBonds.Length) continue;
                            var b = probeBonds[idx];
                            if (b.AtomA >= 0 && b.AtomA < atomCountDbg && b.AtomB >= 0 && b.AtomB < atomCountDbg && b.AtomA != b.AtomB)
                                Union(b.AtomA, b.AtomB);
                        }
                    }
                    for (int bi = 0; bi < bondCountDbg; bi++)
                    {
                        int idx = bondStartDbg + bi;
                        if (idx < 0 || idx >= probeBonds.Length) continue;
                        var b = probeBonds[idx];
                        if (b.AtomA == b.AtomB) selfBond++;
                        else if (b.AtomA >= 0 && b.AtomA < atomCountDbg && b.AtomB >= 0 && b.AtomB < atomCountDbg) validBond++;
                        if (bi < showBond)
                        {
                            if (bi > 0) firstBonds.Append(' ');
                            firstBonds.Append('(').Append(b.AtomA).Append('-').Append(b.AtomB).Append(')');
                        }
                    }
                    Debug.Log($"[BallStickGraphProbe] mesh={m} molIdx={molIdxDbg} atomCount={atomCountDbg} bondCount={bondCountDbg} validBond={validBond} selfBond={selfBond} reachableFrom0={reachable}/{atomCountDbg} cycleEdge={cycleEdge} headTypes={t0},{t1},{t2} hasDigit={hasDigit} hasPercentRing={hasPercentRing} hasBranch={hasBranch} ringTokenCount={ringTokenCount} ringPairedCount={ringPairedCount} ringUnpairedLabels={ringUnpairedLabels} cpuAtomCount={cpuAtomCount} cpuBondCount={cpuBondCount} cpuCycleEdge={cpuCycleEdge} cpuRingClose={cpuRingClose} cpuRingCloseUnique={cpuRingCloseUnique} gpuDigitToken={dbgDigitToken} gpuPercentToken={dbgPercentToken} gpuRingOpen={dbgRingOpen} gpuRingClose={dbgRingClose} gpuBondPreCompact={dbgBondPreCompact} gpuBondPostCompact={dbgBondPostCompact} gpuBondPostConn={dbgBondPostConn} gpuSkipSelf={dbgSkipSelf} gpuSkipRange={dbgSkipRange} gpuSkipDup={dbgSkipDup} gpuMinEnd={dbgMinEnd} gpuMaxEnd={dbgMaxEnd} gpuAtomStart={dbgAtomStart} gpuBondStart={dbgBondStart} gpuMaxBondCount={dbgMaxBondCount} smilesHead={smilesHead} firstBonds={firstBonds}");
                }
            }
#endif

            ballStickCS.Dispatch(kernelLayout, threadGroupX, 1, 1);
            ballStickCS.Dispatch(kernelMesh, threadGroupX, 1, 1);

            
            int[] atomCounts = new int[generatedMeshCount];
            int[] bondCounts = new int[generatedMeshCount];
            {
                var req = await AsyncGPUReadback.RequestAsync(meshAtomCountInputBuffer);
                atomCounts = req.GetData<int>().ToArray();
            }
            {
                var req = await AsyncGPUReadback.RequestAsync(meshBondCountInputBuffer);
                bondCounts = req.GetData<int>().ToArray();
            }
            //atomCountBuffer.GetData(atomCounts);

            
            Vector3[] allPosNormals = new Vector3[maxVertexCount * 2];
            Vector4[] allColors = new Vector4[maxVertexCount];
            int[] allIndices = new int[maxIndexCount];
            //vertexBufferPosition.GetData(allPositions);
            //vertexBufferNormal.GetData(allNormals);
            //vertexBufferColor.GetData(allColors);
            //indexBuffer.GetData(allIndices);
            {
                var req = await AsyncGPUReadback.RequestAsync(vertexBufferPosition);
                allPosNormals = req.GetData<Vector3>().ToArray();
            }
            {
                var req = await AsyncGPUReadback.RequestAsync(vertexBufferColor);
                allColors = req.GetData<Vector4>().ToArray();
            }
            {
                var req = await AsyncGPUReadback.RequestAsync(indexBuffer);
                allIndices = req.GetData<int>().ToArray();
            }

            
            int vertexOffset = 0;
            int indexOffset = 0;
            for (int meshIdx = 0; meshIdx < generatedMeshCount; meshIdx++)
            {
                int atomCount = atomCounts[meshIdx];
                int bondCount = bondCounts[meshIdx];
                if (atomCount <= 1) 
                    continue;

                vertexOffset = meshIdx * (maxAtomLimit * verticesPerAtom + maxBondLimit * verticesPerBond);
                indexOffset = meshIdx * (maxAtomLimit * indicesPerAtom + maxBondLimit * indicesPerBond);

                
                int totalVertices = atomCount * verticesPerAtom + bondCount * verticesPerBond;
                int totalIndices = atomCount * indicesPerAtom + bondCount * indicesPerBond;
                if (vertexOffset + totalVertices > maxVertexCount || indexOffset + totalIndices > maxIndexCount) 
                    break;

                
                Mesh mesh = new Mesh();
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                Vector3[] positions = new Vector3[totalVertices];
                Color[] colors = new Color[totalVertices];
                int[] triangles = new int[totalIndices];

                unsafe
                {
                    Vector4* src = (Vector4*)UnsafeUtility.AddressOf<Vector4>(ref allColors[0]) + vertexOffset;
                    Color* dest = (Color*)UnsafeUtility.AddressOf<Color>(ref colors[0]);
                    UnsafeUtility.MemCpy(dest, src, totalVertices * UnsafeUtility.SizeOf<Vector4>());
                }
                Array.Copy(allPosNormals, vertexOffset, positions, 0, totalVertices);
                Array.Copy(allIndices, indexOffset, triangles, 0, totalIndices);
                for (int v = 0; v < totalVertices; v++)
                {
                    if (float.IsNaN(positions[v].x) || float.IsNaN(positions[v].y) || float.IsNaN(positions[v].z) ||
                        float.IsInfinity(positions[v].x) || float.IsInfinity(positions[v].y) || float.IsInfinity(positions[v].z))
                    {
                        positions[v] = Vector3.zero;
                    }
                }

                mesh.vertices = positions;
                mesh.colors = colors;
                mesh.triangles = triangles;
                if (allPosNormals.Length >= maxVertexCount + vertexOffset + totalVertices)
                {
                    Vector3[] normals = new Vector3[totalVertices];
                    Array.Copy(allPosNormals, maxVertexCount + vertexOffset, normals, 0, totalVertices);
                    mesh.normals = normals;
                }
                else
                {
                    mesh.RecalculateNormals();
                }
                mesh.RecalculateBounds();
                molMeshes.Add(mesh);
            }

            return molMeshes;
        }

        public async UniTask<Mesh> GenerateSingleBallStickMesh(string smiles)
        {
            if (string.IsNullOrEmpty(smiles))
                return null;

            return await GenerateSingleBallStickMesh(BuildSmilesData(smiles));
        }

        public async UniTask<Mesh> GenerateSingleBallStickMesh(int[] smilesData)
        {
            if (smilesData == null || smilesData.Length == 0)
                return null;

            string smiles = SmilesMeshPreprocessor.DecodeAsciiSmiles(smilesData);
            SmilesMeshDescription description = SmilesMeshPreprocessor.Build(smiles, config.bondLength);
            if (description.AtomTypes.Count <= 1)
                return null;

            EnsureBuffers(1);
            AllocateBatchGraphBuffers(1);
            meshAtomCountInputBuffer.SetData(new[] { description.AtomTypes.Count });
            meshBondCountInputBuffer.SetData(new[] { description.Bonds.Count });
            atomTypeInputBuffer.SetData(description.AtomTypes.ToArray());
            atomPositionInputBuffer.SetData(description.AtomPositions.ToArray());
            SmilesMeshBondIndex[] bonds = new SmilesMeshBondIndex[Mathf.Max(1, description.Bonds.Count)];
            for (int i = 0; i < description.Bonds.Count; i++)
                bonds[i] = new SmilesMeshBondIndex { AtomA = description.Bonds[i].AtomA, AtomB = description.Bonds[i].AtomB };
            bondInputBuffer.SetData(bonds);

            int verticesPerAtom = (config.sphereSegments + 1) * (config.sphereSegments + 1);
            int verticesPerBond = 2 * (config.cylinderSegments + 1);
            int indicesPerAtom = config.sphereSegments * config.sphereSegments * 6;
            int indicesPerBond = config.cylinderSegments * 6;

            int kernelId = ballStickCS.FindKernel("CSGenerateBallStickMesh");
            ballStickCS.SetInt("selectedCount", 1);
            ballStickCS.SetInt("sphereSegments", config.sphereSegments);
            ballStickCS.SetInt("cylinderSegments", config.cylinderSegments);
            ballStickCS.SetFloat("atomRadius", config.atomRadius);
            ballStickCS.SetFloat("bondRadius", config.bondRadius);
            ballStickCS.SetInt("maxBondCount", maxBondLimit);
            ballStickCS.SetInt("vertexCapacity", maxVertexCount);
            ballStickCS.SetBuffer(kernelId, "meshAtomStartBuffer", meshAtomStartBuffer);
            ballStickCS.SetBuffer(kernelId, "meshAtomCountInputBuffer", meshAtomCountInputBuffer);
            ballStickCS.SetBuffer(kernelId, "meshBondStartBuffer", meshBondStartBuffer);
            ballStickCS.SetBuffer(kernelId, "meshBondCountInputBuffer", meshBondCountInputBuffer);
            ballStickCS.SetBuffer(kernelId, "atomTypeInputBuffer", atomTypeInputBuffer);
            ballStickCS.SetBuffer(kernelId, "atomPositionInputBuffer", atomPositionInputBuffer);
            ballStickCS.SetBuffer(kernelId, "bondInputBuffer", bondInputBuffer);
            ballStickCS.SetBuffer(kernelId, "vertexPosNormalBuffer", vertexBufferPosition);
            ballStickCS.SetBuffer(kernelId, "vertexOutputBuffer_color", vertexBufferColor);
            ballStickCS.SetBuffer(kernelId, "indexOutputBuffer", indexBuffer);
            ballStickCS.Dispatch(kernelId, 1, 1, 1);

            int[] atomCounts = (await AsyncGPUReadback.RequestAsync(meshAtomCountInputBuffer)).GetData<int>().ToArray();
            int[] bondCounts = (await AsyncGPUReadback.RequestAsync(meshBondCountInputBuffer)).GetData<int>().ToArray();
            if (atomCounts.Length == 0 || atomCounts[0] <= 1)
                return null;

            Vector3[] allPosNormals = (await AsyncGPUReadback.RequestAsync(vertexBufferPosition)).GetData<Vector3>().ToArray();
            Vector4[] allColors = (await AsyncGPUReadback.RequestAsync(vertexBufferColor)).GetData<Vector4>().ToArray();
            int[] allIndices = (await AsyncGPUReadback.RequestAsync(indexBuffer)).GetData<int>().ToArray();

            int atomCount = atomCounts[0];
            int bondCount = bondCounts[0];
            int totalVertices = atomCount * verticesPerAtom + bondCount * verticesPerBond;
            int totalIndices = atomCount * indicesPerAtom + bondCount * indicesPerBond;
            Mesh mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            Vector3[] positions = new Vector3[totalVertices];
            Color[] colors = new Color[totalVertices];
            int[] triangles = new int[totalIndices];

            unsafe
            {
                Vector4* src = (Vector4*)UnsafeUtility.AddressOf<Vector4>(ref allColors[0]);
                Color* dest = (Color*)UnsafeUtility.AddressOf<Color>(ref colors[0]);
                UnsafeUtility.MemCpy(dest, src, totalVertices * UnsafeUtility.SizeOf<Vector4>());
            }
            Array.Copy(allPosNormals, 0, positions, 0, totalVertices);
            Array.Copy(allIndices, 0, triangles, 0, totalIndices);
            mesh.vertices = positions;
            mesh.colors = colors;
            mesh.triangles = triangles;
            if (allPosNormals.Length >= maxVertexCount + totalVertices)
            {
                Vector3[] normals = new Vector3[totalVertices];
                Array.Copy(allPosNormals, maxVertexCount, normals, 0, totalVertices);
                mesh.normals = normals;
            }
            else
            {
                mesh.RecalculateNormals();
            }
            mesh.RecalculateBounds();
            return mesh;
        }

        void OnDestroy()
        {
            vertexBufferPosition?.Release();
            vertexBufferColor?.Release();
            indexBuffer?.Release();
            atomCountBuffer?.Release();
            bondCountBuffer?.Release();
            ReleaseInputBuffers();
            dummySmilesInputBuffer?.Release();
#if SMILES_GRAPH_DEBUG
            dummyGraphDebugBuffer?.Release();
#endif
            if (dummySmilesInputTexture != null)
                Destroy(dummySmilesInputTexture);
        }
    }

}
