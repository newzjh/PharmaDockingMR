using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using static Microsoft.MixedReality.GraphicsTools.ClippingPrimitive;
using Cysharp.Threading.Tasks;

namespace AIDrugDiscovery
{
    
    public enum AtomType
    {
        C, N, O, S, H, P, F, Cl, Br, I, Other
    }

    
    [Serializable]
    public class ProteinHeatmapConfig
    {
        [Header("Settings")]
        public string proteinName = "1AQ1"; 

        [Header("Settings")]
        public int heatmapSize = 32; 
        public float gridSpacing = 1.0f; 
        public Vector3 activeSiteCenter = new Vector3(10.5f, 8.2f, 12.7f); 

        [Header("Settings")]
        public int kernelSize = 3; 
        public int inChannels = 4; 
        public int outChannels = 4; 

        [Header("Settings")]
        public bool lowPowerMode = false; 
    }

    public class HeatmapGenerator : MonoBehaviour
    {
        
        public struct AtomData
        {
            public Vector3 position;
            public int atomicNum;
            public int charge;
            public int hybridization;
            public int degree;
            public int molId;
        }

        public struct HeatmapPixel
        {
            public Vector4 features;
        }

        [Header("Settings")]
        public ComputeShader heatmapConvCS;
        public ComputeShader sparseConv3DCS;
        public List<ProteinHeatmapConfig> proteinConfigs; 

        [Header("Settings")]
        public bool useGpuHeatmapBuild2D = true;
        public bool useGpuHeatmapBuild3D = true;

        [Header("Settings")]
        public bool autoVisualize = true; 
        public float heatmapPlaneScale = 0.1f; 

        public async void Begin()
        {
            
            foreach (var config in proteinConfigs)
            {
                GenerateProteinHeatmap(config);
            }
        }

        #region Core Function 1: Load protein atom data
        public AtomData[] LoadProteinAtomData(ProteinHeatmapConfig config)
        {
            List<AtomData> atomList = new List<AtomData>();

            string tempfolder = Application.persistentDataPath + "/cachepdb";
            if (Directory.Exists(tempfolder) == false)
            {
                Directory.CreateDirectory(tempfolder);
            }
            string pdbqtFullPath = tempfolder + "/" + config.proteinName + ".pdb";


            
            if (!File.Exists(pdbqtFullPath))
            {
                Debug.LogError($"Heatmap generation status");
                return atomList.ToArray();
            }

            
            bool skipHydrogen = config.lowPowerMode;

            
            string[] lines = File.ReadAllLines(pdbqtFullPath);
            int parsedAtomCount = 0;
            int skippedAtomCount = 0;

            foreach (string line in lines)
            {
                
                if (!line.StartsWith("ATOM") && !line.StartsWith("HETATM")) continue;

                try
                {
                    
                    string atomName = line.Length >= 17 ? line.Substring(12, 4).Trim() : "";
                    if (string.IsNullOrEmpty(atomName)) continue;

                    
                    if (skipHydrogen && atomName.StartsWith("H"))
                    {
                        skippedAtomCount++;
                        continue;
                    }

                    
                    char atomSymbol = atomName[0];
                    AtomType atomType = AtomType.Other;
                    switch (atomSymbol)
                    {
                        case 'C': atomType = AtomType.C; break;
                        case 'N': atomType = AtomType.N; break;
                        case 'O': atomType = AtomType.O; break;
                        case 'S': atomType = AtomType.S; break;
                        case 'H': atomType = AtomType.H; break;
                        case 'P': atomType = AtomType.P; break;
                        case 'F': atomType = AtomType.F; break;
                        //case 'C': if (atomName.Contains("Cl")) atomType = AtomType.Cl; break;
                        case 'B': if (atomName.Contains("Br")) atomType = AtomType.Br; break;
                        case 'I': atomType = AtomType.I; break;
                        default: atomType = AtomType.Other; break;
                    }

                    
                    float x = ParseFloatSafe(line, 30, 8);
                    float y = ParseFloatSafe(line, 38, 8);
                    float z = ParseFloatSafe(line, 46, 8);
                    Vector3 position = new Vector3(x, y, z);

                    
                    float charge = ParseFloatSafe(line, 70, 6);
                    int chargeInt = Mathf.RoundToInt(charge * 100); 

                    
                    int hybridization = GetHybridizationByAtomType(atomType);
                    int degree = GetBondDegreeByAtomType(atomType);

                    
                    AtomData atom = new AtomData
                    {
                        position = position,
                        atomicNum = (int)atomType,
                        charge = chargeInt,
                        hybridization = hybridization,
                        degree = degree,
                        molId = 0
                    };
                    atomList.Add(atom);
                    parsedAtomCount++;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Heatmap generation status");
                    continue;
                }
            }

            
            string skipLog = skipHydrogen ? $"(low power mode skipped {skippedAtomCount} hydrogen atoms)" : "";
            Debug.Log($"Heatmap generation status");
            return atomList.ToArray();
        }
        #endregion

