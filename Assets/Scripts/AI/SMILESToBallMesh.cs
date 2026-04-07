using UnityEngine;
using System.Collections.Generic;
using System;
using Cysharp.Threading.Tasks;
using UnityEngine.Rendering;
using System.Runtime.InteropServices;

namespace AIDrugDiscovery
{

    
    // Configures the sphere-only molecular renderer used by Ball mode.
    [System.Serializable]
    public class AtomSphereConfig
    {
        public float bondLength = 1.0f;
        public float baseRadius = 0.5f;
        public int sphereSegments = 16;
        public int topK = 10;
    }


    // Generates a Ball-mode mesh for each molecule by expanding precomputed atom positions into spheres.
    public class SMILESToBallMesh : MonoBehaviour
    {
        public ComputeShader meshGeneratorCS;
        public AtomSphereConfig config;
        public int batchSize = 128;
        public int smilesMaxLength = 256;
        public int maxAtomLimit = 60;

        private ComputeBuffer vertexBufferPosition;
        private ComputeBuffer vertexBufferNormal;
        private ComputeBuffer vertexBufferColor;
        private ComputeBuffer indexBuffer;
        private ComputeBuffer atomCountBuffer; 
        private ComputeBuffer selectedIndexBuffer;
        private ComputeBuffer meshAtomStartBuffer;
        private ComputeBuffer meshAtomCountInputBuffer;
        private ComputeBuffer atomTypeInputBuffer;
        private ComputeBuffer atomPositionInputBuffer;
        private int maxVertexCount; 
        private int maxIndexCount;

