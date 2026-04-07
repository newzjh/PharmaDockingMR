using UnityEngine;
using System.Collections.Generic;
using System;

namespace AIDrugDiscovery
{

    
    [Serializable]
    public class FPFilterConfig
    {
        public float minSimilarity = 0.6f; 
        public int topK = 100; 
        public bool lowPowerMode = false; 
    }

    
    public struct MolFPResult
    {
        public int molIndex; 
        public float maxSimilarity; 
    }

    public class FPFilter : MonoBehaviour
    {
        [Header("")]
        public ComputeShader similarityCS; 
        public FPFilterConfig filterConfig;
        public List<MolFPResult> FilterByFP(ComputeBuffer genFPBuffer, ComputeBuffer refFPBuffer,
            int genCount, int refCount, int fpSize)
        {
            List<MolFPResult> filteredResults = new List<MolFPResult>();

            
            if (genFPBuffer == null || refFPBuffer == null || genCount == 0 || refCount == 0)
            {
                Debug.LogError("Fingerprint filtering status");
                return filteredResults;
            }

            
            ComputeBuffer similarityBuffer = new ComputeBuffer(genCount, sizeof(float));
            float[] initSimilarity = new float[genCount];
            similarityBuffer.SetData(initSimilarity);

            
            int kernelId = similarityCS.FindKernel("CSComputeMaxSimilarity");
            similarityCS.SetInt("genCount", genCount);
            similarityCS.SetInt("refCount", refCount);
            similarityCS.SetFloat("minSimilarity", filterConfig.minSimilarity);

            // Buffer
            similarityCS.SetBuffer(kernelId, "generatedFP", genFPBuffer);
            similarityCS.SetBuffer(kernelId, "referenceFP", refFPBuffer);
            similarityCS.SetBuffer(kernelId, "maxSimilarityOutput", similarityBuffer);

            
            int threadGroupX = Mathf.CeilToInt(genCount / 32f);
            similarityCS.Dispatch(kernelId, threadGroupX, 1, 1);

            
            float[] similarityResults = new float[genCount];
            similarityBuffer.GetData(similarityResults);

            
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

            
            tempResults.Sort((a, b) => b.maxSimilarity.CompareTo(a.maxSimilarity));
            int takeCount = Mathf.Min(filterConfig.topK, tempResults.Count);
            filteredResults.AddRange(tempResults.GetRange(0, takeCount));

            
            similarityBuffer.Release();

            Debug.Log($"Fingerprint filtering status");
            return filteredResults;
        }
        public List<string> GetFilteredSmiles(ComputeBuffer smilesBuffer, List<MolFPResult> filteredResults, int smilesMaxLength)
        {
            List<string> filteredSmiles = new List<string>();
            int stride = smilesMaxLength * sizeof(char);

            
            char[][] allSmiles = new char[smilesBuffer.count][];
            for (int i = 0; i < smilesBuffer.count; i++)
            {
                allSmiles[i] = new char[smilesMaxLength];
            }
            smilesBuffer.GetData(allSmiles);

            
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