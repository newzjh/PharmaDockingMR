using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using static Microsoft.MixedReality.GraphicsTools.ClippingPrimitive;
using Cysharp.Threading.Tasks;

namespace AIDrugDiscovery
{
    // 原子类型枚举（扩展至通用大分子）
    public enum AtomType
    {
        C, N, O, S, H, P, F, Cl, Br, I, Other
    }

    // 通用大分子热力图配置（可序列化，支持Inspector配置）
    [Serializable]
    public class ProteinHeatmapConfig
    {
        [Header("基础配置")]
        public string proteinName = "1AQ1"; // 受体名称（如3CLpro、EGFR）

        [Header("热力图参数")]
        public int heatmapSize = 32; // 热力图尺寸（便携终端可设16）
        public float gridSpacing = 1.0f; // 网格间距（Å）
        public Vector3 activeSiteCenter = new Vector3(10.5f, 8.2f, 12.7f); // 活性位点中心

        [Header("卷积参数")]
        public int kernelSize = 3; // 卷积核大小
        public int inChannels = 4; // 输入通道数（原子类型/电荷/疏水性/氢键）
        public int outChannels = 4; // 输出通道数

        [Header("便携终端适配")]
        public bool lowPowerMode = false; // 低功耗模式（自动降分辨率）
    }

    public class HeatmapGenerator : MonoBehaviour
    {
        // 复用原有数据结构（确保与CS对齐）
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

        [Header("核心配置")]
        public ComputeShader heatmapConvCS;
        public ComputeShader sparseConv3DCS;
        public List<ProteinHeatmapConfig> proteinConfigs; // 支持多受体配置

        [Header("可视化配置")]
        public bool autoVisualize = true; // 自动可视化热力图
        public float heatmapPlaneScale = 0.1f; // 热力图平面缩放系数

        public async void Begin()
        {
            // 批量生成所有配置的大分子热力图
            foreach (var config in proteinConfigs)
            {
                GenerateProteinHeatmap(config);
            }
        }

        #region 核心函数1：加载通用大分子原子数据（替代LoadAQ1AtomData）
        /// <summary>
        /// 加载任意大分子的PDBQT文件，解析为原子数据
        /// </summary>
        /// <param name="config">热力图配置</param>
        /// <returns>原子数据数组</returns>
        public AtomData[] LoadProteinAtomData(ProteinHeatmapConfig config)
        {
            List<AtomData> atomList = new List<AtomData>();

            string tempfolder = Application.persistentDataPath + "/cachepdb";
            if (Directory.Exists(tempfolder) == false)
            {
                Directory.CreateDirectory(tempfolder);
            }
            string pdbqtFullPath = tempfolder + "/" + config.proteinName + ".pdb";


            // 1. 文件存在性校验（跨平台适配）
            if (!File.Exists(pdbqtFullPath))
            {
                Debug.LogError($"[{config.proteinName}] PDBQT文件不存在：{pdbqtFullPath}\n请检查路径是否正确");
                return atomList.ToArray();
            }

            // 2. 低功耗模式适配（简化解析，仅保留核心原子）
            bool skipHydrogen = config.lowPowerMode;

            // 3. 逐行解析PDBQT
            string[] lines = File.ReadAllLines(pdbqtFullPath);
            int parsedAtomCount = 0;
            int skippedAtomCount = 0;

            foreach (string line in lines)
            {
                // 仅处理原子行
                if (!line.StartsWith("ATOM") && !line.StartsWith("HETATM")) continue;

                try
                {
                    // 提取原子名称（兼容不同PDBQT格式）
                    string atomName = line.Length >= 17 ? line.Substring(12, 4).Trim() : "";
                    if (string.IsNullOrEmpty(atomName)) continue;

                    // 低功耗模式跳过氢原子（减少计算量）
                    if (skipHydrogen && atomName.StartsWith("H"))
                    {
                        skippedAtomCount++;
                        continue;
                    }

                    // 解析原子类型（扩展至卤素等）
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

                    // 解析原子坐标（兼容不同字段长度）
                    float x = ParseFloatSafe(line, 30, 8);
                    float y = ParseFloatSafe(line, 38, 8);
                    float z = ParseFloatSafe(line, 46, 8);
                    Vector3 position = new Vector3(x, y, z);

                    // 解析原子电荷（PDBQT扩展字段，兼容±符号）
                    float charge = ParseFloatSafe(line, 70, 6);
                    int chargeInt = Mathf.RoundToInt(charge * 100); // 放大为整数，避免浮点精度

                    // 解析成键数/杂化态（简化版，可扩展）
                    int hybridization = GetHybridizationByAtomType(atomType);
                    int degree = GetBondDegreeByAtomType(atomType);

                    // 封装原子数据（molId固定为0，单一大分子）
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
                    Debug.LogWarning($"[{config.proteinName}] 解析原子行失败：{line.Substring(0, 50)}... \n错误：{ex.Message}");
                    continue;
                }
            }

            // 日志输出（便于调试）
            string skipLog = skipHydrogen ? $"（低功耗模式跳过{skippedAtomCount}个氢原子）" : "";
            Debug.Log($"[{config.proteinName}] 成功解析原子数据：共{parsedAtomCount}个原子 {skipLog}");
            return atomList.ToArray();
        }
        #endregion