        #region Core Function 2: Generate protein heatmap
        public async UniTask<Texture2D> GenerateProteinHeatmap(ProteinHeatmapConfig config)
        {
            
            int finalHeatmapSize = config.lowPowerMode ? Mathf.Max(16, config.heatmapSize / 2) : config.heatmapSize;
            Debug.Log($"Heatmap generation status");

            
            AtomData[] proteinAtoms = LoadProteinAtomData(config);
            if (proteinAtoms == null || proteinAtoms.Length == 0)
            {
                Debug.LogError($"Heatmap generation status");
                return null;
            }

            Texture2D heatmapTex = useGpuHeatmapBuild2D
                ? await RunSparseConvCS(proteinAtoms, config, finalHeatmapSize)
                : await RunSparseConvCS(BuildRawHeatmap2DCPU(proteinAtoms, config, finalHeatmapSize), proteinAtoms, config, finalHeatmapSize);

            
            if (autoVisualize && heatmapTex != null)
            {
                VisualizeHeatmap(heatmapTex, config);
            }

            return heatmapTex;
        }


        public async UniTask<RenderTexture> GenerateProteinHeatmap3D(ProteinHeatmapConfig config)
        {
            
            int finalHeatmapSize = config.lowPowerMode ? Mathf.Max(16, config.heatmapSize / 2) : config.heatmapSize;
            Debug.Log($"Heatmap generation status");

            
            AtomData[] proteinAtoms = LoadProteinAtomData(config);
            if (proteinAtoms == null || proteinAtoms.Length == 0)
            {
                Debug.LogError($"Heatmap generation status");
                return null;
            }

            RenderTexture heatmapTex = useGpuHeatmapBuild3D
                ? await RunSparseConvCS3D(proteinAtoms, config, finalHeatmapSize)
                : await RunSparseConvCS3D(BuildRawHeatmap3DCPU(proteinAtoms, config, finalHeatmapSize), proteinAtoms, config, finalHeatmapSize);

            return heatmapTex;
        }

        #endregion

        #region Helper: Execute sparse convolution compute shader
        private HeatmapPixel[] BuildRawHeatmap2DCPU(AtomData[] proteinAtoms, ProteinHeatmapConfig config, int heatmapSize)
        {
            int pixelCount = heatmapSize * heatmapSize;
            HeatmapPixel[] rawHeatmap = new HeatmapPixel[pixelCount];
            float gridRadius = config.lowPowerMode ? 1.5f : 1.0f;

            for (int y = 0; y < heatmapSize; y++)
            {
                for (int x = 0; x < heatmapSize; x++)
                {
                    int idx = y * heatmapSize + x;
                    Vector4 features = Vector4.zero;
                    float gridX = config.activeSiteCenter.x + (x - heatmapSize / 2) * config.gridSpacing;
                    float gridZ = config.activeSiteCenter.z + (y - heatmapSize / 2) * config.gridSpacing;
                    Vector3 gridCenter = new Vector3(gridX, config.activeSiteCenter.y, gridZ);
                    int atomInGrid = 0;

                    foreach (var atom in proteinAtoms)
                    {
                        Vector3 sampleCenter = new Vector3(gridCenter.x, atom.position.y, gridCenter.z);
                        if (Vector3.Distance(atom.position, sampleCenter) > gridRadius)
                            continue;

                        atomInGrid++;
                        features.x += (float)atom.atomicNum / (int)AtomType.Other;
                        features.y += (float)atom.charge / 200f;
                        features.z += IsHydrophobic(atom.atomicNum) ? 1 : 0;
                        features.w += IsHydrogenBond(atom.atomicNum) ? 1 : 0;
                    }

                    if (atomInGrid > 0)
                        features /= atomInGrid;

                    rawHeatmap[idx] = new HeatmapPixel { features = features };
                }
            }

            return rawHeatmap;
        }

