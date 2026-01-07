using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AIDrugDiscovery
{

    // 原子数据结构（与Compute Shader中的AtomData对齐）
    [System.Serializable]
    public struct AtomData
    {
        public Vector3 position;   // 原子坐标
        public int atomicNum;     // 原子序数
        public int charge;        // 电荷
        public int hybridization; // 杂化态（简化为int）
        public int degree;        // 成键数
        public int molId;         // 所属分子ID
    }

    // Morgan指纹结构（与CS中的MorganFP对齐）
    public struct MorganFP
    {
        public uint[] counts; // 1024位Count指纹
        public MorganFP(int length)
        {
            counts = new uint[length];
        }
    }

    // 热力图像素结构（与CS中的HeatmapPixel对齐）
    public struct HeatmapPixel
    {
        public Vector4 features; // 4通道：原子类型/电荷/键长/键角
    }

    public class DrugAIComputeTest : MonoBehaviour
    {
        // 绑定Compute Shader文件
        public ComputeShader morganCS;
        public ComputeShader diffusionForwardCS;
        public ComputeShader heatmapConvCS;

        // 测试参数
        private const int BATCH_SIZE = 2;          // 测试用2个分子
        private const int HEATMAP_SIZE = 32;       // 32×32热力图
        private const int FP_LENGTH = 512;        // Morgan指纹长度
        private const int TOTAL_TIMESTEPS = 1000;  // Diffusion总时间步

        void Start()
        {
            var hg = GameObject.FindFirstObjectByType<HeatmapGenerator>(FindObjectsInactive.Include);
            var dg = GameObject.FindFirstObjectByType<DiffusionGenerator>(FindObjectsInactive.Include);

            foreach (var config in hg.proteinConfigs)
            {
                var heatmap = hg.GenerateProteinHeatmap(config);
                dg.GenerateProteinTargetedMols(dg.diffusionConfigs.First(), heatmap);
            }
            

            //// 步骤1：测试Morgan指纹生成
            //TestMorganFingerprintGeneration();

            //// 步骤2：测试Diffusion前向扩散（噪声添加）
            //TestDiffusionForward();

            // 步骤3：测试热力图稀疏卷积
            //TestHeatmapSparseConv();
        }

        #region 1. Morgan指纹生成测试
        void TestMorganFingerprintGeneration()
        {
            Debug.Log("=== 开始测试512位Morgan指纹生成 ===");

            // 1. 准备测试原子数据（无修改）
            List<AtomData> atomList = new List<AtomData>();
            for (int molId = 0; molId < BATCH_SIZE; molId++)
            {
                for (int atomIdx = 0; atomIdx < 5; atomIdx++)
                {
                    atomList.Add(new AtomData
                    {
                        position = new Vector3(UnityEngine.Random.Range(-5f, 5f), UnityEngine.Random.Range(-5f, 5f), 0),
                        atomicNum = molId == 0 ? 6 : 8,
                        charge = 0,
                        hybridization = 2,
                        degree = 3,
                        molId = molId
                    });
                }
            }
            AtomData[] atomArray = atomList.ToArray();

            // 2. 创建Compute Buffer（核心修改：指纹缓冲区大小）
            int atomStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(AtomData));
            ComputeBuffer atomBuffer = new ComputeBuffer(atomArray.Length, atomStride);
            atomBuffer.SetData(atomArray);

            // 关键修改：512个uint，计算缓冲区步长
            int fpStride = FP_LENGTH * sizeof(uint); // 512 * 4 = 2048字节/指纹
            ComputeBuffer fpBuffer = new ComputeBuffer(BATCH_SIZE, fpStride);
            //MorganFP[] initFP = new MorganFP[BATCH_SIZE];
            //for (int i = 0; i < BATCH_SIZE; i++)
            //{
            //    initFP[i] = new MorganFP(FP_LENGTH); // 初始化512位数组
            //}
            //fpBuffer.SetData(initFP);
            UInt32[] dataFP = new uint[BATCH_SIZE * FP_LENGTH];
            fpBuffer.SetData(dataFP);

            // 3. 设置Compute Shader参数（无修改，fpLength已定义为512）
            int kernelId = morganCS.FindKernel("CSGenerateMorgan");
            morganCS.SetInt("batchSize", BATCH_SIZE);
            morganCS.SetInt("radius", 2);
            morganCS.SetInt("fpLength", FP_LENGTH); // 传入512
            morganCS.SetBuffer(kernelId, "atomBuffer", atomBuffer);
            morganCS.SetBuffer(kernelId, "fpBuffer", fpBuffer);

            // 4. 调度CS（无修改）
            int threadGroupCount = Mathf.CeilToInt(BATCH_SIZE / 64f);
            morganCS.Dispatch(kernelId, threadGroupCount, 1, 1);

            // 5. 读取结果（验证512位指纹）
            //MorganFP[] resultFP = new MorganFP[BATCH_SIZE];
            fpBuffer.GetData(dataFP);

            // 6. 打印512位指纹的非零位数量（验证修改生效）
            for (int molId = 0; molId < BATCH_SIZE; molId++)
            {
                int nonZeroCount = 0;
                // 遍历512位指纹（核心修改：i < 512）
                for (int i = 0; i < FP_LENGTH; i++)
                {
                    if (dataFP[molId * FP_LENGTH + i] > 0) nonZeroCount++;
                }
                Debug.Log($"分子{molId}的512位Morgan指纹非零位数量：{nonZeroCount}");
            }

            // 7. 释放Buffer
            atomBuffer.Release();
            fpBuffer.Release();
        }
        #endregion

        #region 2. Diffusion前向扩散测试（添加噪声）
        void TestDiffusionForward()
        {
            Debug.Log("\n=== 开始测试Diffusion前向扩散 ===");

            // 1. 准备测试热力图数据（2个分子，32×32×4通道）
            int heatmapPixelCount = HEATMAP_SIZE * HEATMAP_SIZE;
            HeatmapPixel[] originHeatmap = new HeatmapPixel[BATCH_SIZE * heatmapPixelCount];
            for (int batchIdx = 0; batchIdx < BATCH_SIZE; batchIdx++)
            {
                for (int y = 0; y < HEATMAP_SIZE; y++)
                {
                    for (int x = 0; x < HEATMAP_SIZE; x++)
                    {
                        int idx = batchIdx * heatmapPixelCount + y * HEATMAP_SIZE + x;
                        // 模拟原子位置（仅中心区域有值）
                        if (x > 10 && x < 22 && y > 10 && y < 22)
                        {
                            originHeatmap[idx] = new HeatmapPixel
                            {
                                features = new Vector4(6, 0, 2, 3) // C原子特征
                            };
                        }
                        else
                        {
                            originHeatmap[idx] = new HeatmapPixel { features = Vector4.zero };
                        }
                    }
                }
            }

            // 2. 准备时间步数据（每个分子随机时间步）
            int[] timesteps = new int[BATCH_SIZE];
            for (int i = 0; i < BATCH_SIZE; i++) timesteps[i] = UnityEngine.Random.Range(100, 900);

            // 3. 创建Compute Buffer
            int heatmapStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(HeatmapPixel));
            ComputeBuffer originHeatmapBuffer = new ComputeBuffer(originHeatmap.Length, heatmapStride);
            originHeatmapBuffer.SetData(originHeatmap);

            ComputeBuffer timestepBuffer = new ComputeBuffer(BATCH_SIZE, sizeof(int));
            timestepBuffer.SetData(timesteps);

            ComputeBuffer noisyHeatmapBuffer = new ComputeBuffer(originHeatmap.Length, heatmapStride);
            noisyHeatmapBuffer.SetData(new HeatmapPixel[originHeatmap.Length]);

            ComputeBuffer noiseBuffer = new ComputeBuffer(originHeatmap.Length, heatmapStride);
            noiseBuffer.SetData(new HeatmapPixel[originHeatmap.Length]);

            // 4. 设置CS参数
            int kernelId = diffusionForwardCS.FindKernel("CSForwardDiffusion");
            diffusionForwardCS.SetInt("batchSize", BATCH_SIZE);
            diffusionForwardCS.SetInt("heatmapSize", HEATMAP_SIZE);
            diffusionForwardCS.SetFloat("betaStart", 1e-4f);
            diffusionForwardCS.SetFloat("betaEnd", 0.02f);
            diffusionForwardCS.SetInt("totalTimesteps", TOTAL_TIMESTEPS);
            diffusionForwardCS.SetBuffer(kernelId, "originHeatmap", originHeatmapBuffer);
            diffusionForwardCS.SetBuffer(kernelId, "timesteps", timestepBuffer);
            diffusionForwardCS.SetBuffer(kernelId, "noisyHeatmap", noisyHeatmapBuffer);
            diffusionForwardCS.SetBuffer(kernelId, "noiseBuffer", noiseBuffer);

            // 5. 调度CS（线程组：32×32×batchSize）
            diffusionForwardCS.Dispatch(kernelId,
                Mathf.CeilToInt(HEATMAP_SIZE / 32f),
                Mathf.CeilToInt(HEATMAP_SIZE / 32f),
                BATCH_SIZE);

            // 6. 读取结果并验证
            HeatmapPixel[] noisyHeatmap = new HeatmapPixel[originHeatmap.Length];
            noisyHeatmapBuffer.GetData(noisyHeatmap);

            // 打印第一个分子中心像素的噪声前后对比
            int testIdx = 0 * heatmapPixelCount + 16 * HEATMAP_SIZE + 16; // 中心像素
            Debug.Log($"原始热力图中心像素特征：{originHeatmap[testIdx].features}");
            Debug.Log($"加噪声后热力图中心像素特征：{noisyHeatmap[testIdx].features}");

            // 7. 释放Buffer
            originHeatmapBuffer.Release();
            timestepBuffer.Release();
            noisyHeatmapBuffer.Release();
            noiseBuffer.Release();
        }
        #endregion

        #region 3. 热力图稀疏卷积测试
        void TestHeatmapSparseConv()
        {
            Debug.Log("\n=== 开始测试热力图稀疏卷积 ===");

            // 1. 准备测试数据
            int heatmapPixelCount = HEATMAP_SIZE * HEATMAP_SIZE;
            Vector4[] originHeatmap = new Vector4[BATCH_SIZE * heatmapPixelCount];
            for (int batchIdx = 0; batchIdx < BATCH_SIZE; batchIdx++)
            {
                for (int y = 0; y < HEATMAP_SIZE; y++)
                {
                    for (int x = 0; x < HEATMAP_SIZE; x++)
                    {
                        int idx = batchIdx * heatmapPixelCount + y * HEATMAP_SIZE + x;
                        // 模拟稀疏数据：仅少量像素有值
                        if (x == 16 && y == 16) originHeatmap[idx] = new Vector4(6, 0, 2, 3);
                        else originHeatmap[idx] = Vector4.zero;
                    }
                }
            }

            // 2. 准备卷积核权重（3×3×4×4，简化为全1）
            int kernelSize = 3;
            int inChannels = 4;
            int outChannels = 4;
            float[] kernelWeights = new float[kernelSize * kernelSize * inChannels * outChannels];
            for (int i = 0; i < kernelWeights.Length; i++) kernelWeights[i] = 1f / 9f; // 平均卷积

            // 3. 创建Compute Buffer
            int heatmapStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector4));
            ComputeBuffer inputBuffer = new ComputeBuffer(originHeatmap.Length, heatmapStride);
            inputBuffer.SetData(originHeatmap);

            ComputeBuffer kernelBuffer = new ComputeBuffer(kernelWeights.Length, sizeof(float));
            kernelBuffer.SetData(kernelWeights);

            ComputeBuffer outputBuffer = new ComputeBuffer(originHeatmap.Length, heatmapStride);
            outputBuffer.SetData(new Vector4[originHeatmap.Length]);

            // 4. 设置CS参数
            int kernelId = heatmapConvCS.FindKernel("CSSparseConv");
            heatmapConvCS.SetInt("batchSize", BATCH_SIZE);
            heatmapConvCS.SetInt("heatmapSize", HEATMAP_SIZE);
            heatmapConvCS.SetInt("kernelSize", kernelSize);
            heatmapConvCS.SetFloat("padding", 1f);
            heatmapConvCS.SetFloat("stride", 1f);
            heatmapConvCS.SetInt("inChannels", inChannels);
            heatmapConvCS.SetInt("outChannels", outChannels);
            heatmapConvCS.SetBuffer(kernelId, "heatmapInput", inputBuffer);
            heatmapConvCS.SetBuffer(kernelId, "kernelWeights", kernelBuffer);
            heatmapConvCS.SetBuffer(kernelId, "heatmapOutput", outputBuffer);

            // 5. 调度CS
            heatmapConvCS.Dispatch(kernelId,
                Mathf.CeilToInt(HEATMAP_SIZE / 32f),
                Mathf.CeilToInt(HEATMAP_SIZE / 32f),
                BATCH_SIZE);

            // 6. 读取结果并验证
            Vector4[] convResult = new Vector4[originHeatmap.Length];
            outputBuffer.GetData(convResult);

            // 打印卷积后中心像素值
            int testIdx = 0 * heatmapPixelCount + 16 * HEATMAP_SIZE + 16;
            Debug.Log($"卷积前中心像素：{originHeatmap[testIdx]}");
            Debug.Log($"卷积后中心像素：{convResult[testIdx]}");

            // 7. 释放Buffer
            inputBuffer.Release();
            kernelBuffer.Release();
            outputBuffer.Release();
        }
        #endregion

        void OnDestroy()
        {
            // 兜底释放所有可能未释放的Buffer（防止内存泄漏）
            Debug.Log("释放所有Compute Buffer");
        }
    }

}