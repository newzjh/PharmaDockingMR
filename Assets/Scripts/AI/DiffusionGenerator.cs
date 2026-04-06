using UnityEngine;
using System.Collections.Generic;
using System;
using Mirror.BouncyCastle.Asn1.Mozilla;
using Cysharp.Threading.Tasks;
using UnityEngine.Rendering;

namespace AIDrugDiscovery
{
    // ͨ����ɢģ�����ã������л���֧��Inspector���ö����壩
    [Serializable]
    public class ProteinDiffusionConfig
    {
        [Header("��������")]
        public string proteinName = "1AQ1"; // �������ƣ���3CLpro��EGFR��

        [Header("��ɢ���Ĳ���")]
        public int batchSize = 1024; // �������ɷ�������
        public int timesteps = 1000; // ��ɢʱ�䲽
        public float betaStart = 0.0001f; // ������ʼǿ��
        public float betaEnd = 0.02f; // ��������ǿ��

        [Header("����Լ������")]
        public float heatmapWeight = 0.8f; // ����ͼԼ��Ȩ��
        public int maxAtomLimit = 60; // �������ԭ����
        public float minFeatureScore = 0.3f; // ��С����ƥ������������ˮռ�ȣ�
        public Vector3 proteinActiveCenter = new Vector3(10.5f, 8.2f, 12.7f); // ����ӻ�������

        [Header("ԭ������ƫ�ã����䲻ͬ���壩")]
        public float cWeight = 0.4f; // Cԭ��Ȩ��
        public float oWeight = 0.3f; // Oԭ��Ȩ��
        public float nWeight = 0.2f; // Nԭ��Ȩ��
        public float sWeight = 0.1f; // Sԭ��Ȩ��

        [Header("��������")]
        public bool lowPowerMode = false; // ��Я�ն˵͹���ģʽ
    }

    public class DiffusionGenerator : MonoBehaviour
    {
        public const int SMILES_MAX_LENGTH = 256;

        public sealed class GeneratedSmilesBatch : IDisposable
        {
            public ComputeBuffer SmilesBuffer { get; }
            public int BatchSize { get; }
            public RenderTexture SmilesTexture { get; }

            public GeneratedSmilesBatch(ComputeBuffer smilesBuffer, int batchSize, RenderTexture smilesTexture = null)
            {
                SmilesBuffer = smilesBuffer;
                BatchSize = batchSize;
                SmilesTexture = smilesTexture;
            }

            public void Dispose()
            {
                if (SmilesBuffer != null)
                {
                    SmilesBuffer.Release();
                    SmilesBuffer.Dispose();
                }

                if (SmilesTexture != null)
                    RenderTexture.Destroy(SmilesTexture);
            }
        }

        [Header("�������")]
        public ComputeShader diffusionCS;
        public List<ProteinDiffusionConfig> diffusionConfigs; // �����������б�
        public bool collectDebugScores = false;
        public bool useLegacySmilesTextureTransport = true;


        public async void Begin(Texture2D heatmap, RenderTexture heatmap3D)
        {
            foreach (var config in diffusionConfigs)
            {
                  await GenerateProteinTargetedMols(config, heatmap, heatmap3D, 0);
            }
        }

        public bool test = true;

        // ͨ�ô���Ӱ���������ɺ���
        
