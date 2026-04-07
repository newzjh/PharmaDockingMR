using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Rendering;

namespace AIDrugDiscovery
{

    // 锟斤拷锟侥ｏ拷锟斤拷锟斤拷锟?
    [System.Serializable]
    public class CartoonConfig
    {
        public float bondLength = 1.5f;    // 原接间距系数
        public float atomRadius = 0.3f;    // 原半�?
        public float bondRadius = 0.3f;    // 化学键圆半径
        public int sphereSegments = 6;     // 原球段数
        public int cylinderSegments = 6;   // 化学键圆段数
        public int topK = 10;              // 保留Top-K筛选后拥有Mesh
    }



    public class SMILESToCartoonMesh : MonoBehaviour
    {
        public ComputeShader cartoonCS;
        public CartoonConfig config;
        //public ComputeBuffer smilesBuffer; // 锟斤拷锟斤拷锟絊MILES Buffer
        public int batchSize = 128;              // 锟斤拷锟斤拷锟斤拷锟轿达拷小
        public int smilesMaxLength = 256;  // 锟斤拷锟斤拷SMILES锟斤拷蟪ざ锟?
        public int maxAtomLimit = 60;
        public int maxExtraBondCount = 12;
        public bool useSelectedSubsetDispatch = true;
        public bool useLegacySmilesTextureInput = false;

        private ComputeBuffer vertexBufferPosition;
        private ComputeBuffer vertexBufferNormal;
        private ComputeBuffer vertexBufferColor;
        private ComputeBuffer indexBuffer;
        private ComputeBuffer atomCountBuffer; // 每锟斤拷锟斤拷锟接碉拷原锟斤拷锟斤拷
        private ComputeBuffer bondCountBuffer;
        private ComputeBuffer selectedIndexBuffer;
        private int maxVertexCount;
        private int maxIndexCount;
        private int allocatedBatchSize;
        private int maxBondLimit;