        #region 核心函数2：生成通用大分子热力图（替代GenerateAQ1Heatmap）
        /// <summary>
        /// 生成任意大分子的4通道热力图
        /// </summary>
        /// <param name="config">热力图配置</param>
        /// <returns>热力图纹理（Texture2D<float4>）</returns>
        public async UniTask<Texture2D> GenerateProteinHeatmap(ProteinHeatmapConfig config)
        {
            // 1. 低功耗模式自动降分辨率
            int finalHeatmapSize = config.lowPowerMode ? Mathf.Max(16, config.heatmapSize / 2) : config.heatmapSize;
            Debug.Log($"[{config.proteinName}] 开始生成热力图（尺寸：{finalHeatmapSize}×{finalHeatmapSize}）");

            // 2. 加载原子数据
            AtomData[] proteinAtoms = LoadProteinAtomData(config);
            if (proteinAtoms == null || proteinAtoms.Length == 0)
            {
                Debug.LogError($"[{config.proteinName}] 原子数据为空，终止热力图生成");
                return null;
            }

            // 3. 初始化原始热力图
            int pixelCount = finalHeatmapSize * finalHeatmapSize;
            HeatmapPixel[] rawHeatmap = new HeatmapPixel[pixelCount];

            // 4. 计算每个像素的原子特征
            for (int y = 0; y < finalHeatmapSize; y++)
            {
                for (int x = 0; x < finalHeatmapSize; x++)
                {
                    int idx = y * finalHeatmapSize + x;
                    Vector4 features = Vector4.zero;

                    // 计算像素对应的3D网格中心（基于活性位点）
                    float gridX = config.activeSiteCenter.x + (x - finalHeatmapSize / 2) * config.gridSpacing;
                    float gridZ = config.activeSiteCenter.z + (y - finalHeatmapSize / 2) * config.gridSpacing;
                    Vector3 gridCenter = new Vector3(gridX, config.activeSiteCenter.y, gridZ);

                    // 统计网格内原子特征（半径可配置）
                    float gridRadius = config.lowPowerMode ? 1.5f : 1.0f;
                    int atomInGrid = 0;

                    foreach (var atom in proteinAtoms)
                    {
                        var gridCenter2 = gridCenter;
                        gridCenter2.y = atom.position.y;
                        float distance = Vector3.Distance(atom.position, gridCenter2);
                        if (distance > gridRadius) 
                            continue;

                        atomInGrid++;
                        // 通道1：原子类型（归一化）
                        features.x += (float)atom.atomicNum / (int)AtomType.Other;
                        // 通道2：电荷（归一化到-1~1）
                        features.y += (float)atom.charge / 200f;
                        // 通道3：疏水性（C/S/卤素为疏水）
                        features.z += IsHydrophobic(atom.atomicNum) ? 1 : 0;
                        // 通道4：氢键潜力（N/O为氢键供体/受体）
                        features.w += IsHydrogenBond(atom.atomicNum) ? 1 : 0;
                    }

                    // 特征平均化
                    if (atomInGrid > 0) features /= atomInGrid;
                    rawHeatmap[idx] = new HeatmapPixel { features = features };
                }
            }

            // 5. 调用CS执行稀疏卷积
            Texture2D heatmapTex = await RunSparseConvCS(rawHeatmap, proteinAtoms, config, finalHeatmapSize);

            // 6. 自动可视化
            if (autoVisualize && heatmapTex != null)
            {
                VisualizeHeatmap(heatmapTex, config);
            }

            return heatmapTex;
        }

