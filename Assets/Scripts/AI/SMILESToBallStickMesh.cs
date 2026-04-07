using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Rendering;

namespace AIDrugDiscovery
{

    
    // Configures the BallStick renderer used for atom-and-bond previews.
    [System.Serializable]
    public class BallStickConfig
    {
        public float bondLength = 1.5f;    
        public float atomRadius = 0.3f;    
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
        private ComputeBuffer dummySmilesInputBuffer;
        private Texture2D dummySmilesInputTexture;
        private int maxVertexCount;
        private int maxIndexCount;
        private int allocatedBatchSize;
        private int maxBondLimit;

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

            
            vertexBufferPosition = new ComputeBuffer(maxVertexCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3)));
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

            meshAtomStartBuffer.SetData(atomStarts);
            meshAtomCountInputBuffer.SetData(new int[meshCount]);
            meshBondStartBuffer.SetData(bondStarts);
            meshBondCountInputBuffer.SetData(new int[meshCount]);
            atomTypeInputBuffer.SetData(new int[Mathf.Max(1, meshCount * maxAtomLimit)]);
            atomPositionInputBuffer.SetData(new Vector3[Mathf.Max(1, meshCount * maxAtomLimit)]);
            bondInputBuffer.SetData(new SmilesMeshBondIndex[Mathf.Max(1, meshCount * maxBondLimit)]);
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

            foreach (int kernelId in new[] { kernelGraph, kernelLayout, kernelMesh })
            {
                ballStickCS.SetInt("batchSize", runtimeBatchSize);
                ballStickCS.SetInt("selectedCount", generatedMeshCount);
                ballStickCS.SetInt("useSmilesTextureInput", useLegacySmilesTextureInput && legacySmilesTexture != null ? 1 : 0);
                ballStickCS.SetInt("smilesMaxLength", smilesMaxLength);
                ballStickCS.SetInt("sphereSegments", config.sphereSegments);
                ballStickCS.SetInt("cylinderSegments", config.cylinderSegments);
                ballStickCS.SetFloat("bondLength", config.bondLength);
                ballStickCS.SetFloat("bondRadius", config.bondRadius);
                ballStickCS.SetInt("maxBondCount", maxBondLimit);
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
            }

            ballStickCS.SetBuffer(kernelMesh, "vertexOutputBuffer_position", vertexBufferPosition);
            ballStickCS.SetBuffer(kernelMesh, "vertexOutputBuffer_color", vertexBufferColor);
            ballStickCS.SetBuffer(kernelMesh, "indexOutputBuffer", indexBuffer);

            ballStickCS.Dispatch(kernelGraph, threadGroupX, 1, 1);
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

            
            Vector3[] allPositions = new Vector3[maxVertexCount];
            Vector4[] allColors = new Vector4[maxVertexCount];
            int[] allIndices = new int[maxIndexCount];
            //vertexBufferPosition.GetData(allPositions);
            //vertexBufferNormal.GetData(allNormals);
            //vertexBufferColor.GetData(allColors);
            //indexBuffer.GetData(allIndices);
            {
                var req = await AsyncGPUReadback.RequestAsync(vertexBufferPosition);
                allPositions = req.GetData<Vector3>().ToArray();
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
                Vector3[] normals = new Vector3[totalVertices];
                Color[] colors = new Color[totalVertices];
                int[] triangles = new int[totalIndices];

                unsafe
                {
                    Vector4* src = (Vector4*)UnsafeUtility.AddressOf<Vector4>(ref allColors[0]) + vertexOffset;
                    Color* dest = (Color*)UnsafeUtility.AddressOf<Color>(ref colors[0]);
                    UnsafeUtility.MemCpy(dest, src, totalVertices * UnsafeUtility.SizeOf<Vector4>());
                }
                Array.Copy(allPositions, vertexOffset, positions, 0, totalVertices);
                Array.Copy(allIndices, indexOffset, triangles, 0, totalIndices);

                mesh.vertices = positions;
                mesh.colors = colors;
                mesh.triangles = triangles;
                mesh.RecalculateNormals();
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
            ballStickCS.SetFloat("bondRadius", config.bondRadius);
            ballStickCS.SetInt("maxBondCount", maxBondLimit);
            ballStickCS.SetBuffer(kernelId, "meshAtomStartBuffer", meshAtomStartBuffer);
            ballStickCS.SetBuffer(kernelId, "meshAtomCountInputBuffer", meshAtomCountInputBuffer);
            ballStickCS.SetBuffer(kernelId, "meshBondStartBuffer", meshBondStartBuffer);
            ballStickCS.SetBuffer(kernelId, "meshBondCountInputBuffer", meshBondCountInputBuffer);
            ballStickCS.SetBuffer(kernelId, "atomTypeInputBuffer", atomTypeInputBuffer);
            ballStickCS.SetBuffer(kernelId, "atomPositionInputBuffer", atomPositionInputBuffer);
            ballStickCS.SetBuffer(kernelId, "bondInputBuffer", bondInputBuffer);
            ballStickCS.SetBuffer(kernelId, "vertexOutputBuffer_position", vertexBufferPosition);
            ballStickCS.SetBuffer(kernelId, "vertexOutputBuffer_color", vertexBufferColor);
            ballStickCS.SetBuffer(kernelId, "indexOutputBuffer", indexBuffer);
            ballStickCS.Dispatch(kernelId, 1, 1, 1);

            int[] atomCounts = (await AsyncGPUReadback.RequestAsync(meshAtomCountInputBuffer)).GetData<int>().ToArray();
            int[] bondCounts = (await AsyncGPUReadback.RequestAsync(meshBondCountInputBuffer)).GetData<int>().ToArray();
            if (atomCounts.Length == 0 || atomCounts[0] <= 1)
                return null;

            Vector3[] allPositions = (await AsyncGPUReadback.RequestAsync(vertexBufferPosition)).GetData<Vector3>().ToArray();
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
            Array.Copy(allPositions, 0, positions, 0, totalVertices);
            Array.Copy(allIndices, 0, triangles, 0, totalIndices);
            mesh.vertices = positions;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
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
            if (dummySmilesInputTexture != null)
                Destroy(dummySmilesInputTexture);
        }
    }

}