        void Start()
        {
            // Allocate enough room for the worst-case vertex and index output of the whole batch.
            int verticesPerAtom = (config.sphereSegments + 1) * (config.sphereSegments + 1);
            maxVertexCount = batchSize * maxAtomLimit * verticesPerAtom;
            int indicesPerAtom = config.sphereSegments * config.sphereSegments * 6;
            maxIndexCount = batchSize * maxAtomLimit * indicesPerAtom;

            vertexBufferPosition = new ComputeBuffer(maxVertexCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3)));
            vertexBufferNormal = new ComputeBuffer(maxVertexCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3)));
            vertexBufferColor = new ComputeBuffer(maxVertexCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector4)));
            indexBuffer = new ComputeBuffer(maxIndexCount, sizeof(int));
            atomCountBuffer = new ComputeBuffer(batchSize, sizeof(int));
        }

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

        private void ReleaseInputBuffers()
        {
            meshAtomStartBuffer?.Release();
            meshAtomCountInputBuffer?.Release();
            atomTypeInputBuffer?.Release();
            atomPositionInputBuffer?.Release();
            selectedIndexBuffer?.Release();
        }

        private void AllocateBatchGraphBuffers(int meshCount)
        {
            ReleaseInputBuffers();

            int[] atomStarts = new int[meshCount];
            for (int meshIdx = 0; meshIdx < meshCount; meshIdx++)
                atomStarts[meshIdx] = meshIdx * maxAtomLimit;

            meshAtomStartBuffer = new ComputeBuffer(meshCount, sizeof(int));
            meshAtomCountInputBuffer = new ComputeBuffer(meshCount, sizeof(int));
            atomTypeInputBuffer = new ComputeBuffer(Mathf.Max(1, meshCount * maxAtomLimit), sizeof(int));
            atomPositionInputBuffer = new ComputeBuffer(Mathf.Max(1, meshCount * maxAtomLimit), Marshal.SizeOf(typeof(Vector3)));

            meshAtomStartBuffer.SetData(atomStarts);
            meshAtomCountInputBuffer.SetData(new int[meshCount]);
            atomTypeInputBuffer.SetData(new int[Mathf.Max(1, meshCount * maxAtomLimit)]);
            atomPositionInputBuffer.SetData(new Vector3[Mathf.Max(1, meshCount * maxAtomLimit)]);
        }

        // Batch Ball mode keeps SMILES parsing and layout on the GPU by splitting the work into graph, layout, and mesh kernels.
        public List<Mesh> GenerateMolMeshes(List<int> filteredIndices, RenderTexture smilesTexture)
        {
            List<Mesh> molMeshes = new List<Mesh>();
            int verticesPerAtom = (config.sphereSegments + 1) * (config.sphereSegments + 1);
            int meshCount = filteredIndices.Count;
            if (meshCount == 0)
                return molMeshes;
            AllocateBatchGraphBuffers(meshCount);
            selectedIndexBuffer = new ComputeBuffer(meshCount, sizeof(int));
            selectedIndexBuffer.SetData(filteredIndices.ToArray());
            int kernelGraph = meshGeneratorCS.FindKernel("CSBuildBallGraphBatch");
            int kernelLayout = meshGeneratorCS.FindKernel("CSBuildBallLayoutBatch");
            int kernelMesh = meshGeneratorCS.FindKernel("CSGenerateBallMesh");
            foreach (int kernelId in new[] { kernelGraph, kernelLayout, kernelMesh })
            {
                meshGeneratorCS.SetInt("batchSize", batchSize);
                meshGeneratorCS.SetInt("selectedCount", meshCount);
                meshGeneratorCS.SetInt("useSmilesTextureInput", 1);
                meshGeneratorCS.SetInt("smilesMaxLength", smilesMaxLength);
                meshGeneratorCS.SetInt("sphereSegments", config.sphereSegments);
                meshGeneratorCS.SetFloat("bondLength", config.bondLength);
                meshGeneratorCS.SetFloat("atomRadius", config.baseRadius);
                meshGeneratorCS.SetBuffer(kernelId, "smilesInputBuffer", atomCountBuffer);
                meshGeneratorCS.SetTexture(kernelId, "smilesInputTexture", smilesTexture);
                meshGeneratorCS.SetBuffer(kernelId, "selectedMolIndexBuffer", selectedIndexBuffer);
                meshGeneratorCS.SetBuffer(kernelId, "meshAtomStartBuffer", meshAtomStartBuffer);
                meshGeneratorCS.SetBuffer(kernelId, "meshAtomCountInputBuffer", meshAtomCountInputBuffer);
                meshGeneratorCS.SetBuffer(kernelId, "atomTypeInputBuffer", atomTypeInputBuffer);
                meshGeneratorCS.SetBuffer(kernelId, "atomPositionInputBuffer", atomPositionInputBuffer);
                meshGeneratorCS.SetBuffer(kernelId, "atomCountOutputBuffer", atomCountBuffer);
            }
            meshGeneratorCS.SetBuffer(kernelMesh, "vertexOutputBuffer_position", vertexBufferPosition);
            meshGeneratorCS.SetBuffer(kernelMesh, "vertexOutputBuffer_normal", vertexBufferNormal);
            meshGeneratorCS.SetBuffer(kernelMesh, "vertexOutputBuffer_color", vertexBufferColor);
            meshGeneratorCS.SetBuffer(kernelMesh, "indexOutputBuffer", indexBuffer);

            int threadGroupX = Mathf.CeilToInt(Mathf.Max(1, meshCount) / 32f);
            meshGeneratorCS.Dispatch(kernelGraph, threadGroupX, 1, 1);
            meshGeneratorCS.Dispatch(kernelLayout, threadGroupX, 1, 1);
            meshGeneratorCS.Dispatch(kernelMesh, threadGroupX, 1, 1);

            int[] atomCounts = new int[meshCount];
            atomCountBuffer.GetData(atomCounts);

            Vector3[] allPositions = new Vector3[maxVertexCount];
            Vector3[] allNormals = new Vector3[maxVertexCount];
            Vector4[] allColors = new Vector4[maxVertexCount];
            vertexBufferPosition.GetData(allPositions);
            vertexBufferNormal.GetData(allNormals);
            vertexBufferColor.GetData(allColors);

            int offset = 0;
            for (int idx = 0; idx < meshCount; idx++)
            {
                int atomCount = atomCounts[idx];
                if (atomCount == 0) continue;
                offset = idx * (maxAtomLimit * verticesPerAtom);

                int totalVertices = atomCount * verticesPerAtom;
                if (offset + totalVertices > maxVertexCount) break;

                Mesh mesh = new Mesh();
                Vector3[] positions = new Vector3[totalVertices];
                Vector3[] normals = new Vector3[totalVertices];
                Color[] colors = new Color[totalVertices];

                for (int i = 0; i < totalVertices; i++)
                {
                    colors[i] = allColors[offset + i];
                }
                Array.Copy(allPositions, offset, positions, 0, totalVertices);
                Array.Copy(allNormals, offset, normals, 0, totalVertices);

                mesh.vertices = positions;
                mesh.normals = normals;
                mesh.colors = colors;
                mesh.triangles = GenerateTriangles(atomCount, config.sphereSegments);

                molMeshes.Add(mesh);
            }

            return molMeshes;
        }

        public async UniTask<Mesh> GenerateSingleBallMesh(string smiles)
        {
            if (string.IsNullOrEmpty(smiles))
                return null;
            return await GenerateSingleBallMesh(BuildSmilesData(smiles));
        }

        public async UniTask<Mesh> GenerateSingleBallMesh(int[] smilesData)
        {
            if (smilesData == null || smilesData.Length == 0)
                return null;

            string smiles = SmilesMeshPreprocessor.DecodeAsciiSmiles(smilesData);
            SmilesMeshDescription description = SmilesMeshPreprocessor.Build(smiles, config.bondLength);
            if (description.AtomTypes.Count <= 0)
                return null;

            AllocateBatchGraphBuffers(1);
            atomTypeInputBuffer.SetData(description.AtomTypes.ToArray());
            atomPositionInputBuffer.SetData(description.AtomPositions.ToArray());
            meshAtomCountInputBuffer.SetData(new[] { description.AtomTypes.Count });
            atomCountBuffer.SetData(new int[batchSize]);

            int kernelId = meshGeneratorCS.FindKernel("CSGenerateBallMesh");
            meshGeneratorCS.SetInt("selectedCount", 1);
            meshGeneratorCS.SetInt("sphereSegments", config.sphereSegments);
            meshGeneratorCS.SetFloat("atomRadius", config.baseRadius);
            meshGeneratorCS.SetBuffer(kernelId, "meshAtomStartBuffer", meshAtomStartBuffer);
            meshGeneratorCS.SetBuffer(kernelId, "meshAtomCountInputBuffer", meshAtomCountInputBuffer);
            meshGeneratorCS.SetBuffer(kernelId, "atomTypeInputBuffer", atomTypeInputBuffer);
            meshGeneratorCS.SetBuffer(kernelId, "atomPositionInputBuffer", atomPositionInputBuffer);
            meshGeneratorCS.SetBuffer(kernelId, "vertexOutputBuffer_position", vertexBufferPosition);
            meshGeneratorCS.SetBuffer(kernelId, "vertexOutputBuffer_normal", vertexBufferNormal);
            meshGeneratorCS.SetBuffer(kernelId, "vertexOutputBuffer_color", vertexBufferColor);
            meshGeneratorCS.SetBuffer(kernelId, "indexOutputBuffer", indexBuffer);
            meshGeneratorCS.SetBuffer(kernelId, "atomCountOutputBuffer", atomCountBuffer);
            meshGeneratorCS.Dispatch(kernelId, 1, 1, 1);

            int[] atomCounts = (await AsyncGPUReadback.RequestAsync(atomCountBuffer)).GetData<int>().ToArray();
            if (atomCounts.Length == 0 || atomCounts[0] <= 0)
                return null;

            int verticesPerAtom = (config.sphereSegments + 1) * (config.sphereSegments + 1);
            int totalVertices = atomCounts[0] * verticesPerAtom;
            Vector3[] allPositions = (await AsyncGPUReadback.RequestAsync(vertexBufferPosition)).GetData<Vector3>().ToArray();
            Vector3[] allNormals = (await AsyncGPUReadback.RequestAsync(vertexBufferNormal)).GetData<Vector3>().ToArray();
            Vector4[] allColors = (await AsyncGPUReadback.RequestAsync(vertexBufferColor)).GetData<Vector4>().ToArray();

            Mesh mesh = new Mesh();
            Vector3[] positions = new Vector3[totalVertices];
            Vector3[] normals = new Vector3[totalVertices];
            Color[] colors = new Color[totalVertices];
            Array.Copy(allPositions, 0, positions, 0, totalVertices);
            Array.Copy(allNormals, 0, normals, 0, totalVertices);
            for (int i = 0; i < totalVertices; i++)
                colors[i] = allColors[i];
            mesh.vertices = positions;
            mesh.normals = normals;
            mesh.colors = colors;
            mesh.triangles = GenerateTriangles(atomCounts[0], config.sphereSegments);
            mesh.RecalculateBounds();
            return mesh;
        }
        private int[] GenerateTriangles(int atomCount, int segments)
        {
            // Rebuild the sphere triangle list on the CPU because Ball mode only reads per-vertex data back from the shader.
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
            vertexBufferPosition?.Release();
            vertexBufferNormal?.Release();
            vertexBufferColor?.Release();
            indexBuffer?.Release();
            atomCountBuffer?.Release();
            ReleaseInputBuffers();
        }
    }

}
