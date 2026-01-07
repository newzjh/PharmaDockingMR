using UnityEngine;
using System.Collections.Generic;
using System;
using Mirror.BouncyCastle.Asn1.Mozilla;

namespace AIDrugDiscovery
{
    // 通用扩散模型配置（可序列化，支持Inspector配置多受体）
    [Serializable]
    public class ProteinDiffusionConfig
    {
        [Header("基础配置")]
        public string proteinName = "1AQ1"; // 受体名称（如3CLpro、EGFR）

        [Header("扩散核心参数")]
        public int batchSize = 100; // 单次生成分子数量
        public int timesteps = 1000; // 扩散时间步
        public float betaStart = 0.0001f; // 噪声初始强度
        public float betaEnd = 0.02f; // 噪声最终强度

        [Header("靶向约束参数")]
        public float heatmapWeight = 0.8f; // 热力图约束权重
        public int maxAtoms = 50; // 分子最大原子数
        public float minFeatureScore = 0.3f; // 最小特征匹配分数（替代疏水占比）
        public Vector3 proteinActiveCenter = new Vector3(10.5f, 8.2f, 12.7f); // 大分子活性中心

        [Header("原子类型偏好（适配不同受体）")]
        public float cWeight = 0.4f; // C原子权重
        public float oWeight = 0.3f; // O原子权重
        public float nWeight = 0.2f; // N原子权重
        public float sWeight = 0.1f; // S原子权重

        [Header("性能适配")]
        public bool lowPowerMode = false; // 便携终端低功耗模式
    }

    public class DiffusionGenerator : MonoBehaviour
    {
        public const int SMILES_MAX_LENGTH = 256;

        [Header("核心组件")]
        public ComputeShader diffusionCS;
        public List<ProteinDiffusionConfig> diffusionConfigs; // 多受体配置列表

        /// <summary>
        /// 从扩散模型的SMILES Buffer直接计算Morgan指纹，无CPU回读
        /// </summary>
        public ComputeBuffer ComputeFPFromSmilesBuffer(ComputeShader morganCS, ComputeBuffer smilesBuffer, int batchSize, int fpSize = 512)
        {
            // 1. 创建指纹输出Buffer
            int fpStride = sizeof(bool) * fpSize;
            ComputeBuffer fpBuffer = new ComputeBuffer(batchSize, fpStride);

            // 2. 配置MorganFP Compute Shader
            int kernelId = morganCS.FindKernel("CSComputeMorganFPFromSMILES");
            morganCS.SetInt("batchSize", batchSize);
            morganCS.SetInt("fpSize", fpSize);
            morganCS.SetInt("radius", 2);
            morganCS.SetInt("smilesMaxLength", SMILES_MAX_LENGTH);

            // 3. 绑定输入输出Buffer（关键：直接传递SMILES Buffer）
            morganCS.SetBuffer(kernelId, "smilesInputBuffer", smilesBuffer);
            morganCS.SetBuffer(kernelId, "fpOutputBuffer", fpBuffer);

            // 4. 调度GPU计算（线程组适配移动端）
            int threadGroupX = Mathf.CeilToInt(batchSize / 32f);
            morganCS.Dispatch(kernelId, threadGroupX, 1, 1);

            // 5. 指纹Buffer直接用于后续筛选（无需回读CPU）
            return fpBuffer;
        }

        //// 调用示例：扩散生成 → 指纹计算 流水线
        //public void RunDrugDiscoveryPipeline(int batchSize)
        //{
        //    // Step1: 扩散模型生成SMILES Buffer
        //    ComputeBuffer smilesBuffer = CreateSmilesBuffer(batchSize);
        //    RunForwardDiffusion(smilesBuffer); // 执行ForwardDiffusion CS

        //    // Step2: 直接用SMILES Buffer计算指纹（无CPU回读）
        //    ComputeBuffer fpBuffer = ComputeFPFromSmilesBuffer(morganCS, smilesBuffer, batchSize);

        //    // Step3: 基于指纹Buffer进行相似性筛选（可在GPU/CPU端执行）
        //    FilterByFPBuffer(fpBuffer);

        //    // 释放资源
        //    smilesBuffer.Release();
        //    fpBuffer.Release();
        //}

        public async void Begin(Texture2D heatmap)
        {
            foreach (var config in diffusionConfigs)
            {
                  GenerateProteinTargetedMols(config, heatmap);
            }
        }