        public async UniTask<ValueTuple<List<string>, List<int>, GeneratedSmilesBatch>> GenerateProteinTargetedMols(ProteinDiffusionConfig config, Texture2D proteinHeatmap, RenderTexture proteinHeatmap3D, int batchOffset)
        {
            List<string> generatedSmiles = new List<string>();
            List<int> generatedIndices = new List<int>();

            // 1. ����У��
            if (proteinHeatmap == null)
            {
                Debug.LogError($"[{config.proteinName}] ����ͼΪ�գ��޷����ɰ������");
                return (generatedSmiles, generatedIndices, null);
            }

            // 2. �͹���ģʽ����
            int effectiveBatchSize = config.lowPowerMode ? Mathf.RoundToInt(config.batchSize * 0.3f) : config.batchSize;
            int effectiveTimesteps = config.lowPowerMode ? Mathf.RoundToInt(config.timesteps * 0.5f) : config.timesteps;

            // 3. Ԥ������ɢ�������ȱ���beta/alpha/alpha_cumprod��
            float[] betas = new float[effectiveTimesteps];
            float[] alphas = new float[effectiveTimesteps];
            float[] alphaCumprod = new float[effectiveTimesteps];
            ComputeBetaSchedule(betas, alphas, alphaCumprod, effectiveTimesteps, config);

            // 4. ����Compute Buffer
            // 4.1 ��������Buffer
            ComputeBuffer betaBuffer = new ComputeBuffer(effectiveTimesteps, sizeof(float));
            betaBuffer.SetData(betas);
            ComputeBuffer alphaCumprodBuffer = new ComputeBuffer(effectiveTimesteps, sizeof(float));
            alphaCumprodBuffer.SetData(alphaCumprod);

            // 4.2 ԭ������Ȩ��Buffer�����䲻ͬ�����ƫ�ã�
            Vector4 atomWeightBuffer = new Vector4(config.cWeight, config.oWeight, config.nWeight, config.sWeight);

            // 4.3 ��������Buffer�����ݴ���ӻ���λ�����꣩

            // 4.4 SMILES���Buffer��ÿ������256�ַ���
            int smilesStride = SMILES_MAX_LENGTH * sizeof(int);
            ComputeBuffer smilesBuffer = new ComputeBuffer(effectiveBatchSize, smilesStride);
            int[] initSmiles = new int[effectiveBatchSize * SMILES_MAX_LENGTH];
            smilesBuffer.SetData(initSmiles);
            RenderTexture boundSmilesTexture = new RenderTexture(SMILES_MAX_LENGTH, effectiveBatchSize, 0, UnityEngine.Experimental.Rendering.GraphicsFormat.R16_SFloat);
            boundSmilesTexture.enableRandomWrite = true;
            boundSmilesTexture.Create();
            RenderTexture exportedSmilesTexture = useLegacySmilesTextureTransport ? boundSmilesTexture : null;

            // 5. ����Compute Shader����
            int kernelId = diffusionCS.FindKernel("CSForwardDiffusion");
            diffusionCS.SetInt("batchSize", effectiveBatchSize);
            diffusionCS.SetInt("batchOffset", batchOffset);
            diffusionCS.SetInt("timesteps", effectiveTimesteps);
            diffusionCS.SetFloat("heatmapWeight", config.heatmapWeight);
            diffusionCS.SetInt("maxAtoms", config.maxAtomLimit);
            diffusionCS.SetFloat("minFeatureScore", config.minFeatureScore);
            diffusionCS.SetInt("heatmapSize", proteinHeatmap.width); // ��������ͼ�ߴ�
            diffusionCS.SetInt("writeDebugScores", collectDebugScores ? 1 : 0);
            diffusionCS.SetInt("writeSmilesTexture", useLegacySmilesTextureTransport ? 1 : 0);

            // ��������Buffer
            diffusionCS.SetTexture(kernelId, "proteinHeatmap", proteinHeatmap);
            diffusionCS.SetTexture(kernelId, "proteinHeatmap3D", proteinHeatmap3D);
            diffusionCS.SetBuffer(kernelId, "betaBuffer", betaBuffer);
            diffusionCS.SetBuffer(kernelId, "alphaCumprodBuffer", alphaCumprodBuffer);
            diffusionCS.SetVector("atomWeightBuffer", atomWeightBuffer);
            diffusionCS.SetVector("activeCenterBuffer", config.proteinActiveCenter);
            diffusionCS.SetBuffer(kernelId, "smilesOutputBuffer", smilesBuffer);
            diffusionCS.SetTexture(kernelId, "smilesOutputTexture", boundSmilesTexture);

            // ����Debug Buffer
            ComputeBuffer matchScoreDebugBuffer = new ComputeBuffer(effectiveBatchSize * config.maxAtomLimit, sizeof(float));
            diffusionCS.SetBuffer(kernelId, "matchScoreDebugBuffer", matchScoreDebugBuffer);

            // 6. ����CS���߳�������batchSize��
            int threadGroupX = Mathf.CeilToInt(effectiveBatchSize / 64f);
            diffusionCS.Dispatch(kernelId, threadGroupX, 1, 1);
            //while (test && Application.isPlaying)
            //{
            //    diffusionCS.Dispatch(kernelId, threadGroupX, 1, 1);
            //    await UniTask.NextFrame();
            //}

            float[] scores = null;
            if (collectDebugScores)
            {
                scores = new float[effectiveBatchSize * config.maxAtomLimit];
                {
                    var req = await AsyncGPUReadback.RequestAsync(matchScoreDebugBuffer);
                    scores = req.GetData<float>().ToArray();
                }

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
                    //    Debug.Log($"����{i}ƽ��ƥ�������{avgScore}");
                }
            }

            // 7. ��ȡ������SMILES���
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

            // 8. �ͷ�����Buffer�������ڴ�й©��
            betaBuffer.Release();
            alphaCumprodBuffer.Release();
            matchScoreDebugBuffer?.Release();
            matchScoreDebugBuffer?.Dispose();

            Debug.Log($"[{config.proteinName}] �������������ɣ�������{generatedSmiles.Count}����ЧSMILES");
            if (!useLegacySmilesTextureTransport)
                RenderTexture.Destroy(boundSmilesTexture);

            return (generatedSmiles, generatedIndices, new GeneratedSmilesBatch(smilesBuffer, effectiveBatchSize, exportedSmilesTexture));
        }

        // ����������Ԥ������ɢ�������ȱ���֧���Զ���beta������
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
