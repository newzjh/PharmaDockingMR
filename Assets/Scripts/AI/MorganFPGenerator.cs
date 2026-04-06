using UnityEngine;
using System;
using Cysharp.Threading.Tasks;
using System.Collections;
using UnityEngine.Rendering;

namespace AIDrugDiscovery
{

    /// <summary>
    /// ��Diffusion���ɵ�SMILES Buffer����512λMorganָ��
    /// </summary>
    public class MorganFPGenerator : MonoBehaviour
    {
        [Header("��������")]
        public ComputeShader morganFPComputeShader; // ������Compute Shader
        public int smilesMaxLength = 256;           // ����SMILES��󳤶ȣ�����Diffusionһ�£�
        public int morganRadius = 2;                // Morganָ�ư뾶����ҵ��׼Ϊ2��
        public bool usePackedFpReadback = false;
        public bool useLegacySmilesTextureInput = true;
        public bool useGraphTopologyMorgan = true;
        private const int FP_SIZE = 512;            // �̶�512λָ��
        private const int FP_PACKED_WORDS = FP_SIZE / 32;

        private uint[] allPackedFP = null;
        private uint[] allLegacyFP = null;

        private Texture2D CreateDummyTexture()
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.clear);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// ����512λָ�ƣ�ȫ��GPU�˴�������SMILES�ض���
        /// </summary>
        /// <param name="smilesBuffer">Diffusion�����SMILES Buffer</param>
        /// <param name="batchSize">������������</param>
        /// <returns>512λָ��Buffer����ֱ������FilterByFP��</returns>
        public async UniTask Generate512BitFP(ComputeBuffer smilesBuffer, int batchSize, Texture legacySmilesTexture = null)
        {
            // 1. ����У��
            if ((smilesBuffer == null && !(useLegacySmilesTextureInput && legacySmilesTexture != null)) || batchSize <= 0)
            {
                Debug.LogError("SMILES Buffer��Ч�����δ�С��ƥ��");
                return;
            }

            // 2. ����ָ�����Buffer��ÿ������512��bool��batchSize * 512���ȣ�
            int fpElementCount = usePackedFpReadback ? FP_PACKED_WORDS : FP_SIZE;
            int fpBufferCount = batchSize * fpElementCount;
            ComputeBuffer fpBuffer = new ComputeBuffer(fpBufferCount, sizeof(uint));

            // 3. ��ʼ��ָ��BufferΪȫfalse
            uint[] initFP = new uint[fpBufferCount];
            Array.Fill(initFP, 0u);
            fpBuffer.SetData(initFP);

            // 4. ����Compute Shader����
            bool useLegacyKernel = !useGraphTopologyMorgan || (useLegacySmilesTextureInput && legacySmilesTexture != null);
            int kernelId = morganFPComputeShader.FindKernel(useLegacyKernel ? "CSGenerateMorganFPLegacy" : "CSGenerateMorganFP");
            morganFPComputeShader.SetInt("batchSize", batchSize);
            morganFPComputeShader.SetInt("smilesMaxLength", smilesMaxLength);
            morganFPComputeShader.SetInt("morganRadius", morganRadius);
            morganFPComputeShader.SetInt("packOutput", usePackedFpReadback ? 1 : 0);
            morganFPComputeShader.SetInt("useSmilesTextureInput", useLegacySmilesTextureInput && legacySmilesTexture != null ? 1 : 0);

            Texture boundTexture = legacySmilesTexture ?? CreateDummyTexture();
            bool disposeDummyTexture = legacySmilesTexture == null;
            ComputeBuffer boundBuffer = smilesBuffer ?? new ComputeBuffer(1, sizeof(int));
            bool disposeDummyBuffer = smilesBuffer == null;

            // 5. ���������Buffer
            morganFPComputeShader.SetBuffer(kernelId, "smilesInputBuffer", boundBuffer);
            morganFPComputeShader.SetTexture(kernelId, "smilesInputTexture", boundTexture);
            morganFPComputeShader.SetBuffer(kernelId, "fpOutputBuffer", fpBuffer);

            // 6. ����GPU���㣨�߳��������ƶ��ˣ�
            int threadGroupX = Mathf.CeilToInt(batchSize / 32f); // 32�߳�/��
            morganFPComputeShader.Dispatch(kernelId, threadGroupX, 1, 1);

            // 7. �ȴ�GPU������ɣ��ƶ��˱��룬��������δд��Ͷ�ȡ��
            //ComputeShader.SyncThread();

            //fpBuffer.GetData(allFP);
            var req = await AsyncGPUReadback.RequestAsync(fpBuffer);
            if (usePackedFpReadback)
            {
                allPackedFP = req.GetData<uint>().ToArray();
                allLegacyFP = null;
            }
            else
            {
                allLegacyFP = req.GetData<uint>().ToArray();
                allPackedFP = null;
            }
            fpBuffer.Dispose();
            if (disposeDummyBuffer)
                boundBuffer.Dispose();
            if (disposeDummyTexture)
                Destroy(boundTexture);

            Debug.Log($"512λָ��������ɣ����δ�С={batchSize}��elements={fpBufferCount}");
            //return fpBuffer;
        }

        /// <summary>
        /// ����ѡ����ȡָ��Buffer��CPU�������ڵ���/��֤��
        /// </summary>
        /// <param name="fpBuffer">ָ��Buffer</param>
        /// <param name="molIdx">��������</param>
        /// <returns>�÷��ӵ�512λָ������</returns>
        public BitArray GetFPFromBuffer(int molIdx)
        {
            if (usePackedFpReadback)
            {
                if (allPackedFP == null || molIdx >= allPackedFP.Length / FP_PACKED_WORDS)
                {
                    Debug.LogError("ָ��Buffer��Ч���������Խ��");
                    return null;
                }
            }
            else if (allLegacyFP == null || molIdx >= allLegacyFP.Length / FP_SIZE)
            {
                Debug.LogError("ָ��Buffer��Ч���������Խ��");
                return null;
            }

            BitArray bits = new BitArray(FP_SIZE);
            if (usePackedFpReadback)
            {
                int wordBase = molIdx * FP_PACKED_WORDS;
                for (int wordIdx = 0; wordIdx < FP_PACKED_WORDS; wordIdx++)
                {
                    uint word = allPackedFP[wordBase + wordIdx];
                    int bitBase = wordIdx * 32;
                    for (int bit = 0; bit < 32; bit++)
                    {
                        bits.Set(bitBase + bit, (word & (1u << bit)) != 0u);
                    }
                }
            }
            else
            {
                int bitBase = molIdx * FP_SIZE;
                for (int i = 0; i < FP_SIZE; i++)
                    bits.Set(i, allLegacyFP[bitBase + i] != 0u);
            }

            return bits;
        }


        private void OnDestroy()
        {
            // �����ͷţ���ֹ��©��
            morganFPComputeShader = null;
        }
    }

}
