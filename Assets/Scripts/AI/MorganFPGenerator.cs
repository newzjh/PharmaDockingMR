using UnityEngine;
using System;

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

    /// <summary>
    /// 生成512位指纹（全程GPU端处理，无SMILES回读）
    /// </summary>
    /// <param name="smilesBuffer">Diffusion输出的SMILES Buffer</param>
    /// <param name="batchSize">分子批次数量</param>
    /// <returns>512位指纹Buffer（可直接用于FilterByFP）</returns>
    public ComputeBuffer Generate512BitFP(ComputeBuffer smilesBuffer, RenderTexture smilesTexture, int batchSize)
    {
        // 1. 参数校验
        if (smilesBuffer == null || smilesBuffer.count != batchSize)
        {
            Debug.LogError("SMILES Buffer无效或批次大小不匹配");
            return null;
        }

        // 2. 创建指纹输出Buffer（每个分子512个bool，batchSize * 512长度）
        int fpBufferCount = batchSize * FP_SIZE;
        ComputeBuffer fpBuffer = new ComputeBuffer(fpBufferCount, sizeof(bool), ComputeBufferType.Default);

        // 3. 初始化指纹Buffer为全false
        bool[] initFP = new bool[fpBufferCount];
        Array.Fill(initFP, false);
        fpBuffer.SetData(initFP);

        // 4. 配置Compute Shader参数
        int kernelId = morganFPComputeShader.FindKernel("CSGenerateMorganFP");
        morganFPComputeShader.SetInt("batchSize", batchSize);
        morganFPComputeShader.SetInt("smilesMaxLength", smilesMaxLength);
        morganFPComputeShader.SetInt("fpSize", FP_SIZE);
        morganFPComputeShader.SetInt("morganRadius", morganRadius);

        // 5. 绑定输入输出Buffer
        morganFPComputeShader.SetTexture(kernelId, "smilesInputTexture", smilesTexture);
        morganFPComputeShader.SetBuffer(kernelId, "smilesInputBuffer", smilesBuffer);
        morganFPComputeShader.SetBuffer(kernelId, "fpOutputBuffer", fpBuffer);

        // 6. 调度GPU计算（线程组适配移动端）
        int threadGroupX = Mathf.CeilToInt(batchSize / 32f); // 32线程/组
        morganFPComputeShader.Dispatch(kernelId, threadGroupX, 1, 1);

        // 7. 等待GPU计算完成（移动端必须，避免数据未写入就读取）
        //ComputeShader.SyncThread();

        Debug.Log($"512位指纹生成完成：批次大小={batchSize}，指纹Buffer长度={fpBufferCount}");
        return fpBuffer;
    }

    /// <summary>
    /// （可选）读取指纹Buffer到CPU（仅用于调试/验证）
    /// </summary>
    /// <param name="fpBuffer">指纹Buffer</param>
    /// <param name="molIdx">分子索引</param>
    /// <returns>该分子的512位指纹数组</returns>
    public bool[] GetFPFromBuffer(ComputeBuffer fpBuffer, int molIdx)
    {
        if (fpBuffer == null || molIdx >= fpBuffer.count / FP_SIZE)
        {
            Debug.LogError("指纹Buffer无效或分子索引越界");
            return null;
        }

        bool[] allFP = new bool[fpBuffer.count];
        fpBuffer.GetData(allFP);

        bool[] molFP = new bool[FP_SIZE];
        Array.Copy(allFP, molIdx * FP_SIZE, molFP, 0, FP_SIZE);
        return molFP;
    }

    /// <summary>
    /// 释放Buffer资源（必须调用，避免内存泄漏）
    /// </summary>
    public void ReleaseBuffer(ComputeBuffer buffer)
    {
        if (buffer != null)
        {
            buffer.Release();
            buffer.Dispose();
        }
    }

    private void OnDestroy()
    {
        // 兜底释放（防止遗漏）
        morganFPComputeShader = null;
    }
}