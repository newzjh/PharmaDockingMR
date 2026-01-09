using UnityEngine;
using System;
using Cysharp.Threading.Tasks;
using System.Collections;


namespace AIDrugDiscovery
{

    /// <summary>
    /// 从Diffusion生成的SMILES Buffer生成512位Morgan指纹
    /// </summary>
    public class MorganFPGenerator : MonoBehaviour
    {
        [Header("核心配置")]
        public ComputeShader morganFPComputeShader; // 绑定上述Compute Shader
        public int smilesMaxLength = 256;           // 单个SMILES最大长度（需与Diffusion一致）
        public int morganRadius = 2;                // Morgan指纹半径（行业标准为2）
        private const int FP_SIZE = 512;            // 固定512位指纹

        private int[] allFP = null;

        /// <summary>
        /// 生成512位指纹（全程GPU端处理，无SMILES回读）
        /// </summary>
        /// <param name="smilesBuffer">Diffusion输出的SMILES Buffer</param>
        /// <param name="batchSize">分子批次数量</param>
        /// <returns>512位指纹Buffer（可直接用于FilterByFP）</returns>
        public async UniTask<ComputeBuffer> Generate512BitFP(Texture smilesTexture, int batchSize)
        {
            // 1. 参数校验
            if (smilesTexture == null || batchSize <= 0)
            {
                Debug.LogError("SMILES Buffer无效或批次大小不匹配");
                return null;
            }

            // 2. 创建指纹输出Buffer（每个分子512个bool，batchSize * 512长度）
            int fpBufferCount = batchSize * FP_SIZE;
            ComputeBuffer fpBuffer = new ComputeBuffer(fpBufferCount, sizeof(int));

            // 3. 初始化指纹Buffer为全false
            allFP = new int[fpBufferCount];
            Array.Fill(allFP, 0);
            fpBuffer.SetData(allFP);

            // 4. 配置Compute Shader参数
            int kernelId = morganFPComputeShader.FindKernel("CSGenerateMorganFP");
            morganFPComputeShader.SetInt("batchSize", batchSize);
            morganFPComputeShader.SetInt("smilesMaxLength", smilesMaxLength);
            morganFPComputeShader.SetInt("morganRadius", morganRadius);

            // 5. 绑定输入输出Buffer
            morganFPComputeShader.SetTexture(kernelId, "smilesInputTexture", smilesTexture);
            //morganFPComputeShader.SetBuffer(kernelId, "smilesInputBuffer", smilesBuffer);
            morganFPComputeShader.SetBuffer(kernelId, "fpOutputBuffer", fpBuffer);

            // 6. 调度GPU计算（线程组适配移动端）
            int threadGroupX = Mathf.CeilToInt(batchSize / 32f); // 32线程/组
            morganFPComputeShader.Dispatch(kernelId, threadGroupX, 1, 1);

            // 7. 等待GPU计算完成（移动端必须，避免数据未写入就读取）
            //ComputeShader.SyncThread();
            fpBuffer.GetData(allFP);

            Debug.Log($"512位指纹生成完成：批次大小={batchSize}，指纹Buffer长度={fpBufferCount}");
            return fpBuffer;
        }

        /// <summary>
        /// （可选）读取指纹Buffer到CPU（仅用于调试/验证）
        /// </summary>
        /// <param name="fpBuffer">指纹Buffer</param>
        /// <param name="molIdx">分子索引</param>
        /// <returns>该分子的512位指纹数组</returns>
        public BitArray GetFPFromBuffer(int molIdx)
        {
            if (allFP == null || molIdx >= allFP.Length / FP_SIZE)
            {
                Debug.LogError("指纹Buffer无效或分子索引越界");
                return null;
            }

            BitArray bits = new BitArray(FP_SIZE);
            for (int i = 0; i < FP_SIZE; i++)
            {
                bits.Set(i, allFP[molIdx * FP_SIZE + i] > 0);
            }

            return bits;
        }


        private void OnDestroy()
        {
            // 兜底释放（防止遗漏）
            morganFPComputeShader = null;
        }
    }

}