        private Texture3D BuildRawHeatmap3DCPU(AtomData[] proteinAtoms, ProteinHeatmapConfig config, int heatmapSize)
        {
            Texture3D rawHeatmap = new Texture3D(heatmapSize, heatmapSize, heatmapSize, TextureFormat.RGBAHalf, false);
            rawHeatmap.filterMode = FilterMode.Point;
            rawHeatmap.wrapMode = TextureWrapMode.Clamp;
            float gridRadius = config.lowPowerMode ? 1.5f : 1.0f;

            for (int z = 0; z < heatmapSize; z++)
            {
                for (int y = 0; y < heatmapSize; y++)
                {
                    for (int x = 0; x < heatmapSize; x++)
                    {
                        Color features = Color.black;
                        float gridX = config.activeSiteCenter.x + (x - heatmapSize / 2) * config.gridSpacing;
                        float gridY = config.activeSiteCenter.y + (y - heatmapSize / 2) * config.gridSpacing;
                        float gridZ = config.activeSiteCenter.z + (z - heatmapSize / 2) * config.gridSpacing;
                        Vector3 gridCenter = new Vector3(gridX, gridY, gridZ);
                        int atomInGrid = 0;

                        foreach (var atom in proteinAtoms)
                        {
                            if (Vector3.Distance(atom.position, gridCenter) > gridRadius)
                                continue;

                            atomInGrid++;
                            features.r += (float)atom.atomicNum / (int)AtomType.Other;
                            features.g += (float)atom.charge / 200f;
                            features.b += IsHydrophobic(atom.atomicNum) ? 1 : 0;
                            features.a += IsHydrogenBond(atom.atomicNum) ? 1 : 0;
                        }

                        if (atomInGrid > 0)
                            features /= atomInGrid;

                        rawHeatmap.SetPixel(x, y, z, features);
                    }
                }
            }

            rawHeatmap.Apply();
            return rawHeatmap;
        }

        public async UniTask<Texture2D> RunSparseConvCS(AtomData[] proteinAtoms, ProteinHeatmapConfig config, int heatmapSize)
        {
            int pixelCount = heatmapSize * heatmapSize;

            
            int atomStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(AtomData));
            ComputeBuffer atomBuffer = new ComputeBuffer(proteinAtoms.Length, atomStride);
            atomBuffer.SetData(proteinAtoms);

            int heatmapStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(HeatmapPixel));
            ComputeBuffer rawBuffer = new ComputeBuffer(pixelCount, heatmapStride);
            ComputeBuffer outputBuffer = new ComputeBuffer(pixelCount, heatmapStride);
            outputBuffer.SetData(new HeatmapPixel[pixelCount]);

            
            float[] kernelWeights = new float[config.kernelSize * config.kernelSize * config.inChannels * config.outChannels];
            float weightVal = 1f / (config.kernelSize * config.kernelSize);
            for (int i = 0; i < kernelWeights.Length; i++) kernelWeights[i] = weightVal;

            ComputeBuffer kernelBuffer = new ComputeBuffer(kernelWeights.Length, sizeof(float));
            kernelBuffer.SetData(kernelWeights);

            
            int buildKernelId = heatmapConvCS.FindKernel("CSBuildHeatmap2D");
            int kernelId = heatmapConvCS.FindKernel("CSSparseConv");
            heatmapConvCS.SetInt("heatmapSize", heatmapSize);
            heatmapConvCS.SetInt("kernelSize", config.kernelSize);
            heatmapConvCS.SetFloat("padding", 1f);
            heatmapConvCS.SetFloat("stride", 1f);
            heatmapConvCS.SetInt("inChannels", config.inChannels);
            heatmapConvCS.SetInt("outChannels", config.outChannels);
            heatmapConvCS.SetInt("atomCount", proteinAtoms.Length);
            heatmapConvCS.SetVector("activeSiteCenter", config.activeSiteCenter);
            heatmapConvCS.SetFloat("gridSpacing", config.gridSpacing);
            heatmapConvCS.SetFloat("gridRadius", config.lowPowerMode ? 1.5f : 1.0f);

            
            heatmapConvCS.SetBuffer(buildKernelId, "atomBuffer", atomBuffer);
            heatmapConvCS.SetBuffer(buildKernelId, "heatmapInput", rawBuffer);
            heatmapConvCS.SetBuffer(buildKernelId, "kernelWeights", kernelBuffer);
            heatmapConvCS.SetBuffer(buildKernelId, "heatmapOutput", outputBuffer);
            heatmapConvCS.SetBuffer(buildKernelId, "rawHeatmapOutput", rawBuffer);