        // 通用大分子靶向分子生成函数
        public List<string> GenerateProteinTargetedMols(ProteinDiffusionConfig config, Texture2D proteinHeatmap)
        {
            List<string> generatedSmiles = new List<string>();

            // 1. 参数校验
            if (proteinHeatmap == null)
            {
                Debug.LogError($"[{config.proteinName}] 热力图为空，无法生成靶向分子");
                return generatedSmiles;
            }

            // 2. 低功耗模式适配
            int effectiveBatchSize = config.lowPowerMode ? Mathf.RoundToInt(config.batchSize * 0.3f) : config.batchSize;
            int effectiveTimesteps = config.lowPowerMode ? Mathf.RoundToInt(config.timesteps * 0.5f) : config.timesteps;

            // 3. 预计算扩散噪声调度表（beta/alpha/alpha_cumprod）
            float[] betas = new float[effectiveTimesteps];
            float[] alphas = new float[effectiveTimesteps];
            float[] alphaCumprod = new float[effectiveTimesteps];
            ComputeBetaSchedule(betas, alphas, alphaCumprod, effectiveTimesteps, config);

            // 4. 创建Compute Buffer
            // 4.1 噪声调度Buffer
            ComputeBuffer betaBuffer = new ComputeBuffer(effectiveTimesteps, sizeof(float));
            betaBuffer.SetData(betas);
            ComputeBuffer alphaCumprodBuffer = new ComputeBuffer(effectiveTimesteps, sizeof(float));
            alphaCumprodBuffer.SetData(alphaCumprod);

            // 4.2 原子类型权重Buffer（适配不同受体的偏好）
            float[] atomWeights = new float[] { config.cWeight, config.oWeight, config.nWeight, config.sWeight };
            ComputeBuffer atomWeightBuffer = new ComputeBuffer(4, sizeof(float));
            atomWeightBuffer.SetData(atomWeights);

            // 4.3 活性中心Buffer（传递大分子活性位点坐标）
            ComputeBuffer activeCenterBuffer = new ComputeBuffer(1, sizeof(float) * 3);
            activeCenterBuffer.SetData(new Vector3[] { config.proteinActiveCenter });

            // 4.4 SMILES输出Buffer（每个分子256字符）
            int smilesStride = SMILES_MAX_LENGTH * sizeof(int);
            ComputeBuffer smilesBuffer = new ComputeBuffer(effectiveBatchSize, smilesStride);
            int[] initSmiles = new int[effectiveBatchSize * SMILES_MAX_LENGTH];
            smilesBuffer.SetData(initSmiles);

            // 5. 配置Compute Shader参数
            int kernelId = diffusionCS.FindKernel("CSForwardDiffusion");
            diffusionCS.SetInt("batchSize", effectiveBatchSize);
            diffusionCS.SetInt("timesteps", effectiveTimesteps);
            diffusionCS.SetFloat("heatmapWeight", config.heatmapWeight);
            diffusionCS.SetInt("maxAtoms", config.maxAtoms);
            diffusionCS.SetFloat("minFeatureScore", config.minFeatureScore);
            diffusionCS.SetInt("heatmapSize", proteinHeatmap.width); // 传递热力图尺寸

            // 绑定纹理和Buffer
            diffusionCS.SetTexture(kernelId, "proteinHeatmap", proteinHeatmap);
            diffusionCS.SetBuffer(kernelId, "betaBuffer", betaBuffer);
            diffusionCS.SetBuffer(kernelId, "alphaCumprodBuffer", alphaCumprodBuffer);
            diffusionCS.SetBuffer(kernelId, "atomWeightBuffer", atomWeightBuffer);
            diffusionCS.SetBuffer(kernelId, "activeCenterBuffer", activeCenterBuffer);
            diffusionCS.SetBuffer(kernelId, "smilesOutputBuffer", smilesBuffer);

            // 6. 调度CS（线程组适配batchSize）
            int threadGroupX = Mathf.CeilToInt(effectiveBatchSize / 64f);
            diffusionCS.Dispatch(kernelId, threadGroupX, 1, 1);

            // 7. 读取并解析SMILES结果
            char[][] resultChars = new char[effectiveBatchSize][];
            for (int i = 0; i < effectiveBatchSize; i++)
            {
                resultChars[i] = new char[SMILES_MAX_LENGTH];
            }
            smilesBuffer.GetData(initSmiles);
            for (int i = 0; i < effectiveBatchSize; i++)
            {
                for (int j = 0; j < SMILES_MAX_LENGTH; j++)
                    resultChars[i][j] = (char)initSmiles[i* SMILES_MAX_LENGTH + j];
            }

            foreach (var chars in resultChars)
            {
                string smiles = new string(chars).TrimEnd('\0');
                if (!string.IsNullOrEmpty(smiles) && smiles.Length >= 3)
                {
                    generatedSmiles.Add(smiles);
                }
            }

            // 8. 释放所有Buffer（避免内存泄漏）
            betaBuffer.Release();
            alphaCumprodBuffer.Release();
            atomWeightBuffer.Release();
            activeCenterBuffer.Release();
            smilesBuffer.Release();

            Debug.Log($"[{config.proteinName}] 靶向分子生成完成：共生成{generatedSmiles.Count}个有效SMILES");
            return generatedSmiles;
        }

        // 批量生成所有配置的大分子靶向分子
        public void GenerateAllProteinTargetedMols(Dictionary<string, Texture2D> proteinHeatmapDict)
        {
            foreach (var config in diffusionConfigs)
            {
                if (proteinHeatmapDict.TryGetValue(config.proteinName, out var heatmap))
                {
                    GenerateProteinTargetedMols(config, heatmap);
                }
                else
                {
                    Debug.LogError($"[{config.proteinName}] 未找到对应的热力图");
                }
            }
        }

        // 辅助函数：预计算扩散噪声调度表（支持自定义beta参数）
        private void ComputeBetaSchedule(float[] betas, float[] alphas, float[] alphaCumprod, int timesteps, ProteinDiffusionConfig config)
        {
            float betaStep = (config.betaEnd - config.betaStart) / (timesteps - 1);
            alphas[0] = 1 - config.betaStart;
            alphaCumprod[0] = alphas[0];
            betas[0] = config.betaStart;

            for (int t = 1; t < timesteps; t++)
            {
                betas[t] = config.betaStart + betaStep * t;
                alphas[t] = 1 - betas[t];
                alphaCumprod[t] = alphaCumprod[t - 1] * alphas[t];
            }
        }
    }

}