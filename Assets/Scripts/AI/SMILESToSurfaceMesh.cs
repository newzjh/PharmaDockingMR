using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Runtime.InteropServices;
using UnityEngine.Rendering;

namespace AIDrugDiscovery
{
    [System.Serializable]
    public class SurfaceConfig
    {
        public float gridSpacing = 0.3f;
        public float isoLevel = 0.5f;
        public int gridResolution = 64; // e.g. 64x64x64
        public float padding = 3.0f; // Padding around atoms
        public float bondLength = 1.5f; // Used for layout
    }

    public struct SurfaceVertexData
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector4 color;
    }

    public class SMILESToSurfaceMesh : MonoBehaviour
    {
        [Header("渲染参数 (Surface Mode)")]
        public ComputeShader surfaceCS;
        public SurfaceConfig config;
        public int smilesMaxLength = 256;
        public int maxVertexLimit = 65000;
        public bool useLegacySmilesTextureInput = false;

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
            int maxAtoms = 100; // Match MAX_ATOM_COUNT in shader

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
        }

        private Texture2D CreateDummyTexture()
        {
            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBAHalf, false);
            tex.SetPixel(0, 0, Color.black);
            tex.Apply();
            return tex;
        }

        public async UniTask<Mesh> GenerateSingleSurfaceMesh(string smiles)
        {
            if (string.IsNullOrEmpty(smiles)) return null;
            if (surfaceCS == null) return null;

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

            Texture boundTexture = singleSmilesTexture ?? CreateDummyTexture();
            bool disposeDummyTexture = singleSmilesTexture == null;

            // Reset vertex count
            vertexCountBuffer.SetData(new int[] { 0 });

            // 1. CSParseSMILES
            int kernelParse = surfaceCS.FindKernel("CSParseSMILES");
            surfaceCS.SetBuffer(kernelParse, "smilesInputBuffer", singleSmilesBuffer);
            surfaceCS.SetTexture(kernelParse, "smilesInputTexture", boundTexture);
            surfaceCS.SetInt("useSmilesTextureInput", useLegacySmilesTextureInput && singleSmilesTexture != null ? 1 : 0);
            surfaceCS.SetInt("smilesMaxLength", smilesMaxLength);
            surfaceCS.SetFloat("bondLength", config.bondLength);
            surfaceCS.SetBuffer(kernelParse, "parsedAtomTypes", parsedAtomTypesBuffer);
            surfaceCS.SetBuffer(kernelParse, "parsedAtomPositions", parsedAtomPositionsBuffer);
            surfaceCS.SetBuffer(kernelParse, "parsedAtomCount", parsedAtomCountBuffer);
            
            surfaceCS.Dispatch(kernelParse, 1, 1, 1);

            // Wait a frame if needed, but since we are doing async, we can just let GPU execute sequentially.
            // All subsequent dispatches will be queued after this one automatically by the driver.

            // Setup Grid
            int res = config.gridResolution;
            float spacing = config.gridSpacing;
            // Approximate grid min: Since we layout along X, min X is roughly 0 - padding. Max X is roughly maxAtoms * bondLength + padding.
            // To make it simple and centered, we can just use a fixed bounding box for small molecules.
            // Max atoms = 100, length = 150. If we use a fixed 64x64x64 grid with 0.3 spacing, total size is ~19.2 Angstroms.
            // Wait, 19.2 Angstroms is too small for a 100 atom molecule laid out linearly!
            // If the molecule is laid out linearly, it can be up to 100 * 1.5 = 150 A long!
            // We should really read back the atom count, or just compute a dynamic grid.
            // Let's do a simple readback to get the bounds, or just make the grid very large.
            // Reading back the count is safer.
            
            int[] parsedCount = new int[1];
            var reqCount = await AsyncGPUReadback.RequestAsync(parsedAtomCountBuffer);
            parsedCount = reqCount.GetData<int>().ToArray();
            int aCount = parsedCount[0];

            if (aCount == 0)
            {
                singleSmilesBuffer.Dispose();
                if (!disposeDummyTexture && singleSmilesTexture != null) Destroy(singleSmilesTexture);
                if (disposeDummyTexture) Destroy(boundTexture);
                return null;
            }

            float maxX = aCount * config.bondLength;
            float padding = config.padding;
            
            // Adjust grid bounds based on actual size
            Vector3 minBounds = new Vector3(-padding, -padding, -padding);
            Vector3 maxBounds = new Vector3(maxX + padding, padding, padding);
            Vector3 size = maxBounds - minBounds;
            
            // Set dynamic grid resolution based on size and spacing
            int resX = Mathf.CeilToInt(size.x / spacing);
            int resY = Mathf.CeilToInt(size.y / spacing);
            int resZ = Mathf.CeilToInt(size.z / spacing);
            
            // Ensure multiples of 8 for thread groups
            resX = Mathf.CeilToInt(resX / 8.0f) * 8;
            resY = Mathf.CeilToInt(resY / 8.0f) * 8;
            resZ = Mathf.CeilToInt(resZ / 8.0f) * 8;
            
            // Reallocate density grids if needed
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

            // 2. CSClearGrid
            int kernelClear = surfaceCS.FindKernel("CSClearGrid");
            surfaceCS.SetBuffer(kernelClear, "densityGrid", densityGridBuffer);
            surfaceCS.SetBuffer(kernelClear, "colorGrid", colorGridBuffer);
            surfaceCS.Dispatch(kernelClear, groupX, groupY, groupZ);

            // 3. CSComputeDensity
            int kernelDensity = surfaceCS.FindKernel("CSComputeDensity");
            surfaceCS.SetBuffer(kernelDensity, "parsedAtomTypes", parsedAtomTypesBuffer);
            surfaceCS.SetBuffer(kernelDensity, "parsedAtomPositions", parsedAtomPositionsBuffer);
            surfaceCS.SetBuffer(kernelDensity, "parsedAtomCount", parsedAtomCountBuffer);
            surfaceCS.SetBuffer(kernelDensity, "densityGrid", densityGridBuffer);
            surfaceCS.SetBuffer(kernelDensity, "colorGrid", colorGridBuffer);
            surfaceCS.Dispatch(kernelDensity, groupX, groupY, groupZ);

            // 4. CSMarchingCubes
            int kernelMC = surfaceCS.FindKernel("CSMarchingCubes");
            surfaceCS.SetBuffer(kernelMC, "densityGrid", densityGridBuffer);
            surfaceCS.SetBuffer(kernelMC, "colorGrid", colorGridBuffer);
            surfaceCS.SetBuffer(kernelMC, "edgeTable", edgeTableBuffer);
            surfaceCS.SetBuffer(kernelMC, "triTable", triTableBuffer);
            surfaceCS.SetBuffer(kernelMC, "vertexBuffer", vertexBuffer);
            surfaceCS.SetBuffer(kernelMC, "vertexCountBuffer", vertexCountBuffer);
            surfaceCS.Dispatch(kernelMC, groupX, groupY, groupZ);

            // 5. Readback results
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

            // Cleanup local resources
            singleSmilesBuffer.Dispose();
            if (!disposeDummyTexture && singleSmilesTexture != null) Destroy(singleSmilesTexture);
            if (disposeDummyTexture) Destroy(boundTexture);

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
        }
    }
}