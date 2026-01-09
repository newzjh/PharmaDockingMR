using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;

namespace AIDrugDiscovery
{

    // 球棍模型配置
    [System.Serializable]
    public class BallStickConfig
    {
        public float bondLength = 1.5f;    // 原子间标准键长
        public float atomRadius = 0.3f;    // 原子球半径
        public float bondRadius = 0.1f;    // 化学键圆柱半径
        public int sphereSegments = 12;    // 原子球分段数
        public int cylinderSegments = 8;   // 化学键圆柱分段数
        public int topK = 10;              // 生成Top-K筛选后分子的Mesh
    }



    public class SMILESToBallStickMesh : MonoBehaviour
    {
        public ComputeShader ballStickCS;
        public BallStickConfig config;
        //public ComputeBuffer smilesBuffer; // 输入的SMILES Buffer
        public int batchSize = 128;              // 分子批次大小
        public int smilesMaxLength = 256;  // 单个SMILES最大长度
        public int maxAtomLimit = 60;

        private ComputeBuffer vertexBufferPosition;
        private ComputeBuffer vertexBufferNormal;
        private ComputeBuffer vertexBufferColor;
        private ComputeBuffer indexBuffer;
        private ComputeBuffer atomCountBuffer; // 每个分子的原子数
        private int maxVertexCount;
        private int maxIndexCount;

        public void Awake()
        {
            // 计算单分子最大顶点/索引数：球(每个原子(seg+1)^2) + 棍(每个键2*seg)
            int verticesPerAtom = (config.sphereSegments + 1) * (config.sphereSegments + 1);
            int verticesPerBond = 2 * (config.cylinderSegments + 1);
            int indicesPerAtom = config.sphereSegments * config.sphereSegments * 6;
            int indicesPerBond = config.cylinderSegments * 6;

            // 假设单分子最多50个原子，49个键
            maxVertexCount = batchSize /** config.topK*/ * (maxAtomLimit * verticesPerAtom + (maxAtomLimit-1) * verticesPerBond);
            maxIndexCount = batchSize /** config.topK*/ * (maxAtomLimit * indicesPerAtom + (maxAtomLimit-1) * indicesPerBond);

            // 初始化Buffer
            vertexBufferPosition = new ComputeBuffer(maxVertexCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3)));
            vertexBufferNormal = new ComputeBuffer(maxVertexCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3)));
            vertexBufferColor = new ComputeBuffer(maxVertexCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector4)));
            indexBuffer = new ComputeBuffer(maxIndexCount, sizeof(int));
            atomCountBuffer = new ComputeBuffer(batchSize, sizeof(int));
        }

        public bool test = true;

        /// <summary>
        /// 生成球棍模型Mesh
        /// </summary>
        /// <param name="filteredIndices">筛选后的分子索引列表</param>
        public async UniTask<List<Mesh>> GenerateBallStickMeshes(List<int> filteredIndices, ComputeBuffer smilesBuffer, RenderTexture smilesTexture)
        {
            List<Mesh> molMeshes = new List<Mesh>();
            int verticesPerAtom = (config.sphereSegments + 1) * (config.sphereSegments + 1);
            int verticesPerBond = 2 * (config.cylinderSegments + 1);
            int indicesPerAtom = config.sphereSegments * config.sphereSegments * 6;
            int indicesPerBond = config.cylinderSegments * 6;

            // 1. 配置Compute Shader
            int kernelId = ballStickCS.FindKernel("CSGenerateBallStickMesh");
            ballStickCS.SetInt("batchSize", batchSize);
            ballStickCS.SetInt("smilesMaxLength", smilesMaxLength);
            ballStickCS.SetInt("sphereSegments", config.sphereSegments);
            ballStickCS.SetInt("cylinderSegments", config.cylinderSegments);
            ballStickCS.SetFloat("bondLength", config.bondLength);
            ballStickCS.SetFloat("atomRadius", config.atomRadius);
            ballStickCS.SetFloat("bondRadius", config.bondRadius);
            ballStickCS.SetInt("topK", config.topK);

            // 2. 绑定Buffer
            ballStickCS.SetBuffer(kernelId, "smilesInputBuffer", smilesBuffer);
            ballStickCS.SetTexture(kernelId, "smilesInputTexture", smilesTexture);
            ballStickCS.SetBuffer(kernelId, "vertexOutputBuffer_position", vertexBufferPosition);
            ballStickCS.SetBuffer(kernelId, "vertexOutputBuffer_normal", vertexBufferNormal);
            ballStickCS.SetBuffer(kernelId, "vertexOutputBuffer_color", vertexBufferColor);
            ballStickCS.SetBuffer(kernelId, "indexOutputBuffer", indexBuffer);
            ballStickCS.SetBuffer(kernelId, "atomCountOutputBuffer", atomCountBuffer);

            // 3. 调度GPU计算（适配移动端32线程组）
            int threadGroupX = Mathf.CeilToInt(batchSize / 32f);
            ballStickCS.Dispatch(kernelId, threadGroupX, 1, 1);
            //while (test && Application.isPlaying)
            //{
            //    ballStickCS.Dispatch(kernelId, threadGroupX, 1, 1);
            //    await UniTask.NextFrame();
            //}

            // 4. 读取原子数
            int[] atomCounts = new int[batchSize];
            atomCountBuffer.GetData(atomCounts);

            // 5. 读取顶点和索引数据
            Vector3[] allPositions = new Vector3[maxVertexCount];
            Vector3[] allNormals = new Vector3[maxVertexCount];
            Vector4[] allColors = new Vector4[maxVertexCount];
            int[] allIndices = new int[maxIndexCount];
            vertexBufferPosition.GetData(allPositions);
            vertexBufferNormal.GetData(allNormals);
            vertexBufferColor.GetData(allColors);
            indexBuffer.GetData(allIndices);

            // 6. 为每个筛选分子生成Mesh
            int vertexOffset = 0;
            int indexOffset = 0;
            foreach (int molIdx in filteredIndices)
            {
                if (molIdx >= batchSize)
                    continue;
                int atomCount = atomCounts[molIdx];
                if (atomCount <= 1) 
                    continue;
                int bondCount = atomCount - 1;

                //Debug.Log("molIdx:" + molIdx);

                vertexOffset = molIdx * (maxAtomLimit * verticesPerAtom + (maxAtomLimit - 1) * verticesPerBond);
                indexOffset = molIdx * (maxAtomLimit * indicesPerAtom + (maxAtomLimit - 1) * indicesPerBond);

                // 计算当前分子的顶点/索引总数
                int totalVertices = atomCount * verticesPerAtom + bondCount * verticesPerBond;
                int totalIndices = atomCount * indicesPerAtom + bondCount * indicesPerBond;
                if (vertexOffset + totalVertices > maxVertexCount || indexOffset + totalIndices > maxIndexCount) 
                    break;

                // 填充Mesh数据
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

                //// 更新偏移量
                //vertexOffset += totalVertices;
                //indexOffset += totalIndices;
            }

            return molMeshes;
        }

        void OnDestroy()
        {
            vertexBufferPosition?.Release();
            vertexBufferNormal?.Release();
            vertexBufferColor?.Release();
            indexBuffer?.Release();
            atomCountBuffer?.Release();
        }
    }

}