            heatmapConvCS.SetBuffer(kernelId, "heatmapInput", rawBuffer);
            heatmapConvCS.SetBuffer(kernelId, "kernelWeights", kernelBuffer);
            heatmapConvCS.SetBuffer(kernelId, "heatmapOutput", outputBuffer);
            heatmapConvCS.SetBuffer(kernelId, "atomBuffer", atomBuffer);
            heatmapConvCS.SetBuffer(kernelId, "rawHeatmapOutput", rawBuffer);

            
            int threadGroupX = Mathf.CeilToInt(heatmapSize / 32f);
            int threadGroupY = Mathf.CeilToInt(heatmapSize / 32f);
            heatmapConvCS.Dispatch(buildKernelId, threadGroupX, threadGroupY, 1);
            heatmapConvCS.Dispatch(kernelId, threadGroupX, threadGroupY, 1);

            
            HeatmapPixel[] convHeatmap = new HeatmapPixel[pixelCount];
            outputBuffer.GetData(convHeatmap);

            Texture2D heatmapTex = new Texture2D(heatmapSize, heatmapSize, TextureFormat.RGBAFloat, false);
            heatmapTex.filterMode = FilterMode.Point;
            heatmapTex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[pixelCount];
            for (int y = 0; y < heatmapSize; y++)
            {
                for (int x = 0; x < heatmapSize; x++)
                {
                    int idx = y * heatmapSize + x;
                    Vector4 feat = convHeatmap[idx].features;
                    pixels[idx] = new Color(feat.x, feat.y, feat.z, feat.w);
                }
            }
            heatmapTex.SetPixels(pixels);
            heatmapTex.Apply();

            
            atomBuffer.Release();
            rawBuffer.Release();
            outputBuffer.Release();
            kernelBuffer.Release();

