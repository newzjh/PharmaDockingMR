using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Rendering;

namespace AIDrugDiscovery
{

    // ���ģ������
    [System.Serializable]
    public class BallStickConfig
    {
        public float bondLength = 1.5f;    // ԭ�Ӽ��׼����
        public float atomRadius = 0.3f;    // ԭ����뾶
        public float bondRadius = 0.1f;    // ��ѧ��Բ���뾶
        public int sphereSegments = 12;    // ԭ����ֶ���
        public int cylinderSegments = 8;   // ��ѧ��Բ���ֶ���
        public int topK = 10;              // ����Top-Kɸѡ����ӵ�Mesh
    }



    public class SMILESToBallStickMesh : MonoBehaviour
    {
        public ComputeShader ballStickCS;
        public BallStickConfig config;
        //public ComputeBuffer smilesBuffer; // �����SMILES Buffer
        public int batchSize = 128;              // �������δ�С
        public int smilesMaxLength = 256;  // ����SMILES��󳤶�
        public int maxAtomLimit = 60;
        public int maxExtraBondCount = 12;
        public bool useSelectedSubsetDispatch = true;
        public bool useLegacySmilesTextureInput = false;

        private ComputeBuffer vertexBufferPosition;
        private ComputeBuffer vertexBufferNormal;
        private ComputeBuffer vertexBufferColor;
        private ComputeBuffer indexBuffer;
        private ComputeBuffer atomCountBuffer; // ÿ�����ӵ�ԭ����
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

            // ��ʼ��Buffer
            vertexBufferPosition = new ComputeBuffer(maxVertexCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3)));
            vertexBufferNormal = new ComputeBuffer(maxVertexCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3)));
            vertexBufferColor = new ComputeBuffer(maxVertexCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector4)));
            indexBuffer = new ComputeBuffer(maxIndexCount, sizeof(int));
            atomCountBuffer = new ComputeBuffer(allocatedBatchSize, sizeof(int));
            bondCountBuffer = new ComputeBuffer(allocatedBatchSize, sizeof(int));
        }

        public bool test = true;

        /// <summary>
        /// �������ģ��Mesh
        /// </summary>
        /// <param name="filteredIndices">ɸѡ��ķ��������б�</param>
        public async UniTask<List<Mesh>> GenerateBallStickMeshes(List<int> filteredIndices, ComputeBuffer smilesBuffer, int runtimeBatchSize, Texture legacySmilesTexture = null)
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

            // 1. ����Compute Shader
            int kernelId = ballStickCS.FindKernel("CSGenerateBallStickMesh");
            ballStickCS.SetInt("batchSize", runtimeBatchSize);
            ballStickCS.SetInt("selectedCount", generatedMeshCount);
            ballStickCS.SetInt("useSmilesTextureInput", useLegacySmilesTextureInput && legacySmilesTexture != null ? 1 : 0);
            ballStickCS.SetInt("smilesMaxLength", smilesMaxLength);
            ballStickCS.SetInt("sphereSegments", config.sphereSegments);
            ballStickCS.SetInt("cylinderSegments", config.cylinderSegments);
            ballStickCS.SetFloat("bondLength", config.bondLength);
            ballStickCS.SetFloat("atomRadius", config.atomRadius);
            ballStickCS.SetFloat("bondRadius", config.bondRadius);
            ballStickCS.SetInt("topK", config.topK);
            ballStickCS.SetInt("maxBondCount", maxBondLimit);

            Texture boundTexture = legacySmilesTexture ?? CreateDummyTexture();
            bool disposeDummyTexture = legacySmilesTexture == null;
            ComputeBuffer boundBuffer = smilesBuffer ?? new ComputeBuffer(1, sizeof(int));
            bool disposeDummyBuffer = smilesBuffer == null;

            // 2. ��Buffer
            ballStickCS.SetBuffer(kernelId, "smilesInputBuffer", boundBuffer);
            ballStickCS.SetTexture(kernelId, "smilesInputTexture", boundTexture);
            ballStickCS.SetBuffer(kernelId, "selectedMolIndexBuffer", selectedIndexBuffer);
            ballStickCS.SetBuffer(kernelId, "vertexOutputBuffer_position", vertexBufferPosition);
            ballStickCS.SetBuffer(kernelId, "vertexOutputBuffer_normal", vertexBufferNormal);
            ballStickCS.SetBuffer(kernelId, "vertexOutputBuffer_color", vertexBufferColor);
            ballStickCS.SetBuffer(kernelId, "indexOutputBuffer", indexBuffer);
            ballStickCS.SetBuffer(kernelId, "atomCountOutputBuffer", atomCountBuffer);
            ballStickCS.SetBuffer(kernelId, "bondCountOutputBuffer", bondCountBuffer);

            // 3. ����GPU���㣨�����ƶ���32�߳��飩
            int threadGroupX = Mathf.CeilToInt(generatedMeshCount / 32f);
            ballStickCS.Dispatch(kernelId, threadGroupX, 1, 1);
            //while (test && Application.isPlaying)
            //{
            //    ballStickCS.Dispatch(kernelId, threadGroupX, 1, 1);
            //    await UniTask.NextFrame();
            //}

            // 4. ��ȡԭ����
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

            // 5. ��ȡ�������������
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

            // 6. Ϊÿ��ɸѡ��������Mesh
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

                // ���㵱ǰ���ӵĶ���/��������
                int totalVertices = atomCount * verticesPerAtom + bondCount * verticesPerBond;
                int totalIndices = atomCount * indicesPerAtom + bondCount * indicesPerBond;
                if (vertexOffset + totalVertices > maxVertexCount || indexOffset + totalIndices > maxIndexCount) 
                    break;

                // ���Mesh����
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

                //// ����ƫ����
                //vertexOffset += totalVertices;
                //indexOffset += totalIndices;
            }

            if (disposeDummyBuffer)
                boundBuffer.Dispose();
            if (disposeDummyTexture)
                Destroy(boundTexture);

            return molMeshes;
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
