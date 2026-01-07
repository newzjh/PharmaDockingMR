using UnityEngine;
using System.Collections.Generic;

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

    // GPU传递的Mesh顶点数据
    public struct VertexData
    {
        public Vector3 position;
        public Vector3 normal;
        public Color color;
    }

    // GPU传递的Mesh索引数据
    public struct IndexData
    {
        public int triIndex;
    }

    public class SMILESToBallStickMesh : MonoBehaviour
    {
        public ComputeShader ballStickCS;
        public BallStickConfig config;
        public ComputeBuffer smilesBuffer; // 输入的SMILES Buffer
        public int batchSize;              // 分子批次大小
        public int smilesMaxLength = 256;  // 单个SMILES最大长度

        private ComputeBuffer vertexBuffer;
        private ComputeBuffer indexBuffer;
        private ComputeBuffer atomCountBuffer; // 每个分子的原子数
        private int maxVertexCount;
        private int maxIndexCount;

        void Start()
        {
            // 计算单分子最大顶点/索引数：球(每个原子(seg+1)^2) + 棍(每个键2*seg)
            int verticesPerAtom = (config.sphereSegments + 1) * (config.sphereSegments + 1);
            int verticesPerBond = 2 * config.cylinderSegments;
            int indicesPerAtom = config.sphereSegments * config.sphereSegments * 6;
            int indicesPerBond = config.cylinderSegments * 6;

            // 假设单分子最多50个原子，49个键
            maxVertexCount = batchSize * config.topK * (50 * verticesPerAtom + 49 * verticesPerBond);
            maxIndexCount = batchSize * config.topK * (50 * indicesPerAtom + 49 * indicesPerBond);

            // 初始化Buffer
            vertexBuffer = new ComputeBuffer(maxVertexCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(VertexData)));
            indexBuffer = new ComputeBuffer(maxIndexCount, sizeof(int));
            atomCountBuffer = new ComputeBuffer(batchSize, sizeof(int));
        }

        /// <summary>
        /// 生成球棍模型Mesh
        /// </summary>
        /// <param name="filteredIndices">筛选后的分子索引列表</param>
        public List<Mesh> GenerateBallStickMeshes(List<int> filteredIndices)
        {
            List<Mesh> molMeshes = new List<Mesh>();
            int verticesPerAtom = (config.sphereSegments + 1) * (config.sphereSegments + 1);
            int verticesPerBond = 2 * config.cylinderSegments;
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
            ballStickCS.SetBuffer(kernelId, "vertexOutputBuffer", vertexBuffer);
            ballStickCS.SetBuffer(kernelId, "indexOutputBuffer", indexBuffer);
            ballStickCS.SetBuffer(kernelId, "atomCountOutputBuffer", atomCountBuffer);

            // 3. 调度GPU计算（适配移动端32线程组）
            int threadGroupX = Mathf.CeilToInt(batchSize / 32f);
            ballStickCS.Dispatch(kernelId, threadGroupX, 1, 1);

            // 4. 读取原子数
            int[] atomCounts = new int[batchSize];
            atomCountBuffer.GetData(atomCounts);

            // 5. 读取顶点和索引数据
            VertexData[] allVertices = new VertexData[maxVertexCount];
            IndexData[] allIndices = new IndexData[maxIndexCount];
            vertexBuffer.GetData(allVertices);
            indexBuffer.GetData(allIndices);

            // 6. 为每个筛选分子生成Mesh
            int vertexOffset = 0;
            int indexOffset = 0;
            foreach (int molIdx in filteredIndices)
            {
                if (molIdx >= batchSize) continue;
                int atomCount = atomCounts[molIdx];
                if (atomCount <= 1) continue;
                int bondCount = atomCount - 1;

                // 计算当前分子的顶点/索引总数
                int totalVertices = atomCount * verticesPerAtom + bondCount * verticesPerBond;
                int totalIndices = atomCount * indicesPerAtom + bondCount * indicesPerBond;
                if (vertexOffset + totalVertices > maxVertexCount || indexOffset + totalIndices > maxIndexCount) break;

                // 填充Mesh数据
                Mesh mesh = new Mesh();
                Vector3[] positions = new Vector3[totalVertices];
                Vector3[] normals = new Vector3[totalVertices];
                Color[] colors = new Color[totalVertices];
                int[] triangles = new int[totalIndices];

                for (int i = 0; i < totalVertices; i++)
                {
                    positions[i] = allVertices[vertexOffset + i].position;
                    normals[i] = allVertices[vertexOffset + i].normal;
                    colors[i] = allVertices[vertexOffset + i].color;
                }
                for (int i = 0; i < totalIndices; i++)
                {
                    triangles[i] = allIndices[indexOffset + i].triIndex;
                }

                mesh.vertices = positions;
                mesh.normals = normals;
                mesh.colors = colors;
                mesh.triangles = triangles;
                mesh.RecalculateBounds();
                molMeshes.Add(mesh);

                // 更新偏移量
                vertexOffset += totalVertices;
                indexOffset += totalIndices;
            }

            return molMeshes;
        }

        void OnDestroy()
        {
            vertexBuffer?.Release();
            indexBuffer?.Release();
            atomCountBuffer?.Release();
        }
    }

}