            return heatmapTex;
        }

        public async UniTask<Texture2D> RunSparseConvCS(HeatmapPixel[] rawHeatmap, AtomData[] proteinAtoms, ProteinHeatmapConfig config, int heatmapSize)
        {
            int pixelCount = heatmapSize * heatmapSize;
            int atomStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(AtomData));
            ComputeBuffer atomBuffer = new ComputeBuffer(proteinAtoms.Length, atomStride);
            atomBuffer.SetData(proteinAtoms);

            int heatmapStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(HeatmapPixel));
            ComputeBuffer inputBuffer = new ComputeBuffer(pixelCount, heatmapStride);
            inputBuffer.SetData(rawHeatmap);
            ComputeBuffer outputBuffer = new ComputeBuffer(pixelCount, heatmapStride);
            outputBuffer.SetData(new HeatmapPixel[pixelCount]);

            float[] kernelWeights = new float[config.kernelSize * config.kernelSize * config.inChannels * config.outChannels];
            float weightVal = 1f / (config.kernelSize * config.kernelSize);
            for (int i = 0; i < kernelWeights.Length; i++) kernelWeights[i] = weightVal;

            ComputeBuffer kernelBuffer = new ComputeBuffer(kernelWeights.Length, sizeof(float));
            kernelBuffer.SetData(kernelWeights);

            int kernelId = heatmapConvCS.FindKernel("CSSparseConv");
            heatmapConvCS.SetInt("heatmapSize", heatmapSize);
            heatmapConvCS.SetInt("kernelSize", config.kernelSize);
            heatmapConvCS.SetFloat("padding", 1f);
            heatmapConvCS.SetFloat("stride", 1f);
            heatmapConvCS.SetInt("inChannels", config.inChannels);
            heatmapConvCS.SetInt("outChannels", config.outChannels);
            heatmapConvCS.SetBuffer(kernelId, "heatmapInput", inputBuffer);
            heatmapConvCS.SetBuffer(kernelId, "kernelWeights", kernelBuffer);
            heatmapConvCS.SetBuffer(kernelId, "heatmapOutput", outputBuffer);
            heatmapConvCS.SetBuffer(kernelId, "atomBuffer", atomBuffer);
            heatmapConvCS.SetBuffer(kernelId, "rawHeatmapOutput", outputBuffer);

            int threadGroupX = Mathf.CeilToInt(heatmapSize / 32f);
            int threadGroupY = Mathf.CeilToInt(heatmapSize / 32f);
            heatmapConvCS.Dispatch(kernelId, threadGroupX, threadGroupY, 1);

            HeatmapPixel[] convHeatmap = new HeatmapPixel[pixelCount];
            outputBuffer.GetData(convHeatmap);

            Texture2D heatmapTex = new Texture2D(heatmapSize, heatmapSize, TextureFormat.RGBAFloat, false);
            heatmapTex.filterMode = FilterMode.Point;
            heatmapTex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[pixelCount];
            for (int idx = 0; idx < pixelCount; idx++)
            {
                Vector4 feat = convHeatmap[idx].features;
                pixels[idx] = new Color(feat.x, feat.y, feat.z, feat.w);
            }
            heatmapTex.SetPixels(pixels);
            heatmapTex.Apply();

            atomBuffer.Release();
            inputBuffer.Release();
            outputBuffer.Release();
            kernelBuffer.Release();

            return heatmapTex;
        }

        public bool test = true;
        public async UniTask<RenderTexture> RunSparseConvCS3D(AtomData[] proteinAtoms, ProteinHeatmapConfig config, int heatmapSize)
        {

            
            int atomStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(AtomData));
            ComputeBuffer atomBuffer = new ComputeBuffer(proteinAtoms.Length, atomStride);
            atomBuffer.SetData(proteinAtoms);

            RenderTexture rawHeatmap = new RenderTexture(heatmapSize, heatmapSize, 0, RenderTextureFormat.ARGBHalf, 0);
            rawHeatmap.filterMode = FilterMode.Point;
            rawHeatmap.wrapMode = TextureWrapMode.Clamp;
            rawHeatmap.enableRandomWrite = true;
            rawHeatmap.name = "raw_heatmap" + heatmapSize + "x" + heatmapSize + "x" + heatmapSize;
            rawHeatmap.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            rawHeatmap.volumeDepth = heatmapSize;
            rawHeatmap.Create();

            RenderTexture outHeatmap = new RenderTexture(heatmapSize, heatmapSize, 0, RenderTextureFormat.ARGBHalf, 0);
            outHeatmap.filterMode = FilterMode.Point;
            outHeatmap.wrapMode = TextureWrapMode.Clamp;
            outHeatmap.enableRandomWrite = true;
            outHeatmap.name = "heatmap" + heatmapSize + "x" + heatmapSize + "x" + heatmapSize;
            outHeatmap.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            outHeatmap.volumeDepth = heatmapSize;
            outHeatmap.enableRandomWrite = true;
            outHeatmap.Create();

            Vector3Int stride = new Vector3Int(1, 1, 1);
            Vector3Int padding = new Vector3Int(1, 1, 1);
            Vector3 voxelResolution = new Vector3(0.5f, 0.5f, 0.5f);
            float sparseThreshold = 0.01f; 

            int buildKernelId = sparseConv3DCS.FindKernel("CSBuildHeatmap3D");
            int kernelId = sparseConv3DCS.FindKernel("CSSparseConv3D");
            sparseConv3DCS.SetInt("heatmapSize", heatmapSize);
            sparseConv3DCS.SetInts("kernelSize", config.kernelSize, config.kernelSize, config.kernelSize);
            sparseConv3DCS.SetInts("stride", stride.x, stride.y, stride.z);
            sparseConv3DCS.SetInts("padding", padding.x, padding.y, padding.z);
            sparseConv3DCS.SetFloat("sparseThreshold", sparseThreshold);
            sparseConv3DCS.SetVector("voxelResolution", voxelResolution);
            sparseConv3DCS.SetVector("activeSiteCenter", config.activeSiteCenter);
            sparseConv3DCS.SetFloat("gridSpacing", config.gridSpacing);
            sparseConv3DCS.SetFloat("gridRadius", config.lowPowerMode ? 1.5f : 1.0f);
            sparseConv3DCS.SetInt("atomCount", proteinAtoms.Length);

            
            sparseConv3DCS.SetBuffer(buildKernelId, "atomBuffer", atomBuffer);
            sparseConv3DCS.SetTexture(buildKernelId, "InputHeatmap3D", rawHeatmap);
            sparseConv3DCS.SetTexture(buildKernelId, "OutputHeatmap3D", outHeatmap);
            sparseConv3DCS.SetTexture(buildKernelId, "RawHeatmap3D", rawHeatmap);

            
            sparseConv3DCS.SetBuffer(kernelId, "atomBuffer", atomBuffer);
            sparseConv3DCS.SetTexture(kernelId, "InputHeatmap3D", rawHeatmap);
            sparseConv3DCS.SetTexture(kernelId, "OutputHeatmap3D", outHeatmap);
            sparseConv3DCS.SetTexture(kernelId, "RawHeatmap3D", rawHeatmap);

            
            int threadGroupX = Mathf.CeilToInt(heatmapSize / 8f);
            int threadGroupY = Mathf.CeilToInt(heatmapSize / 8f);
            int threadGroupZ = Mathf.CeilToInt(heatmapSize / 8f);
            sparseConv3DCS.Dispatch(buildKernelId, threadGroupX, threadGroupY, threadGroupZ);
            sparseConv3DCS.Dispatch(kernelId, threadGroupX, threadGroupY, threadGroupZ);

            //while (test && Application.isPlaying)
            //{
            //    sparseConv3DCS.Dispatch(kernelId, threadGroupX, threadGroupY, threadGroupZ);
            //    await UniTask.NextFrame();
            //}
            //HeatmapPixel[] convHeatmap = new HeatmapPixel[pixelCount];
            //outputBuffer.GetData(convHeatmap);



            //Color[] pixels = new Color[pixelCount];
            //for (int y = 0; y < heatmapSize; y++)
            //{
            //    for (int x = 0; x < heatmapSize; x++)
            //    {
            //        int idx = y * heatmapSize + x;
            //        Vector4 feat = convHeatmap[idx].features;
            //        pixels[idx] = new Color(feat.x, feat.y, feat.z, feat.w);
            //    }
            //}
            //heatmapTex.SetPixels(pixels);
            //heatmapTex.Apply();

            
            atomBuffer.Release();
            RenderTexture.Destroy(rawHeatmap);

            return outHeatmap;
        }

        public async UniTask<RenderTexture> RunSparseConvCS3D(Texture3D inputHeatmap, AtomData[] proteinAtoms, ProteinHeatmapConfig config, int heatmapSize)
        {
            int atomStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(AtomData));
            ComputeBuffer atomBuffer = new ComputeBuffer(proteinAtoms.Length, atomStride);
            atomBuffer.SetData(proteinAtoms);

            RenderTexture outHeatmap = new RenderTexture(heatmapSize, heatmapSize, 0, RenderTextureFormat.ARGBHalf, 0);
            outHeatmap.filterMode = FilterMode.Point;
            outHeatmap.wrapMode = TextureWrapMode.Clamp;
            outHeatmap.enableRandomWrite = true;
            outHeatmap.name = "heatmap" + heatmapSize + "x" + heatmapSize + "x" + heatmapSize;
            outHeatmap.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            outHeatmap.volumeDepth = heatmapSize;
            outHeatmap.Create();

            Vector3Int stride = new Vector3Int(1, 1, 1);
            Vector3Int padding = new Vector3Int(1, 1, 1);
            Vector3 voxelResolution = new Vector3(0.5f, 0.5f, 0.5f);
            float sparseThreshold = 0.01f;

            int kernelId = sparseConv3DCS.FindKernel("CSSparseConv3D");
            sparseConv3DCS.SetInt("heatmapSize", heatmapSize);
            sparseConv3DCS.SetInts("kernelSize", config.kernelSize, config.kernelSize, config.kernelSize);
            sparseConv3DCS.SetInts("stride", stride.x, stride.y, stride.z);
            sparseConv3DCS.SetInts("padding", padding.x, padding.y, padding.z);
            sparseConv3DCS.SetFloat("sparseThreshold", sparseThreshold);
            sparseConv3DCS.SetVector("voxelResolution", voxelResolution);
            sparseConv3DCS.SetBuffer(kernelId, "atomBuffer", atomBuffer);
            sparseConv3DCS.SetTexture(kernelId, "InputHeatmap3D", inputHeatmap);
            sparseConv3DCS.SetTexture(kernelId, "OutputHeatmap3D", outHeatmap);
            sparseConv3DCS.SetTexture(kernelId, "RawHeatmap3D", outHeatmap);

            int threadGroupX = Mathf.CeilToInt(heatmapSize / 8f);
            int threadGroupY = Mathf.CeilToInt(heatmapSize / 8f);
            int threadGroupZ = Mathf.CeilToInt(heatmapSize / 8f);
            sparseConv3DCS.Dispatch(kernelId, threadGroupX, threadGroupY, threadGroupZ);

            atomBuffer.Release();
            Texture3D.Destroy(inputHeatmap);
            return outHeatmap;
        }
        #endregion

        #region Helper: Visualization
        private void VisualizeHeatmap(Texture2D heatmapTex, ProteinHeatmapConfig config)
        {
            
            GameObject heatmapPlane = new GameObject($"{config.proteinName}_Heatmap");
            heatmapPlane.transform.position = new Vector3(0, 1, 0);
            heatmapPlane.transform.localScale = new Vector3(
                config.heatmapSize * heatmapPlaneScale,
                1,
                config.heatmapSize * heatmapPlaneScale
            );

            
            MeshRenderer renderer = heatmapPlane.AddComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Unlit/Texture"));
            mat.SetTexture("_MainTex", heatmapTex);
            renderer.material = mat;
            //HeatmapTouchScaler scaler = heatmapPlane.AddComponent<HeatmapTouchScaler>();
            //scaler.minScale = config.lowPowerMode ? 0.3f : 0.5f;
            //scaler.maxScale = config.lowPowerMode ? 1.5f : 2.0f;

            Debug.Log($"Heatmap generation status");
        }
        #endregion

        #region Utility: Safe float parsing and atom feature checks
        private float ParseFloatSafe(string line, int startIdx, int length)
        {
            if (startIdx + length > line.Length) return 0f;
            string valStr = line.Substring(startIdx, length).Trim();
            return float.TryParse(valStr, out float val) ? val : 0f;
        }
        private int GetHybridizationByAtomType(AtomType type)
        {
            return type switch
            {
                AtomType.C => 3, // sp3
                AtomType.N => 2, // sp2
                AtomType.O => 2, // sp2
                _ => 2
            };
        }
        private int GetBondDegreeByAtomType(AtomType type)
        {
            return type switch
            {
                AtomType.C => 4,
                AtomType.N => 3,
                AtomType.O => 2,
                AtomType.S => 2,
                AtomType.H => 1,
                _ => 1
            };
        }
        private bool IsHydrophobic(int atomicNum)
        {
            AtomType type = (AtomType)atomicNum;
            return type == AtomType.C || type == AtomType.S || type == AtomType.F ||
                   type == AtomType.Cl || type == AtomType.Br || type == AtomType.I;
        }
        private bool IsHydrogenBond(int atomicNum)
        {
            AtomType type = (AtomType)atomicNum;
            return type == AtomType.N || type == AtomType.O;
        }
        #endregion
    }

    
    public class HeatmapTouchScaler : MonoBehaviour
    {
        public float minScale = 0.5f;
        public float maxScale = 2.0f;
        public float scaleSpeed = 0.1f;

        void Update()
        {
            
            if (Input.mouseScrollDelta.y != 0)
            {
                float scale = Mathf.Clamp(
                    transform.localScale.x + Input.mouseScrollDelta.y * scaleSpeed,
                    minScale, maxScale
                );
                transform.localScale = new Vector3(scale, 1, scale);
            }

            
            if (Input.touchCount == 2)
            {
                Touch t0 = Input.GetTouch(0);
                Touch t1 = Input.GetTouch(1);

                float prevDist = Vector2.Distance(t0.position - t0.deltaPosition, t1.position - t1.deltaPosition);
                float currDist = Vector2.Distance(t0.position, t1.position);
                float delta = (currDist - prevDist) * 0.001f;

                float scale = Mathf.Clamp(transform.localScale.x + delta, minScale, maxScale);
                transform.localScale = new Vector3(scale, 1, scale);
            }
        }
    }


}
