using UnityEngine;
using System;
using Cysharp.Threading.Tasks;
using System.Collections;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace AIDrugDiscovery
{
    public class MorganFPGenerator : MonoBehaviour
    {
        [Header("Settings")]
        public ComputeShader morganFPComputeShader; 
        public int smilesMaxLength = 256;           
        public int morganRadius = 2;                
        public bool usePackedFpReadback = false;
        public bool useLegacySmilesTextureInput = true;
        public bool useGraphTopologyMorgan = true;
        public int graphKernelChunkSize = 8;
        private const int FP_SIZE = 512;            
        private const int FP_PACKED_WORDS = FP_SIZE / 32;
        private const int MAX_ATOM_COUNT = 60;
        private const int MAX_GRAPH_NEIGHBORS = 6;

        private uint[] allPackedFP = null;
        private uint[] allLegacyFP = null;

        private sealed class SmilesGraph
        {
            public readonly List<int> AtomTypes = new List<int>(MAX_ATOM_COUNT);
            public readonly List<List<(int neighbor, int bondType)>> Adjacency = new List<List<(int, int)>>(MAX_ATOM_COUNT);
        }

        private Texture2D CreateDummyTexture()
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.clear);
            texture.Apply();
            return texture;
        }

        private static int BondCharToType(char c)
        {
            return c switch
            {
                '-' => 0,
                '=' => 1,
                '#' => 2,
                ':' => 3,
                _ => 4
            };
        }

        private static bool IsAromaticAtomType(int atomType)
        {
            return atomType == 66 || atomType == 77 || atomType == 88 || atomType == 166 || atomType == 155;
        }

        private static bool TryParseAtomToken(string smiles, int idx, out int atomType, out int consumedChars)
        {
            atomType = 0;
            consumedChars = 0;
            if (idx < 0 || idx >= smiles.Length)
                return false;

            char c = smiles[idx];
            char c2 = idx + 1 < smiles.Length ? smiles[idx + 1] : '\0';
            switch (c)
            {
                case 'C':
                    if (c2 == 'l') { atomType = 17; consumedChars = 2; return true; }
                    atomType = 6; consumedChars = 1; return true;
                case 'c': atomType = 66; consumedChars = 1; return true;
                case 'N': atomType = 7; consumedChars = 1; return true;
                case 'n': atomType = 77; consumedChars = 1; return true;
                case 'O': atomType = 8; consumedChars = 1; return true;
                case 'o': atomType = 88; consumedChars = 1; return true;
                case 'S':
                    if (c2 == 'i') { atomType = 14; consumedChars = 2; return true; }
                    if (c2 == 'e') { atomType = 34; consumedChars = 2; return true; }
                    atomType = 16; consumedChars = 1; return true;
                case 's': atomType = 166; consumedChars = 1; return true;
                case 'P': atomType = 15; consumedChars = 1; return true;
                case 'p': atomType = 155; consumedChars = 1; return true;
                case 'F': atomType = 9; consumedChars = 1; return true;
                case 'B':
                    if (c2 == 'r') { atomType = 35; consumedChars = 2; return true; }
                    atomType = 5; consumedChars = 1; return true;
                case 'I': atomType = 53; consumedChars = 1; return true;
                case 'H': atomType = 1; consumedChars = 1; return true;
                case 'A':
                    if (c2 == 's') { atomType = 33; consumedChars = 2; return true; }
                    break;
            }

            return false;
        }

        private static SmilesGraph BuildSmilesGraph(string smiles)
        {
            SmilesGraph graph = new SmilesGraph();
            Dictionary<int, (int atomIndex, int bondType)> ringOpeners = new Dictionary<int, (int, int)>();
            Stack<int> branchStack = new Stack<int>();

            int currentAtom = -1;
            int pendingBondType = 4;

            for (int i = 0; i < smiles.Length && graph.AtomTypes.Count < MAX_ATOM_COUNT; i++)
            {
                char c = smiles[i];
                if (char.IsWhiteSpace(c))
                    continue;

                if (c == '-' || c == '=' || c == '#' || c == ':')
                {
                    pendingBondType = BondCharToType(c);
                    continue;
                }

                if (c == '(')
                {
                    if (currentAtom >= 0)
                        branchStack.Push(currentAtom);
                    continue;
                }

                if (c == ')')
                {
                    if (branchStack.Count > 0)
                        currentAtom = branchStack.Pop();
                    continue;
                }

                if (char.IsDigit(c))
                {
                    int ringNumber = c - '0';
                    if (currentAtom >= 0)
                    {
                        if (!ringOpeners.TryGetValue(ringNumber, out var start))
                        {
                            ringOpeners[ringNumber] = (currentAtom, pendingBondType);
                        }
                        else
                        {
                            int bondType = pendingBondType != 4 ? pendingBondType : start.bondType;
                            if (bondType == 4)
                            {
                                bool aromaticPair = IsAromaticAtomType(graph.AtomTypes[currentAtom]) && IsAromaticAtomType(graph.AtomTypes[start.atomIndex]);
                                bondType = aromaticPair ? 3 : 0;
                            }
                            AddUndirectedEdge(graph, currentAtom, start.atomIndex, bondType);
                            ringOpeners.Remove(ringNumber);
                        }
                    }
                    pendingBondType = 4;
                    continue;
                }

                if (c == '[' || c == ']' || c == '/' || c == '\\' || c == '+' || c == '%' || c == '@' || c == '$')
                    continue;

                if (!TryParseAtomToken(smiles, i, out int atomType, out int consumedChars))
                    continue;

                int newAtom = graph.AtomTypes.Count;
                graph.AtomTypes.Add(atomType);
                graph.Adjacency.Add(new List<(int, int)>(MAX_GRAPH_NEIGHBORS));

                if (currentAtom >= 0)
                {
                    int bondType = pendingBondType;
                    if (bondType == 4)
                    {
                        bool aromaticPair = IsAromaticAtomType(graph.AtomTypes[currentAtom]) && IsAromaticAtomType(atomType);
                        bondType = aromaticPair ? 3 : 0;
                    }
                    AddUndirectedEdge(graph, currentAtom, newAtom, bondType);
                }

                currentAtom = newAtom;
                pendingBondType = 4;
                i += consumedChars - 1;
            }

            return graph;
        }

        private static void AddUndirectedEdge(SmilesGraph graph, int a, int b, int bondType)
        {
            if (a < 0 || b < 0 || a >= graph.Adjacency.Count || b >= graph.Adjacency.Count)
                return;

            bool exists = false;
            for (int i = 0; i < graph.Adjacency[a].Count; i++)
            {
                if (graph.Adjacency[a][i].neighbor == b)
                {
                    exists = true;
                    break;
                }
            }
            if (exists)
                return;

            graph.Adjacency[a].Add((b, bondType));
            graph.Adjacency[b].Add((a, bondType));
        }
        public async UniTask Generate512BitFP(ComputeBuffer smilesBuffer, int batchSize, Texture legacySmilesTexture = null, IReadOnlyList<string> generatedSmiles = null)
        {
            
            if ((smilesBuffer == null && !(useLegacySmilesTextureInput && legacySmilesTexture != null)) || batchSize <= 0)
            {
                Debug.LogError("Morgan fingerprint generation status");
                return;
            }

            
            int fpElementCount = usePackedFpReadback ? FP_PACKED_WORDS : FP_SIZE;
            int fpBufferCount = batchSize * fpElementCount;
            ComputeBuffer fpBuffer = new ComputeBuffer(fpBufferCount, sizeof(uint));

            
            uint[] initFP = new uint[fpBufferCount];
            Array.Fill(initFP, 0u);
            fpBuffer.SetData(initFP);

            
            bool useLegacyKernel = !useGraphTopologyMorgan || (useLegacySmilesTextureInput && legacySmilesTexture != null);
            int kernelId = morganFPComputeShader.FindKernel(useLegacyKernel ? "CSGenerateMorganFPLegacy" : "CSGenerateMorganFP");
            morganFPComputeShader.SetInt("batchSize", batchSize);
            morganFPComputeShader.SetInt("smilesMaxLength", smilesMaxLength);
            morganFPComputeShader.SetInt("morganRadius", morganRadius);
            morganFPComputeShader.SetInt("packOutput", usePackedFpReadback ? 1 : 0);
            morganFPComputeShader.SetInt("useSmilesTextureInput", useLegacySmilesTextureInput && legacySmilesTexture != null ? 1 : 0);

            Texture boundTexture = legacySmilesTexture ?? CreateDummyTexture();
            bool disposeDummyTexture = legacySmilesTexture == null;
            ComputeBuffer boundBuffer = smilesBuffer ?? new ComputeBuffer(1, sizeof(int));
            bool disposeDummyBuffer = smilesBuffer == null;
            ComputeBuffer graphAtomCountBuffer = null;
            ComputeBuffer graphAtomTypeBuffer = null;
            ComputeBuffer graphDegreeBuffer = null;
            ComputeBuffer graphNeighborIndexBuffer = null;
            ComputeBuffer graphNeighborBondTypeBuffer = null;

            
            morganFPComputeShader.SetBuffer(kernelId, "smilesInputBuffer", boundBuffer);
            morganFPComputeShader.SetTexture(kernelId, "smilesInputTexture", boundTexture);
            morganFPComputeShader.SetBuffer(kernelId, "fpOutputBuffer", fpBuffer);

            ComputeBuffer dummyIntBuffer = new ComputeBuffer(1, sizeof(int));
            if (!useLegacyKernel)
            {
                BuildGraphBuffers(generatedSmiles, batchSize, out graphAtomCountBuffer, out graphAtomTypeBuffer, out graphDegreeBuffer, out graphNeighborIndexBuffer, out graphNeighborBondTypeBuffer);
                morganFPComputeShader.SetInt("graphMaxNeighbors", MAX_GRAPH_NEIGHBORS);
                morganFPComputeShader.SetBuffer(kernelId, "graphAtomCountBuffer", graphAtomCountBuffer);
                morganFPComputeShader.SetBuffer(kernelId, "graphAtomTypeBuffer", graphAtomTypeBuffer);
                morganFPComputeShader.SetBuffer(kernelId, "graphDegreeBuffer", graphDegreeBuffer);
                morganFPComputeShader.SetBuffer(kernelId, "graphNeighborIndexBuffer", graphNeighborIndexBuffer);
                morganFPComputeShader.SetBuffer(kernelId, "graphNeighborBondTypeBuffer", graphNeighborBondTypeBuffer);
            }
            else
            {
                dummyIntBuffer.SetData(new[] { 0 });
                morganFPComputeShader.SetInt("graphMaxNeighbors", MAX_GRAPH_NEIGHBORS);
                morganFPComputeShader.SetBuffer(kernelId, "graphAtomCountBuffer", dummyIntBuffer);
                morganFPComputeShader.SetBuffer(kernelId, "graphAtomTypeBuffer", dummyIntBuffer);
                morganFPComputeShader.SetBuffer(kernelId, "graphDegreeBuffer", dummyIntBuffer);
                morganFPComputeShader.SetBuffer(kernelId, "graphNeighborIndexBuffer", dummyIntBuffer);
                morganFPComputeShader.SetBuffer(kernelId, "graphNeighborBondTypeBuffer", dummyIntBuffer);
                graphAtomCountBuffer = dummyIntBuffer;
                graphAtomTypeBuffer = dummyIntBuffer;
                graphDegreeBuffer = dummyIntBuffer;
                graphNeighborIndexBuffer = dummyIntBuffer;
                graphNeighborBondTypeBuffer = dummyIntBuffer;
            }

            
            if (useLegacyKernel)
            {
                morganFPComputeShader.SetInt("moleculeOffset", 0);
                morganFPComputeShader.SetInt("currentBatchSize", batchSize);
                int threadGroupX = Mathf.CeilToInt(batchSize / 32f);
                morganFPComputeShader.Dispatch(kernelId, threadGroupX, 1, 1);
            }
            else
            {
                int chunkSize = Mathf.Max(1, graphKernelChunkSize);
                for (int offset = 0; offset < batchSize; offset += chunkSize)
                {
                    int currentChunkSize = Mathf.Min(chunkSize, batchSize - offset);
                    morganFPComputeShader.SetInt("moleculeOffset", offset);
                    morganFPComputeShader.SetInt("currentBatchSize", currentChunkSize);
                    int threadGroupX = Mathf.CeilToInt(currentChunkSize / 32f);
                    morganFPComputeShader.Dispatch(kernelId, threadGroupX, 1, 1);
                }
            }

            
            //ComputeShader.SyncThread();

            //fpBuffer.GetData(allFP);
            var req = await AsyncGPUReadback.RequestAsync(fpBuffer);
            if (usePackedFpReadback)
            {
                allPackedFP = req.GetData<uint>().ToArray();
                allLegacyFP = null;
            }
            else
            {
                allLegacyFP = req.GetData<uint>().ToArray();
                allPackedFP = null;
            }
            fpBuffer.Dispose();
            graphAtomCountBuffer?.Dispose();
            if (!ReferenceEquals(graphAtomTypeBuffer, graphAtomCountBuffer)) graphAtomTypeBuffer?.Dispose();
            if (!ReferenceEquals(graphDegreeBuffer, graphAtomCountBuffer) && !ReferenceEquals(graphDegreeBuffer, graphAtomTypeBuffer)) graphDegreeBuffer?.Dispose();
            if (!ReferenceEquals(graphNeighborIndexBuffer, graphAtomCountBuffer) && !ReferenceEquals(graphNeighborIndexBuffer, graphAtomTypeBuffer) && !ReferenceEquals(graphNeighborIndexBuffer, graphDegreeBuffer)) graphNeighborIndexBuffer?.Dispose();
            if (!ReferenceEquals(graphNeighborBondTypeBuffer, graphAtomCountBuffer) && !ReferenceEquals(graphNeighborBondTypeBuffer, graphAtomTypeBuffer) && !ReferenceEquals(graphNeighborBondTypeBuffer, graphDegreeBuffer) && !ReferenceEquals(graphNeighborBondTypeBuffer, graphNeighborIndexBuffer)) graphNeighborBondTypeBuffer?.Dispose();
            if (disposeDummyBuffer)
                boundBuffer.Dispose();
            if (disposeDummyTexture)
                Destroy(boundTexture);
            dummyIntBuffer.Dispose();

            Debug.Log($"Morgan fingerprint generation status");
            //return fpBuffer;
        }
        public BitArray GetFPFromBuffer(int molIdx)
        {
            if (usePackedFpReadback)
            {
                if (allPackedFP == null || molIdx >= allPackedFP.Length / FP_PACKED_WORDS)
                {
                    Debug.LogError("Morgan fingerprint generation status");
                    return null;
                }
            }
            else if (allLegacyFP == null || molIdx >= allLegacyFP.Length / FP_SIZE)
            {
                Debug.LogError("Morgan fingerprint generation status");
                return null;
            }

            BitArray bits = new BitArray(FP_SIZE);
            if (usePackedFpReadback)
            {
                int wordBase = molIdx * FP_PACKED_WORDS;
                for (int wordIdx = 0; wordIdx < FP_PACKED_WORDS; wordIdx++)
                {
                    uint word = allPackedFP[wordBase + wordIdx];
                    int bitBase = wordIdx * 32;
                    for (int bit = 0; bit < 32; bit++)
                    {
                        bits.Set(bitBase + bit, (word & (1u << bit)) != 0u);
                    }
                }
            }
            else
            {
                int bitBase = molIdx * FP_SIZE;
                for (int i = 0; i < FP_SIZE; i++)
                    bits.Set(i, allLegacyFP[bitBase + i] != 0u);
            }

            return bits;
        }

        private void BuildGraphBuffers(
            IReadOnlyList<string> generatedSmiles,
            int batchSize,
            out ComputeBuffer atomCountBuffer,
            out ComputeBuffer atomTypeBuffer,
            out ComputeBuffer degreeBuffer,
            out ComputeBuffer neighborIndexBuffer,
            out ComputeBuffer neighborBondTypeBuffer)
        {
            if (generatedSmiles == null)
                throw new ArgumentNullException(nameof(generatedSmiles), "Graph topology Morgan requires the SMILES string list for the current batch.");

            int[] atomCounts = new int[batchSize];
            int[] atomTypes = new int[batchSize * MAX_ATOM_COUNT];
            int[] degrees = new int[batchSize * MAX_ATOM_COUNT];
            int[] neighborIndices = new int[batchSize * MAX_ATOM_COUNT * MAX_GRAPH_NEIGHBORS];
            int[] neighborBondTypes = new int[batchSize * MAX_ATOM_COUNT * MAX_GRAPH_NEIGHBORS];
            Array.Fill(neighborIndices, -1);
            Array.Fill(neighborBondTypes, 4);

            for (int molIdx = 0; molIdx < batchSize; molIdx++)
            {
                string smiles = molIdx < generatedSmiles.Count ? generatedSmiles[molIdx] : null;
                if (string.IsNullOrEmpty(smiles))
                    continue;

                SmilesGraph graph = BuildSmilesGraph(smiles);
                atomCounts[molIdx] = graph.AtomTypes.Count;
                int atomBase = molIdx * MAX_ATOM_COUNT;

                for (int atomIdx = 0; atomIdx < graph.AtomTypes.Count && atomIdx < MAX_ATOM_COUNT; atomIdx++)
                {
                    atomTypes[atomBase + atomIdx] = graph.AtomTypes[atomIdx];
                    int degree = Mathf.Min(graph.Adjacency[atomIdx].Count, MAX_GRAPH_NEIGHBORS);
                    degrees[atomBase + atomIdx] = degree;
                    int neighborBase = (atomBase + atomIdx) * MAX_GRAPH_NEIGHBORS;
                    for (int n = 0; n < degree; n++)
                    {
                        neighborIndices[neighborBase + n] = graph.Adjacency[atomIdx][n].neighbor;
                        neighborBondTypes[neighborBase + n] = graph.Adjacency[atomIdx][n].bondType;
                    }
                }
            }

            atomCountBuffer = new ComputeBuffer(batchSize, sizeof(int));
            atomTypeBuffer = new ComputeBuffer(batchSize * MAX_ATOM_COUNT, sizeof(int));
            degreeBuffer = new ComputeBuffer(batchSize * MAX_ATOM_COUNT, sizeof(int));
            neighborIndexBuffer = new ComputeBuffer(batchSize * MAX_ATOM_COUNT * MAX_GRAPH_NEIGHBORS, sizeof(int));
            neighborBondTypeBuffer = new ComputeBuffer(batchSize * MAX_ATOM_COUNT * MAX_GRAPH_NEIGHBORS, sizeof(int));
            atomCountBuffer.SetData(atomCounts);
            atomTypeBuffer.SetData(atomTypes);
            degreeBuffer.SetData(degrees);
            neighborIndexBuffer.SetData(neighborIndices);
            neighborBondTypeBuffer.SetData(neighborBondTypes);
        }


        private void OnDestroy()
        {
            
            morganFPComputeShader = null;
        }
    }

}