        public async UniTask<RenderTexture> GenerateProteinHeatmap3D(ProteinHeatmapConfig config)
        {
            // 1. 低功耗模式自动降分辨率
            int finalHeatmapSize = config.lowPowerMode ? Mathf.Max(16, config.heatmapSize / 2) : config.heatmapSize;
            Debug.Log($"[{config.proteinName}] 开始生成热力图（尺寸：{finalHeatmapSize}×{finalHeatmapSize}）");

            // 2. 加载原子数据
            AtomData[] proteinAtoms = LoadProteinAtomData(config);
            if (proteinAtoms == null || proteinAtoms.Length == 0)
            {
                Debug.LogError($"[{config.proteinName}] 原子数据为空，终止热力图生成");
                return null;
            }

            // 3. 初始化原始热力图
            int pixelCount = finalHeatmapSize * finalHeatmapSize * finalHeatmapSize;
            Texture3D rawHeatmap = new Texture3D(finalHeatmapSize, finalHeatmapSize, finalHeatmapSize, TextureFormat.RGBAHalf, false);
            rawHeatmap.filterMode = FilterMode.Point;
            rawHeatmap.wrapMode = TextureWrapMode.Clamp;
            //Vector4[] rawHeatmapPixels = new Vector4[pixelCount];

            // 4. 计算每个像素的原子特征
            for (int z = 0; z < finalHeatmapSize; z++)
            {
                for (int y = 0; y < finalHeatmapSize; y++)
                {
                    for (int x = 0; x < finalHeatmapSize; x++)
                    {
                        int idx = z * finalHeatmapSize * finalHeatmapSize + y * finalHeatmapSize + x;
                        Color features = Color.black;
                        features.a = 0;

                        // 计算像素对应的3D网格中心（基于活性位点）
                        float gridX = config.activeSiteCenter.x + (x - finalHeatmapSize / 2) * config.gridSpacing;
                        float gridY = config.activeSiteCenter.y + (y - finalHeatmapSize / 2) * config.gridSpacing;
                        float gridZ = config.activeSiteCenter.z + (z - finalHeatmapSize / 2) * config.gridSpacing;
                        Vector3 gridCenter = new Vector3(gridX, gridY, gridZ);

                        // 统计网格内原子特征（半径可配置）
                        float gridRadius = config.lowPowerMode ? 1.5f : 1.0f;
                        int atomInGrid = 0;

                        foreach (var atom in proteinAtoms)
                        {
                            float distance = Vector3.Distance(atom.position, gridCenter);
                            if (distance > gridRadius)
                                continue;

                            atomInGrid++;
                            // 通道1：原子类型（归一化）
                            features.r += (float)atom.atomicNum / (int)AtomType.Other;
                            // 通道2：电荷（归一化到-1~1）
                            features.g += (float)atom.charge / 200f;
                            // 通道3：疏水性（C/S/卤素为疏水）
                            features.b += IsHydrophobic(atom.atomicNum) ? 1 : 0;
                            // 通道4：氢键潜力（N/O为氢键供体/受体）
                            features.a += IsHydrogenBond(atom.atomicNum) ? 1 : 0;
                        }

                        // 特征平均化
                        if (atomInGrid > 0) features /= atomInGrid;
                        //rawHeatmapPixels[idx] = features;
                        rawHeatmap.SetPixel(x, y, z, features);
                    }
                }
            }
            rawHeatmap.Apply();

            //rawHeatmap.SetPixelData<Vector4>(rawHeatmapPixels, 0);

            // 5. 调用CS执行稀疏卷积
            RenderTexture heatmapTex = await RunSparseConvCS3D(rawHeatmap, proteinAtoms, config, finalHeatmapSize);


            return heatmapTex;
        }

        #endregion

        #region 辅助函数：CS稀疏卷积执行
        public async UniTask<Texture2D> RunSparseConvCS(HeatmapPixel[] rawHeatmap, AtomData[] proteinAtoms, ProteinHeatmapConfig config, int heatmapSize)
        {
            int pixelCount = heatmapSize * heatmapSize;

            // 1. 创建Compute Buffer
            int atomStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(AtomData));
            ComputeBuffer atomBuffer = new ComputeBuffer(proteinAtoms.Length, atomStride);
            atomBuffer.SetData(proteinAtoms);

            int heatmapStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(HeatmapPixel));
            ComputeBuffer inputBuffer = new ComputeBuffer(pixelCount, heatmapStride);
            inputBuffer.SetData(rawHeatmap);

            ComputeBuffer outputBuffer = new ComputeBuffer(pixelCount, heatmapStride);
            outputBuffer.SetData(new HeatmapPixel[pixelCount]);

