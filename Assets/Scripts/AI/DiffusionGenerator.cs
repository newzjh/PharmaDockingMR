using UnityEngine;
using System.Collections.Generic;
using System;
using Mirror.BouncyCastle.Asn1.Mozilla;
using Cysharp.Threading.Tasks;
using UnityEngine.Rendering;

namespace AIDrugDiscovery
{
    // 通用扩散模型配置（可序列化，支持Inspector配置多受体）
    [Serializable]
    public class ProteinDiffusionConfig
    {
        [Header("基础配置")]
        public string proteinName = "1AQ1"; // 受体名称（如3CLpro、EGFR）

        [Header("扩散核心参数")]
        public int batchSize = 1024; // 单次生成分子数量
        public int timesteps = 1000; // 扩散时间步
        public float betaStart = 0.0001f; // 噪声初始强度
        public float betaEnd = 0.02f; // 噪声最终强度

        [Header("靶向约束参数")]
        public float heatmapWeight = 0.8f; // 热力图约束权重
        public int maxAtomLimit = 60; // 分子最大原子数
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


        public async void Begin(Texture2D heatmap, RenderTexture heatmap3D)
        {
            foreach (var config in diffusionConfigs)
            {
                  await GenerateProteinTargetedMols(config, heatmap, heatmap3D, 0);
            }
        }

        public bool test = true;

        // 通用大分子靶向分子生成函数
        
        public async UniTask<ValueTuple<List<string>, List<int>, RenderTexture>> GenerateProteinTargetedMols(ProteinDiffusionConfig config, Texture2D proteinHeatmap, RenderTexture proteinHeatmap3D, int batchOffset)
        {
            List<string> generatedSmiles = new List<string>();
            List<int> generatedIndices = new List<int>();

            // 1. 参数校验
            if (proteinHeatmap == null)
            {
                Debug.LogError($"[{config.proteinName}] 热力图为空，无法生成靶向分子");
                return (generatedSmiles, generatedIndices, null);
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
            Vector4 atomWeightBuffer = new Vector4(config.cWeight, config.oWeight, config.nWeight, config.sWeight);

            // 4.3 活性中心Buffer（传递大分子活性位点坐标）

            // 4.4 SMILES输出Buffer（每个分子256字符）
            int smilesStride = SMILES_MAX_LENGTH * sizeof(int);
            ComputeBuffer smilesBuffer = new ComputeBuffer(effectiveBatchSize, smilesStride);
            int[] initSmiles = new int[effectiveBatchSize * SMILES_MAX_LENGTH];
            smilesBuffer.SetData(initSmiles);

            RenderTexture smilesTexture = new RenderTexture(SMILES_MAX_LENGTH, effectiveBatchSize, 0, UnityEngine.Experimental.Rendering.GraphicsFormat.R16_SFloat);
            smilesTexture.enableRandomWrite = true;

            // 5. 配置Compute Shader参数
            int kernelId = diffusionCS.FindKernel("CSForwardDiffusion");
            diffusionCS.SetInt("batchSize", effectiveBatchSize);
            diffusionCS.SetInt("batchOffset", batchOffset);
            diffusionCS.SetInt("timesteps", effectiveTimesteps);
            diffusionCS.SetFloat("heatmapWeight", config.heatmapWeight);
            diffusionCS.SetInt("maxAtoms", config.maxAtomLimit);
            diffusionCS.SetFloat("minFeatureScore", config.minFeatureScore);
            diffusionCS.SetInt("heatmapSize", proteinHeatmap.width); // 传递热力图尺寸

            // 绑定纹理和Buffer
            diffusionCS.SetTexture(kernelId, "proteinHeatmap", proteinHeatmap);
            diffusionCS.SetTexture(kernelId, "proteinHeatmap3D", proteinHeatmap3D);
            diffusionCS.SetBuffer(kernelId, "betaBuffer", betaBuffer);
            diffusionCS.SetBuffer(kernelId, "alphaCumprodBuffer", alphaCumprodBuffer);
            diffusionCS.SetVector("atomWeightBuffer", atomWeightBuffer);
            diffusionCS.SetVector("activeCenterBuffer", config.proteinActiveCenter);
            diffusionCS.SetBuffer(kernelId, "smilesOutputBuffer", smilesBuffer);
            diffusionCS.SetTexture(kernelId, "smilesOutputTexture", smilesTexture);

            // 创建Debug Buffer
            ComputeBuffer matchScoreDebugBuffer = new ComputeBuffer(effectiveBatchSize * config.maxAtomLimit, sizeof(int));
            // 绑定到CS
            diffusionCS.SetBuffer(kernelId, "matchScoreDebugBuffer", matchScoreDebugBuffer);

            // 6. 调度CS（线程组适配batchSize）
            int threadGroupX = Mathf.CeilToInt(effectiveBatchSize / 64f);
            diffusionCS.Dispatch(kernelId, threadGroupX, 1, 1);
            //while (test && Application.isPlaying)
            //{
            //    diffusionCS.Dispatch(kernelId, threadGroupX, 1, 1);
            //    await UniTask.NextFrame();
            //}

            float[] scores = new float[effectiveBatchSize * config.maxAtomLimit];
            {
                var req = await AsyncGPUReadback.RequestAsync(matchScoreDebugBuffer);
                scores = req.GetData<float>().ToArray();
            }
            //matchScoreDebugBuffer.GetData(scores);
            for (int i = 0; i < effectiveBatchSize; i++)
            {
                float avgScore = 0;
                int count = 0;
                for (int a = 0; a < config.maxAtomLimit; a++)
                {
                    float s = scores[i * config.maxAtomLimit + a];
                    if (s > 0)
                    {
                        avgScore += s;
                        count++;
                    }
                }
                if (count > 0)
                    avgScore /= (float)count;
                //if (avgScore > 0)
                //    Debug.Log($"分子{i}平均匹配分数：{avgScore}");
            }

            // 7. 读取并解析SMILES结果
            char[][] resultChars = new char[effectiveBatchSize][];
            for (int i = 0; i < effectiveBatchSize; i++)
            {
                resultChars[i] = new char[SMILES_MAX_LENGTH];
            }
            {
                var req = await AsyncGPUReadback.RequestAsync(smilesBuffer);
                initSmiles = req.GetData<int>().ToArray();
            }
            //smilesBuffer.GetData(initSmiles);
            for (int i = 0; i < effectiveBatchSize; i++)
            {
                for (int j = 0; j < SMILES_MAX_LENGTH; j++)
                    resultChars[i][j] = (char)initSmiles[i* SMILES_MAX_LENGTH + j];
            }

            int index = 0;
            foreach (var chars in resultChars)
            {
                string smiles = new string(chars).TrimEnd('\0');
                if (!string.IsNullOrEmpty(smiles) && smiles.Length >= 3)
                {
                    generatedSmiles.Add(smiles);
                    generatedIndices.Add(index);
                }
                index++;
            }

            // 8. 释放所有Buffer（避免内存泄漏）
            betaBuffer.Release();
            alphaCumprodBuffer.Release();
            smilesBuffer.Release();

            Debug.Log($"[{config.proteinName}] 靶向分子生成完成：共生成{generatedSmiles.Count}个有效SMILES");
            return (generatedSmiles, generatedIndices, smilesTexture);
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