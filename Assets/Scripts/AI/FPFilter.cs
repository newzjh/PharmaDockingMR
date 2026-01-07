using UnityEngine;
using System.Collections.Generic;
using System;

namespace AIDrugDiscovery
{

    // 指纹筛选配置
    [Serializable]
    public class FPFilterConfig
    {
        public float minSimilarity = 0.6f; // 最低相似性阈值
        public int topK = 100; // 筛选Top-K高相似分子
        public bool lowPowerMode = false; // 移动端低功耗模式
    }

    // 分子筛选结果（关联SMILES索引与相似性）
    public struct MolFPResult
    {
        public int molIndex; // 分子在批次中的索引
        public float maxSimilarity; // 与参考库的最大相似性
    }

    public class FPFilter : MonoBehaviour
    {
        [Header("核心组件")]
        public ComputeShader similarityCS; // 相似性计算Compute Shader
        public FPFilterConfig filterConfig;

        /// <summary>
        /// 基于指纹Buffer筛选分子
        /// </summary>
        /// <param name="genFPBuffer">生成分子的指纹Buffer</param>
        /// <param name="refFPBuffer">参考分子库的指纹Buffer</param>
        /// <param name="genCount">生成分子数量</param>
        /// <param name="refCount">参考分子数量</param>
        /// <param name="fpSize">指纹长度（512/256）</param>
        /// <returns>筛选后的分子索引与相似性</returns>
        public List<MolFPResult> FilterByFP(ComputeBuffer genFPBuffer, ComputeBuffer refFPBuffer,
            int genCount, int refCount, int fpSize)
        {
            List<MolFPResult> filteredResults = new List<MolFPResult>();

            // 1. 参数校验
            if (genFPBuffer == null || refFPBuffer == null || genCount == 0 || refCount == 0)
            {
                Debug.LogError("指纹Buffer或分子数量为空，无法筛选");
                return filteredResults;
            }

            // 2. 创建相似性输出Buffer（存储每个生成分子的最大相似性）
            ComputeBuffer similarityBuffer = new ComputeBuffer(genCount, sizeof(float));
            float[] initSimilarity = new float[genCount];
            similarityBuffer.SetData(initSimilarity);

            // 3. 配置相似性计算Compute Shader
            int kernelId = similarityCS.FindKernel("CSComputeMaxSimilarity");
            similarityCS.SetInt("genCount", genCount);
            similarityCS.SetInt("refCount", refCount);
            similarityCS.SetInt("fpSize", fpSize);
            similarityCS.SetFloat("minSimilarity", filterConfig.minSimilarity);

            // 绑定Buffer
            similarityCS.SetBuffer(kernelId, "generatedFP", genFPBuffer);
            similarityCS.SetBuffer(kernelId, "referenceFP", refFPBuffer);
            similarityCS.SetBuffer(kernelId, "maxSimilarityOutput", similarityBuffer);

            // 4. 调度GPU计算（线程组适配移动端）
            int threadGroupX = Mathf.CeilToInt(genCount / 32f);
            similarityCS.Dispatch(kernelId, threadGroupX, 1, 1);

            // 5. 读取相似性结果（仅回读相似性值，不回读完整指纹）
            float[] similarityResults = new float[genCount];
            similarityBuffer.GetData(similarityResults);

            // 6. CPU端筛选与排序
            List<MolFPResult> tempResults = new List<MolFPResult>();
            for (int i = 0; i < genCount; i++)
            {
                if (similarityResults[i] >= filterConfig.minSimilarity)
                {
                    tempResults.Add(new MolFPResult
                    {
                        molIndex = i,
                        maxSimilarity = similarityResults[i]
                    });
                }
            }

            // 7. 按相似性降序排序，取Top-K
            tempResults.Sort((a, b) => b.maxSimilarity.CompareTo(a.maxSimilarity));
            int takeCount = Mathf.Min(filterConfig.topK, tempResults.Count);
            filteredResults.AddRange(tempResults.GetRange(0, takeCount));

            // 8. 释放资源
            similarityBuffer.Release();

            Debug.Log($"指纹筛选完成：共{genCount}个分子 → 筛选出{filteredResults.Count}个高相似分子");
            return filteredResults;
        }

        /// <summary>
        /// 从SMILES Buffer中提取筛选后的分子SMILES
        /// </summary>
        public List<string> GetFilteredSmiles(ComputeBuffer smilesBuffer, List<MolFPResult> filteredResults, int smilesMaxLength)
        {
            List<string> filteredSmiles = new List<string>();
            int stride = smilesMaxLength * sizeof(char);

            // 读取所有SMILES（仅在筛选后读取，减少数据量）
            char[][] allSmiles = new char[smilesBuffer.count][];
            for (int i = 0; i < smilesBuffer.count; i++)
            {
                allSmiles[i] = new char[smilesMaxLength];
            }
            smilesBuffer.GetData(allSmiles);

            // 提取筛选后的SMILES
            foreach (var result in filteredResults)
            {
                string smiles = new string(allSmiles[result.molIndex]).TrimEnd('\0');
                if (!string.IsNullOrEmpty(smiles))
                {
                    filteredSmiles.Add(smiles);
                }
            }

            return filteredSmiles;
        }
    }

}