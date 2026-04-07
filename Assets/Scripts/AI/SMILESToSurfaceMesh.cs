using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Runtime.InteropServices;
using UnityEngine.Rendering;

namespace AIDrugDiscovery
{
    // Controls voxelization and marching-cubes settings for Surface mode.
    [System.Serializable]
    public class SurfaceConfig
    {
        public float gridSpacing = 0.3f;
        public float isoLevel = 0.5f;
        public int gridResolution = 64; // For example, a 64x64x64 voxel grid.
        public float padding = 3.0f; // Extra space around the ligand before voxelization.
        public float bondLength = 1.5f; // Used to build the CPU-side atom layout from the SMILES graph.
    }

    public struct SurfaceVertexData
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector4 color;
    }

    // Generates an isosurface mesh for a single ligand by voxelizing CPU-preprocessed atom positions.
    public class SMILESToSurfaceMesh : MonoBehaviour
    {
        [Header("Settings")]
        public ComputeShader surfaceCS;
        public SurfaceConfig config;
        public int smilesMaxLength = 256;
        public int maxVertexLimit = 65000;
        public bool useLegacySmilesTextureInput = false;
        private ComputeBuffer dummySmilesInputBuffer;
        private Texture2D dummySmilesInputTexture;

        private ComputeBuffer edgeTableBuffer;
        private ComputeBuffer triTableBuffer;

        private ComputeBuffer parsedAtomTypesBuffer;
        private ComputeBuffer parsedAtomPositionsBuffer;
        private ComputeBuffer parsedAtomCountBuffer;
        private ComputeBuffer densityGridBuffer;
        private ComputeBuffer colorGridBuffer;

        private ComputeBuffer vertexBuffer;
        private ComputeBuffer vertexCountBuffer;

        private void Awake()
        {
            InitializeBuffers();
        }

        private void InitializeBuffers()
        {
            int maxAtoms = 100; // Must match MAX_ATOM_COUNT in the compute shader.

            edgeTableBuffer = new ComputeBuffer(256, sizeof(int));
            edgeTableBuffer.SetData(MarchingCubesTables.cubeEdgeFlags);

            triTableBuffer = new ComputeBuffer(256 * 16, sizeof(int));
            triTableBuffer.SetData(MarchingCubesTables.triangleConnectionTable);

            parsedAtomTypesBuffer = new ComputeBuffer(maxAtoms, sizeof(int));
            parsedAtomPositionsBuffer = new ComputeBuffer(maxAtoms, sizeof(float) * 3);
            parsedAtomCountBuffer = new ComputeBuffer(1, sizeof(int));

            int totalGridCells = config.gridResolution * config.gridResolution * config.gridResolution;
            densityGridBuffer = new ComputeBuffer(totalGridCells, sizeof(float));
            colorGridBuffer = new ComputeBuffer(totalGridCells, sizeof(float) * 4);

            vertexBuffer = new ComputeBuffer(maxVertexLimit, Marshal.SizeOf(typeof(SurfaceVertexData)));
            vertexCountBuffer = new ComputeBuffer(1, sizeof(int));
            dummySmilesInputBuffer = new ComputeBuffer(1, sizeof(int));
            dummySmilesInputBuffer.SetData(new[] { 0 });
            dummySmilesInputTexture = CreateDummyTexture();
        }

        private Texture2D CreateDummyTexture()
        {
            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBAHalf, false);
            tex.SetPixel(0, 0, Color.black);
            tex.Apply();
            return tex;
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

        public async UniTask<Mesh> GenerateSingleSurfaceMesh(string smiles)
        {
            if (string.IsNullOrEmpty(smiles)) return null;
            if (surfaceCS == null) return null;

            return await GenerateSingleSurfaceMesh(BuildSmilesData(smiles));
        }

        public async UniTask<Mesh> GenerateSingleSurfaceMesh(int[] smilesData)
        {
            if (smilesData == null || smilesData.Length == 0 || surfaceCS == null)
                return null;

            string smiles = SmilesMeshPreprocessor.DecodeAsciiSmiles(smilesData);
            SmilesMeshDescription description = SmilesMeshPreprocessor.Build(smiles, config.bondLength);
            if (description.AtomTypes.Count == 0)
                return null;

            // Upload the CPU-preprocessed atom layout so the compute shader only handles density and marching cubes.
            parsedAtomTypesBuffer.SetData(description.AtomTypes.ToArray());
            parsedAtomPositionsBuffer.SetData(description.AtomPositions.ToArray());
            parsedAtomCountBuffer.SetData(new[] { description.AtomTypes.Count });

            vertexCountBuffer.SetData(new int[] { 0 });

            int res = config.gridResolution;
            float spacing = config.gridSpacing;
            float padding = config.padding;

            Vector3 minBounds = description.AtomPositions[0];
            Vector3 maxBounds = description.AtomPositions[0];
            for (int i = 1; i < description.AtomPositions.Count; i++)
            {
                minBounds = Vector3.Min(minBounds, description.AtomPositions[i]);
                maxBounds = Vector3.Max(maxBounds, description.AtomPositions[i]);
            }
            minBounds -= Vector3.one * padding;
            maxBounds += Vector3.one * padding;
            Vector3 size = maxBounds - minBounds;

            int resX = Mathf.CeilToInt(size.x / spacing);
            int resY = Mathf.CeilToInt(size.y / spacing);
            int resZ = Mathf.CeilToInt(size.z / spacing);

            resX = Mathf.CeilToInt(resX / 8.0f) * 8;
            resY = Mathf.CeilToInt(resY / 8.0f) * 8;
            resZ = Mathf.CeilToInt(resZ / 8.0f) * 8;

            int totalCells = resX * resY * resZ;
            if (densityGridBuffer == null || densityGridBuffer.count < totalCells)
            {
                densityGridBuffer?.Release();
                colorGridBuffer?.Release();
                densityGridBuffer = new ComputeBuffer(totalCells, sizeof(float));
                colorGridBuffer = new ComputeBuffer(totalCells, sizeof(float) * 4);
            }

            surfaceCS.SetInts("gridSize", new int[] { resX, resY, resZ });
            surfaceCS.SetFloats("gridMin", new float[] { minBounds.x, minBounds.y, minBounds.z });
            surfaceCS.SetFloat("gridSpacing", spacing);
            surfaceCS.SetFloat("isoLevel", config.isoLevel);
            surfaceCS.SetInt("maxVertexCount", maxVertexLimit);

            int groupX = resX / 8;
            int groupY = resY / 8;
            int groupZ = resZ / 8;

            int kernelClear = surfaceCS.FindKernel("CSClearGrid");
            surfaceCS.SetBuffer(kernelClear, "densityGrid", densityGridBuffer);
            surfaceCS.SetBuffer(kernelClear, "colorGrid", colorGridBuffer);
            surfaceCS.Dispatch(kernelClear, groupX, groupY, groupZ);

            int kernelDensity = surfaceCS.FindKernel("CSComputeDensity");
            surfaceCS.SetBuffer(kernelDensity, "parsedAtomTypes", parsedAtomTypesBuffer);
            surfaceCS.SetBuffer(kernelDensity, "parsedAtomPositions", parsedAtomPositionsBuffer);
            surfaceCS.SetBuffer(kernelDensity, "parsedAtomCount", parsedAtomCountBuffer);
            surfaceCS.SetBuffer(kernelDensity, "densityGrid", densityGridBuffer);
            surfaceCS.SetBuffer(kernelDensity, "colorGrid", colorGridBuffer);
            surfaceCS.Dispatch(kernelDensity, groupX, groupY, groupZ);

            int kernelMC = surfaceCS.FindKernel("CSMarchingCubes");
            surfaceCS.SetBuffer(kernelMC, "densityGrid", densityGridBuffer);
            surfaceCS.SetBuffer(kernelMC, "colorGrid", colorGridBuffer);
            surfaceCS.SetBuffer(kernelMC, "edgeTable", edgeTableBuffer);
            surfaceCS.SetBuffer(kernelMC, "triTable", triTableBuffer);
            surfaceCS.SetBuffer(kernelMC, "vertexBuffer", vertexBuffer);
            surfaceCS.SetBuffer(kernelMC, "vertexCountBuffer", vertexCountBuffer);
            surfaceCS.Dispatch(kernelMC, groupX, groupY, groupZ);

            var reqVC = await AsyncGPUReadback.RequestAsync(vertexCountBuffer);
            int[] vCountArray = reqVC.GetData<int>().ToArray();
            int vCount = vCountArray[0];

            if (vCount > maxVertexLimit) vCount = (maxVertexLimit / 3) * 3;

            Mesh finalMesh = null;
            if (vCount > 0 && vCount % 3 == 0)
            {
                var reqVB = await AsyncGPUReadback.RequestAsync(vertexBuffer, vCount * Marshal.SizeOf(typeof(SurfaceVertexData)), 0);
                SurfaceVertexData[] verticesData = reqVB.GetData<SurfaceVertexData>().ToArray();

                Vector3[] verts = new Vector3[vCount];
                Vector3[] norms = new Vector3[vCount];
                Color[] cols = new Color[vCount];
                int[] indices = new int[vCount];

                for (int i = 0; i < vCount; i++)
                {
                    verts[i] = verticesData[i].position;
                    norms[i] = verticesData[i].normal;
                    cols[i] = verticesData[i].color;
                    indices[i] = i;
                }

                finalMesh = new Mesh();
                finalMesh.indexFormat = vCount > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                finalMesh.vertices = verts;
                finalMesh.normals = norms;
                finalMesh.colors = cols;
                finalMesh.triangles = indices;
            }

            return finalMesh;
        }

        private void OnDestroy()
        {
            edgeTableBuffer?.Release();
            triTableBuffer?.Release();
            parsedAtomTypesBuffer?.Release();
            parsedAtomPositionsBuffer?.Release();
            parsedAtomCountBuffer?.Release();
            densityGridBuffer?.Release();
            colorGridBuffer?.Release();
            vertexBuffer?.Release();
            vertexCountBuffer?.Release();
            dummySmilesInputBuffer?.Release();
            if (dummySmilesInputTexture != null)
                Destroy(dummySmilesInputTexture);
        }
    }
}
