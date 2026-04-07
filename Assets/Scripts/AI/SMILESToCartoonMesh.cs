using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Rendering;

namespace AIDrugDiscovery
{

    
    // Configures the Cartoon renderer used for tube-like ligand previews.
    [System.Serializable]
    public class CartoonConfig
    {
        public float bondLength = 1.5f;    
        public float atomRadius = 0.3f;    
        public float bondRadius = 0.3f;    
        public int sphereSegments = 6;     
        public int cylinderSegments = 6;   
        public int topK = 10;              
    }
    // Expands a precomputed atom/bond graph into Cartoon mesh buffers.
    public class SMILESToCartoonMesh : MonoBehaviour
    {
        public ComputeShader cartoonCS;
        public CartoonConfig config;
        
        public int batchSize = 128;              
        public int smilesMaxLength = 256;  
        public int maxAtomLimit = 60;
        public int maxExtraBondCount = 12;
        public bool useSelectedSubsetDispatch = true;
        public bool useLegacySmilesTextureInput = false;

        private ComputeBuffer vertexBufferPosition;
        private ComputeBuffer vertexBufferNormal;
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
            vertexBufferNormal?.Release();
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
            vertexBufferNormal = new ComputeBuffer(maxVertexCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3)));
            vertexBufferColor = new ComputeBuffer(maxVertexCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector4)));
            indexBuffer = new ComputeBuffer(maxIndexCount, sizeof(int));
            atomCountBuffer = new ComputeBuffer(allocatedBatchSize, sizeof(int));
            bondCountBuffer = new ComputeBuffer(allocatedBatchSize, sizeof(int));
        }

        private void ReleaseInputBuffers()
        {
            meshAtomStartBuffer?.Release();
            meshAtomCountInputBuffer?.Release();
            meshBondStartBuffer?.Release();
            meshBondCountInputBuffer?.Release();
            atomTypeInputBuffer?.Release();
            atomPositionInputBuffer?.Release();
            bondInputBuffer?.Release();
            selectedIndexBuffer?.Release();
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
        public async UniTask<List<Mesh>> GenerateCartoonMeshes(List<int> filteredIndices, ComputeBuffer smilesBuffer, int runtimeBatchSize, Texture legacySmilesTexture = null)
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
            int kernelGraph = cartoonCS.FindKernel("CSBuildCartoonGraphBatch");
            int kernelLayout = cartoonCS.FindKernel("CSBuildCartoonLayoutBatch");
            int kernelMesh = cartoonCS.FindKernel("CSGenerateCartoonMesh");

            foreach (int kernelId in new[] { kernelGraph, kernelLayout, kernelMesh })
            {
                cartoonCS.SetInt("batchSize", runtimeBatchSize);
                cartoonCS.SetInt("selectedCount", generatedMeshCount);
                cartoonCS.SetInt("useSmilesTextureInput", useLegacySmilesTextureInput && legacySmilesTexture != null ? 1 : 0);
                cartoonCS.SetInt("smilesMaxLength", smilesMaxLength);
                cartoonCS.SetInt("sphereSegments", config.sphereSegments);
                cartoonCS.SetInt("cylinderSegments", config.cylinderSegments);
                cartoonCS.SetFloat("bondLength", config.bondLength);
                cartoonCS.SetFloat("bondRadius", config.bondRadius);
                cartoonCS.SetInt("maxBondCount", maxBondLimit);
                cartoonCS.SetBuffer(kernelId, "smilesInputBuffer", smilesBuffer ?? dummySmilesInputBuffer);
                cartoonCS.SetTexture(kernelId, "smilesInputTexture", legacySmilesTexture ?? dummySmilesInputTexture);
                cartoonCS.SetBuffer(kernelId, "selectedMolIndexBuffer", selectedIndexBuffer);
                cartoonCS.SetBuffer(kernelId, "meshAtomStartBuffer", meshAtomStartBuffer);
                cartoonCS.SetBuffer(kernelId, "meshAtomCountInputBuffer", meshAtomCountInputBuffer);
                cartoonCS.SetBuffer(kernelId, "meshBondStartBuffer", meshBondStartBuffer);
                cartoonCS.SetBuffer(kernelId, "meshBondCountInputBuffer", meshBondCountInputBuffer);
                cartoonCS.SetBuffer(kernelId, "atomTypeInputBuffer", atomTypeInputBuffer);
                cartoonCS.SetBuffer(kernelId, "atomPositionInputBuffer", atomPositionInputBuffer);
                cartoonCS.SetBuffer(kernelId, "bondInputBuffer", bondInputBuffer);
                cartoonCS.SetBuffer(kernelId, "atomCountOutputBuffer", atomCountBuffer);
                cartoonCS.SetBuffer(kernelId, "bondCountOutputBuffer", bondCountBuffer);
            }

            cartoonCS.SetBuffer(kernelMesh, "vertexOutputBuffer_position", vertexBufferPosition);
            cartoonCS.SetBuffer(kernelMesh, "vertexOutputBuffer_normal", vertexBufferNormal);
            cartoonCS.SetBuffer(kernelMesh, "vertexOutputBuffer_color", vertexBufferColor);
            cartoonCS.SetBuffer(kernelMesh, "indexOutputBuffer", indexBuffer);

            cartoonCS.Dispatch(kernelGraph, threadGroupX, 1, 1);
            cartoonCS.Dispatch(kernelLayout, threadGroupX, 1, 1);
            cartoonCS.Dispatch(kernelMesh, threadGroupX, 1, 1);

            
            int[] atomCounts = new int[generatedMeshCount];
            int[] bondCounts = new int[generatedMeshCount];
            {
                var req = await AsyncGPUReadback.RequestAsync(atomCountBuffer);
                atomCounts = req.GetData<int>().ToArray();
            }
            {
                var req = await AsyncGPUReadback.RequestAsync(bondCountBuffer);
                bondCounts = req.GetData<int>().ToArray();
            }
            //atomCountBuffer.GetData(atomCounts);

            
            Vector3[] allPositions = new Vector3[maxVertexCount];
            Vector3[] allNormals = new Vector3[maxVertexCount];
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
                var req = await AsyncGPUReadback.RequestAsync(vertexBufferNormal);
                allNormals = req.GetData<Vector3>().ToArray();
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
                Array.Copy(allNormals, vertexOffset, normals, 0, totalVertices);
                Array.Copy(allIndices, indexOffset, triangles, 0, totalIndices);

                mesh.vertices = positions;
                mesh.normals = normals;
                mesh.colors = colors;
                mesh.triangles = triangles;
                mesh.RecalculateBounds();
                molMeshes.Add(mesh);
            }

            return molMeshes;
        }

        public async UniTask<Mesh> GenerateSingleCartoonMesh(string smiles)
        {
            if (string.IsNullOrEmpty(smiles))
                return null;

            return await GenerateSingleCartoonMesh(BuildSmilesData(smiles));
        }

        public async UniTask<Mesh> GenerateSingleCartoonMesh(int[] smilesData)
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

            int kernelId = cartoonCS.FindKernel("CSGenerateCartoonMesh");
            cartoonCS.SetInt("selectedCount", 1);
            cartoonCS.SetInt("sphereSegments", config.sphereSegments);
            cartoonCS.SetInt("cylinderSegments", config.cylinderSegments);
            cartoonCS.SetFloat("bondRadius", config.bondRadius);
            cartoonCS.SetInt("maxBondCount", maxBondLimit);
            cartoonCS.SetBuffer(kernelId, "meshAtomStartBuffer", meshAtomStartBuffer);
            cartoonCS.SetBuffer(kernelId, "meshAtomCountInputBuffer", meshAtomCountInputBuffer);
            cartoonCS.SetBuffer(kernelId, "meshBondStartBuffer", meshBondStartBuffer);
            cartoonCS.SetBuffer(kernelId, "meshBondCountInputBuffer", meshBondCountInputBuffer);
            cartoonCS.SetBuffer(kernelId, "atomTypeInputBuffer", atomTypeInputBuffer);
            cartoonCS.SetBuffer(kernelId, "atomPositionInputBuffer", atomPositionInputBuffer);
            cartoonCS.SetBuffer(kernelId, "bondInputBuffer", bondInputBuffer);
            cartoonCS.SetBuffer(kernelId, "vertexOutputBuffer_position", vertexBufferPosition);
            cartoonCS.SetBuffer(kernelId, "vertexOutputBuffer_normal", vertexBufferNormal);
            cartoonCS.SetBuffer(kernelId, "vertexOutputBuffer_color", vertexBufferColor);
            cartoonCS.SetBuffer(kernelId, "indexOutputBuffer", indexBuffer);
            cartoonCS.SetBuffer(kernelId, "atomCountOutputBuffer", atomCountBuffer);
            cartoonCS.SetBuffer(kernelId, "bondCountOutputBuffer", bondCountBuffer);
            cartoonCS.Dispatch(kernelId, 1, 1, 1);

            int[] atomCounts = (await AsyncGPUReadback.RequestAsync(atomCountBuffer)).GetData<int>().ToArray();
            int[] bondCounts = (await AsyncGPUReadback.RequestAsync(bondCountBuffer)).GetData<int>().ToArray();
            if (atomCounts.Length == 0 || atomCounts[0] <= 1)
                return null;

            Vector3[] allPositions = (await AsyncGPUReadback.RequestAsync(vertexBufferPosition)).GetData<Vector3>().ToArray();
            Vector3[] allNormals = (await AsyncGPUReadback.RequestAsync(vertexBufferNormal)).GetData<Vector3>().ToArray();
            Vector4[] allColors = (await AsyncGPUReadback.RequestAsync(vertexBufferColor)).GetData<Vector4>().ToArray();
            int[] allIndices = (await AsyncGPUReadback.RequestAsync(indexBuffer)).GetData<int>().ToArray();

            int atomCount = atomCounts[0];
            int bondCount = bondCounts[0];
            int totalVertices = atomCount * verticesPerAtom + bondCount * verticesPerBond;
            int totalIndices = atomCount * indicesPerAtom + bondCount * indicesPerBond;
            Mesh mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            Vector3[] positions = new Vector3[totalVertices];
            Vector3[] normals = new Vector3[totalVertices];
            Color[] colors = new Color[totalVertices];
            int[] triangles = new int[totalIndices];

            unsafe
            {
                Vector4* src = (Vector4*)UnsafeUtility.AddressOf<Vector4>(ref allColors[0]);
                Color* dest = (Color*)UnsafeUtility.AddressOf<Color>(ref colors[0]);
                UnsafeUtility.MemCpy(dest, src, totalVertices * UnsafeUtility.SizeOf<Vector4>());
            }
            Array.Copy(allPositions, 0, positions, 0, totalVertices);
            Array.Copy(allNormals, 0, normals, 0, totalVertices);
            Array.Copy(allIndices, 0, triangles, 0, totalIndices);
            mesh.vertices = positions;
            mesh.normals = normals;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        void OnDestroy()
        {
            vertexBufferPosition?.Release();
            vertexBufferNormal?.Release();
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
