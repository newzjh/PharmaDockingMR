using UnityEngine;
using System.Collections.Generic;

namespace AIDrugDiscovery
{

    // 原子球冠配置
    [System.Serializable]
    public class AtomSphereConfig
    {
        public float bondLength = 1.0f; // 原子间键长
        public float baseRadius = 0.5f; // 原子球冠基础半径
        public int sphereSegments = 16; // 球冠分段数（影响精度）
        public int topK = 10; // 生成Top-K分子的Mesh
    }

    // Mesh数据结构（用于GPU→CPU传递）
    public struct MeshData
    {
        public Vector3 vertex;
        public Vector3 normal;
        public Color color;
    }

    public class SMILESToMeshGenerator : MonoBehaviour
    {
        public ComputeShader meshGeneratorCS;
        public AtomSphereConfig config;
        public ComputeBuffer smilesBuffer; // 输入的SMILES Buffer
        public int batchSize; // 分子批次大小
        public int smilesMaxLength = 256; // 单个SMILES最大长度

        private ComputeBuffer meshDataBuffer;
        private ComputeBuffer atomCountBuffer; // 每个分子的原子数
        private int maxMeshDataCount; // 最大Mesh数据量

        void Start()
        {
            // 计算最大Mesh数据量：每个原子的球冠顶点数 = (segments+1)*(segments+1)
            int verticesPerAtom = (config.sphereSegments + 1) * (config.sphereSegments + 1);
            maxMeshDataCount = batchSize * config.topK * verticesPerAtom;

            // 初始化Buffer
            meshDataBuffer = new ComputeBuffer(maxMeshDataCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(MeshData)));
            atomCountBuffer = new ComputeBuffer(batchSize, sizeof(int));
        }

        /// <summary>
        /// 生成分子球冠Mesh
        /// </summary>
        public List<Mesh> GenerateMolMeshes(List<int> filteredIndices)
        {
            List<Mesh> molMeshes = new List<Mesh>();
            int verticesPerAtom = (config.sphereSegments + 1) * (config.sphereSegments + 1);

            // 1. 配置Compute Shader
            int kernelId = meshGeneratorCS.FindKernel("CSGenerateMolMesh");
            meshGeneratorCS.SetInt("batchSize", batchSize);
            meshGeneratorCS.SetInt("smilesMaxLength", smilesMaxLength);
            meshGeneratorCS.SetInt("sphereSegments", config.sphereSegments);
            meshGeneratorCS.SetFloat("bondLength", config.bondLength);
            meshGeneratorCS.SetFloat("baseRadius", config.baseRadius);
            meshGeneratorCS.SetInt("topK", config.topK);

            // 2. 绑定Buffer
            meshGeneratorCS.SetBuffer(kernelId, "smilesInputBuffer", smilesBuffer);
            meshGeneratorCS.SetBuffer(kernelId, "meshDataOutput", meshDataBuffer);
            meshGeneratorCS.SetBuffer(kernelId, "atomCountOutput", atomCountBuffer);

            // 3. 调度GPU计算
            int threadGroupX = Mathf.CeilToInt(batchSize / 32f);
            meshGeneratorCS.Dispatch(kernelId, threadGroupX, 1, 1);

            // 4. 读取原子数
            int[] atomCounts = new int[batchSize];
            atomCountBuffer.GetData(atomCounts);

            // 5. 读取Mesh数据并生成Mesh
            MeshData[] allMeshData = new MeshData[maxMeshDataCount];
            meshDataBuffer.GetData(allMeshData);

            int offset = 0;
            foreach (int idx in filteredIndices)
            {
                if (idx >= batchSize) continue;
                int atomCount = atomCounts[idx];
                if (atomCount == 0) continue;

                int totalVertices = atomCount * verticesPerAtom;
                if (offset + totalVertices > maxMeshDataCount) break;

                Mesh mesh = new Mesh();
                Vector3[] vertices = new Vector3[totalVertices];
                Vector3[] normals = new Vector3[totalVertices];
                Color[] colors = new Color[totalVertices];

                for (int i = 0; i < totalVertices; i++)
                {
                    vertices[i] = allMeshData[offset + i].vertex;
                    normals[i] = allMeshData[offset + i].normal;
                    colors[i] = allMeshData[offset + i].color;
                }

                mesh.vertices = vertices;
                mesh.normals = normals;
                mesh.colors = colors;
                mesh.triangles = GenerateTriangles(atomCount, config.sphereSegments);

                molMeshes.Add(mesh);
                offset += totalVertices;
            }

            return molMeshes;
        }

        /// <summary>
        /// 生成球冠的三角面索引
        /// </summary>
        private int[] GenerateTriangles(int atomCount, int segments)
        {
            int trianglesPerAtom = segments * segments * 6;
            int[] triangles = new int[atomCount * trianglesPerAtom];
            int vertexPerAtom = (segments + 1) * (segments + 1);
            int offset = 0;

            for (int a = 0; a < atomCount; a++)
            {
                for (int y = 0; y < segments; y++)
                {
                    for (int x = 0; x < segments; x++)
                    {
                        int v0 = x + y * (segments + 1);
                        int v1 = v0 + 1;
                        int v2 = v0 + (segments + 1);
                        int v3 = v2 + 1;

                        triangles[offset++] = v0 + a * vertexPerAtom;
                        triangles[offset++] = v2 + a * vertexPerAtom;
                        triangles[offset++] = v1 + a * vertexPerAtom;

                        triangles[offset++] = v1 + a * vertexPerAtom;
                        triangles[offset++] = v2 + a * vertexPerAtom;
                        triangles[offset++] = v3 + a * vertexPerAtom;
                    }
                }
            }
            return triangles;
        }

        void OnDestroy()
        {
            meshDataBuffer?.Release();
            atomCountBuffer?.Release();
        }
    }

}