            // 2. 初始化卷积核权重（平均卷积）
            float[] kernelWeights = new float[config.kernelSize * config.kernelSize * config.inChannels * config.outChannels];
            float weightVal = 1f / (config.kernelSize * config.kernelSize);
            for (int i = 0; i < kernelWeights.Length; i++) kernelWeights[i] = weightVal;

            ComputeBuffer kernelBuffer = new ComputeBuffer(kernelWeights.Length, sizeof(float));
            kernelBuffer.SetData(kernelWeights);

            // 3. 配置CS参数
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

            // 4. 调度CS（适配线程组）
            int threadGroupX = Mathf.CeilToInt(heatmapSize / 32f);
            int threadGroupY = Mathf.CeilToInt(heatmapSize / 32f);
            heatmapConvCS.Dispatch(kernelId, threadGroupX, threadGroupY, 1);

            // 5. 读取输出并转换为Texture2D
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

            // 6. 释放Buffer
            atomBuffer.Release();
            inputBuffer.Release();
            outputBuffer.Release();
            kernelBuffer.Release();

            return heatmapTex;
        }

        public bool test = true;
        public async UniTask<RenderTexture> RunSparseConvCS3D(Texture3D inputHeatmap, AtomData[] proteinAtoms, ProteinHeatmapConfig config, int heatmapSize)
        {

            // 1. 创建Compute Buffer
            int atomStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(AtomData));
            ComputeBuffer atomBuffer = new ComputeBuffer(proteinAtoms.Length, atomStride);
            atomBuffer.SetData(proteinAtoms);

            RenderTexture outHeatmap = new RenderTexture(heatmapSize, heatmapSize, 0, RenderTextureFormat.ARGBHalf, 0);
            outHeatmap.filterMode = FilterMode.Point;
            outHeatmap.wrapMode = TextureWrapMode.Clamp;
            outHeatmap.enableRandomWrite = true;
            outHeatmap.name = "heatmap" + heatmapSize + "x" + heatmapSize + "x" + heatmapSize; ;
            outHeatmap.wrapMode = TextureWrapMode.Clamp;
            outHeatmap.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            outHeatmap.volumeDepth = heatmapSize;
            outHeatmap.enableRandomWrite = true;
            outHeatmap.Create();


            //ComputeBuffer outputBuffer = new ComputeBuffer(pixelCount, heatmapStride);
            //outputBuffer.SetData(new HeatmapPixel[pixelCount]);

            // 2. 初始化卷积核权重（平均卷积）
            float[] kernelWeights = new float[config.kernelSize * config.kernelSize * config.inChannels * config.outChannels];
            float weightVal = 1f / (config.kernelSize * config.kernelSize);
            for (int i = 0; i < kernelWeights.Length; i++) kernelWeights[i] = weightVal;

            ComputeBuffer kernelBuffer = new ComputeBuffer(kernelWeights.Length, sizeof(float));
            kernelBuffer.SetData(kernelWeights);

            // 3. 配置CS参数
            //int kernelId = heatmapConvCS.FindKernel("CSSparseConv");
            //heatmapConvCS.SetInt("heatmapSize", heatmapSize);
            //heatmapConvCS.SetInt("kernelSize", config.kernelSize);
            //heatmapConvCS.SetFloat("padding", 1f);
            //heatmapConvCS.SetFloat("stride", 1f);
            //heatmapConvCS.SetInt("inChannels", config.inChannels);
            //heatmapConvCS.SetInt("outChannels", config.outChannels);

            //heatmapConvCS.SetBuffer(kernelId, "heatmapInput", inputBuffer);
            //heatmapConvCS.SetBuffer(kernelId, "kernelWeights", kernelBuffer);
            //heatmapConvCS.SetBuffer(kernelId, "heatmapOutput", outputBuffer);
            //heatmapConvCS.SetBuffer(kernelId, "atomBuffer", atomBuffer);

            Vector3Int stride = new Vector3Int(1, 1, 1);
            Vector3Int padding = new Vector3Int(1, 1, 1);
            Vector3 voxelResolution = new Vector3(0.5f, 0.5f, 0.5f);
            float sparseThreshold = 0.01f; // 基于活性值的稀疏阈值

            int kernelId = sparseConv3DCS.FindKernel("CSSparseConv3D");
            sparseConv3DCS.SetInts("kernelSize", config.kernelSize, config.kernelSize, config.kernelSize);
            sparseConv3DCS.SetInts("stride", stride.x, stride.y, stride.z);
            sparseConv3DCS.SetInts("padding", padding.x, padding.y, padding.z);
            sparseConv3DCS.SetFloat("sparseThreshold", sparseThreshold);
            sparseConv3DCS.SetVector("voxelResolution", voxelResolution);
            //sparseConv3DCS.SetVector("activeSiteCenter", activeSiteCenter);

            // 4. 绑定输入输出Texture3D（float4特征）
            sparseConv3DCS.SetTexture(kernelId, "InputHeatmap3D", inputHeatmap);
            sparseConv3DCS.SetTexture(kernelId, "OutputHeatmap3D", outHeatmap);

            // 4. 调度CS（适配线程组）
            int threadGroupX = Mathf.CeilToInt(heatmapSize / 8f);
            int threadGroupY = Mathf.CeilToInt(heatmapSize / 8f);
            int threadGroupZ = Mathf.CeilToInt(heatmapSize / 8f);
            sparseConv3DCS.Dispatch(kernelId, threadGroupX, threadGroupY, threadGroupZ);

            //while (test && Application.isPlaying)
            //{
            //    sparseConv3DCS.Dispatch(kernelId, threadGroupX, threadGroupY, threadGroupZ);
            //    await UniTask.NextFrame();
            //}



            //// 5. 读取输出并转换为Texture2D
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

            // 6. 释放Buffer
            atomBuffer.Release();
            Texture3D.Destroy(inputHeatmap);
            //outputBuffer.Release();
            kernelBuffer.Release();

            return outHeatmap;
        }
        #endregion

        #region 辅助函数：可视化（通用适配）
        private void VisualizeHeatmap(Texture2D heatmapTex, ProteinHeatmapConfig config)
        {
            // 创建唯一名称的热力图平面
            GameObject heatmapPlane = new GameObject($"{config.proteinName}_Heatmap");
            heatmapPlane.transform.position = new Vector3(0, 1, 0);
            heatmapPlane.transform.localScale = new Vector3(
                config.heatmapSize * heatmapPlaneScale,
                1,
                config.heatmapSize * heatmapPlaneScale
            );

            // 添加渲染组件
            MeshRenderer renderer = heatmapPlane.AddComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Unlit/Texture"));
            mat.SetTexture("_MainTex", heatmapTex);
            renderer.material = mat;

            //// 添加触控缩放（便携终端适配）
            //HeatmapTouchScaler scaler = heatmapPlane.AddComponent<HeatmapTouchScaler>();
            //scaler.minScale = config.lowPowerMode ? 0.3f : 0.5f;
            //scaler.maxScale = config.lowPowerMode ? 1.5f : 2.0f;

            Debug.Log($"[{config.proteinName}] 热力图已可视化，尺寸：{heatmapTex.width}×{heatmapTex.height}");
        }
        #endregion

        #region 工具函数：安全解析浮点值、原子特征判断
        /// <summary>
        /// 安全解析字符串中的浮点值（避免索引越界）
        /// </summary>
        private float ParseFloatSafe(string line, int startIdx, int length)
        {
            if (startIdx + length > line.Length) return 0f;
            string valStr = line.Substring(startIdx, length).Trim();
            return float.TryParse(valStr, out float val) ? val : 0f;
        }

        /// <summary>
        /// 根据原子类型判断杂化态（简化版）
        /// </summary>
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

        /// <summary>
        /// 根据原子类型判断成键数（简化版）
        /// </summary>
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

        /// <summary>
        /// 判断原子是否疏水
        /// </summary>
        private bool IsHydrophobic(int atomicNum)
        {
            AtomType type = (AtomType)atomicNum;
            return type == AtomType.C || type == AtomType.S || type == AtomType.F ||
                   type == AtomType.Cl || type == AtomType.Br || type == AtomType.I;
        }

        /// <summary>
        /// 判断原子是否参与氢键
        /// </summary>
        private bool IsHydrogenBond(int atomicNum)
        {
            AtomType type = (AtomType)atomicNum;
            return type == AtomType.N || type == AtomType.O;
        }
        #endregion
    }

    // 通用触控缩放组件（适配便携终端）
    public class HeatmapTouchScaler : MonoBehaviour
    {
        public float minScale = 0.5f;
        public float maxScale = 2.0f;
        public float scaleSpeed = 0.1f;

        void Update()
        {
            // 桌面端：鼠标滚轮
            if (Input.mouseScrollDelta.y != 0)
            {
                float scale = Mathf.Clamp(
                    transform.localScale.x + Input.mouseScrollDelta.y * scaleSpeed,
                    minScale, maxScale
                );
                transform.localScale = new Vector3(scale, 1, scale);
            }

            // 移动端：双指缩放
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