        private Texture2D CreateDummyTexture()
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.clear);
            texture.Apply();
            return texture;
        }

        public void Awake()
        {
            EnsureBuffers(batchSize);
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
            selectedIndexBuffer?.Release();

            int verticesPerAtom = (config.sphereSegments + 1) * (config.sphereSegments + 1);
            int verticesPerBond = 2 * (config.cylinderSegments + 1);
            int indicesPerAtom = config.sphereSegments * config.sphereSegments * 6;
            int indicesPerBond = config.cylinderSegments * 6;

            allocatedBatchSize = requiredBatchSize;
            maxBondLimit = maxAtomLimit + maxExtraBondCount;
            maxVertexCount = allocatedBatchSize * (maxAtomLimit * verticesPerAtom + maxBondLimit * verticesPerBond);
            maxIndexCount = allocatedBatchSize * (maxAtomLimit * indicesPerAtom + maxBondLimit * indicesPerBond);

            // 锟斤拷始锟斤拷Buffer
            vertexBufferPosition = new ComputeBuffer(maxVertexCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3)));
            vertexBufferNormal = new ComputeBuffer(maxVertexCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3)));
            vertexBufferColor = new ComputeBuffer(maxVertexCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector4)));
            indexBuffer = new ComputeBuffer(maxIndexCount, sizeof(int));
            atomCountBuffer = new ComputeBuffer(allocatedBatchSize, sizeof(int));
            bondCountBuffer = new ComputeBuffer(allocatedBatchSize, sizeof(int));
        }

        public bool test = true;

        /// <summary>
        /// 锟斤拷锟斤拷锟斤拷锟侥ｏ拷锟組esh
        /// </summary>
        /// <param name="filteredIndices">筛选锟斤拷姆锟斤拷锟斤拷锟斤拷锟斤拷斜�?/param>
        public async UniTask<List<Mesh>> GenerateCartoonMeshes(List<int> filteredIndices, ComputeBuffer smilesBuffer, int runtimeBatchSize, Texture legacySmilesTexture = null)
        {
            List<Mesh> molMeshes = new List<Mesh>();
            if ((smilesBuffer == null && !(useLegacySmilesTextureInput && legacySmilesTexture != null)) || runtimeBatchSize <= 0 || filteredIndices == null || filteredIndices.Count == 0)
                return molMeshes;

            int generatedMeshCount = useSelectedSubsetDispatch ? filteredIndices.Count : runtimeBatchSize;
            EnsureBuffers(generatedMeshCount);
            int verticesPerAtom = (config.sphereSegments + 1) * (config.sphereSegments + 1);
            int verticesPerBond = 2 * (config.cylinderSegments + 1);
            int indicesPerAtom = config.sphereSegments * config.sphereSegments * 6;
            int indicesPerBond = config.cylinderSegments * 6;
            selectedIndexBuffer?.Release();
            int[] selectedIndices = new int[generatedMeshCount];
            if (useSelectedSubsetDispatch)
            {
                for (int i = 0; i < filteredIndices.Count; i++)
                    selectedIndices[i] = filteredIndices[i];
            }
            else
            {
                for (int i = 0; i < generatedMeshCount; i++)
                    selectedIndices[i] = i;
            }
            selectedIndexBuffer = new ComputeBuffer(generatedMeshCount, sizeof(int));
            selectedIndexBuffer.SetData(selectedIndices);

            // 1. 锟斤拷锟斤拷Compute Shader
            int kernelId = cartoonCS.FindKernel("CSGenerateCartoonMesh");
            cartoonCS.SetInt("batchSize", runtimeBatchSize);
            cartoonCS.SetInt("selectedCount", generatedMeshCount);
            cartoonCS.SetInt("useSmilesTextureInput", useLegacySmilesTextureInput && legacySmilesTexture != null ? 1 : 0);
            cartoonCS.SetInt("smilesMaxLength", smilesMaxLength);
            cartoonCS.SetInt("sphereSegments", config.sphereSegments);
            cartoonCS.SetInt("cylinderSegments", config.cylinderSegments);
            cartoonCS.SetFloat("bondLength", config.bondLength);
            cartoonCS.SetFloat("atomRadius", config.atomRadius);
            cartoonCS.SetFloat("bondRadius", config.bondRadius);
            cartoonCS.SetInt("topK", config.topK);
            cartoonCS.SetInt("maxBondCount", maxBondLimit);

            Texture boundTexture = legacySmilesTexture ?? CreateDummyTexture();
            bool disposeDummyTexture = legacySmilesTexture == null;
            ComputeBuffer boundBuffer = smilesBuffer ?? new ComputeBuffer(1, sizeof(int));
            bool disposeDummyBuffer = smilesBuffer == null;

            // 2. 锟斤拷Buffer
            cartoonCS.SetBuffer(kernelId, "smilesInputBuffer", boundBuffer);
            cartoonCS.SetTexture(kernelId, "smilesInputTexture", boundTexture);
            cartoonCS.SetBuffer(kernelId, "selectedMolIndexBuffer", selectedIndexBuffer);
            cartoonCS.SetBuffer(kernelId, "vertexOutputBuffer_position", vertexBufferPosition);
            cartoonCS.SetBuffer(kernelId, "vertexOutputBuffer_normal", vertexBufferNormal);
            cartoonCS.SetBuffer(kernelId, "vertexOutputBuffer_color", vertexBufferColor);
            cartoonCS.SetBuffer(kernelId, "indexOutputBuffer", indexBuffer);
            cartoonCS.SetBuffer(kernelId, "atomCountOutputBuffer", atomCountBuffer);
            cartoonCS.SetBuffer(kernelId, "bondCountOutputBuffer", bondCountBuffer);

            // 3. 锟斤拷锟斤拷GPU锟斤拷锟姐（锟斤拷锟斤拷锟狡讹拷锟斤�?2锟竭筹拷锟介�?
            int threadGroupX = Mathf.CeilToInt(generatedMeshCount / 32f);
            cartoonCS.Dispatch(kernelId, threadGroupX, 1, 1);
            //while (test && Application.isPlaying)
            //{
            //    cartoonCS.Dispatch(kernelId, threadGroupX, 1, 1);
            //    await UniTask.NextFrame();
            //}

            // 4. 锟斤拷取原锟斤拷锟斤�?
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

            // 5. 锟斤拷取锟斤拷锟斤拷锟斤拷锟斤拷锟斤拷锟斤拷�?
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

            // 6. 为每锟斤拷筛选锟斤拷锟斤拷锟斤拷锟斤拷Mesh
            int vertexOffset = 0;
            int indexOffset = 0;
            for (int outputIdx = 0; outputIdx < filteredIndices.Count; outputIdx++)
            {
                int meshIdx = useSelectedSubsetDispatch ? outputIdx : filteredIndices[outputIdx];
                if (meshIdx < 0 || meshIdx >= generatedMeshCount)
                    continue;

                int atomCount = atomCounts[meshIdx];
                int bondCount = bondCounts[meshIdx];
                if (atomCount <= 1) 
                    continue;

                vertexOffset = meshIdx * (maxAtomLimit * verticesPerAtom + maxBondLimit * verticesPerBond);
                indexOffset = meshIdx * (maxAtomLimit * indicesPerAtom + maxBondLimit * indicesPerBond);

                // 锟斤拷锟姐当前锟斤拷锟接的讹拷锟斤拷/锟斤拷锟斤拷锟斤拷锟斤拷
                int totalVertices = atomCount * verticesPerAtom + bondCount * verticesPerBond;
                int totalIndices = atomCount * indicesPerAtom + bondCount * indicesPerBond;
                if (vertexOffset + totalVertices > maxVertexCount || indexOffset + totalIndices > maxIndexCount) 
                    break;

                // 锟斤拷锟組esh锟斤拷锟斤拷
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

                //// 锟斤拷锟斤拷偏锟斤拷锟斤�?
                //vertexOffset += totalVertices;
                //indexOffset += totalIndices;
            }

            if (disposeDummyBuffer)
                boundBuffer.Dispose();
            if (disposeDummyTexture)
                Destroy(boundTexture);

            return molMeshes;
        }

        public async UniTask<Mesh> GenerateSingleCartoonMesh(string smiles)
        {
            if (string.IsNullOrEmpty(smiles))
                return null;

            int[] smilesData = new int[smilesMaxLength];
            int copyLength = Mathf.Min(smiles.Length, smilesMaxLength - 1);
            for (int i = 0; i < copyLength; i++)
                smilesData[i] = smiles[i];

            ComputeBuffer singleSmilesBuffer = new ComputeBuffer(1, smilesMaxLength * sizeof(int));
            singleSmilesBuffer.SetData(smilesData);

            Texture2D singleSmilesTexture = null;
            if (useLegacySmilesTextureInput)
            {
                singleSmilesTexture = new Texture2D(smilesMaxLength, 1, TextureFormat.RGBAHalf, false);
                Color[] pixels = new Color[smilesMaxLength];
                for (int i = 0; i < copyLength; i++)
                    pixels[i] = new Color(smilesData[i] / 255f, 0f, 0f, 1f);
                for (int i = copyLength; i < smilesMaxLength; i++)
                    pixels[i] = new Color(0f, 0f, 0f, 1f);
                singleSmilesTexture.SetPixels(pixels);
                singleSmilesTexture.Apply();
            }

            List<Mesh> meshes = await GenerateCartoonMeshes(new List<int> { 0 }, singleSmilesBuffer, 1, singleSmilesTexture);
            singleSmilesBuffer.Dispose();
            if (singleSmilesTexture != null)
                Destroy(singleSmilesTexture);
            return meshes.Count > 0 ? meshes[0] : null;
        }

        void OnDestroy()
        {
            vertexBufferPosition?.Release();
            vertexBufferNormal?.Release();
            vertexBufferColor?.Release();
            indexBuffer?.Release();
            atomCountBuffer?.Release();
            bondCountBuffer?.Release();
            selectedIndexBuffer?.Release();
        }
    }

}
