using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using static Mirror.BouncyCastle.Math.EC.ECCurve;

namespace AIDrugDiscovery
{


    public class DrugAIComputeTest : MonoBehaviour
    {

        // 测试参数
        private const int HEATMAP_SIZE = 32;       // 32×32热力图
        private const int FP_LENGTH = 512;        // Morgan指纹长度
        private const int TOTAL_TIMESTEPS = 1000;  // Diffusion总时间步

        public Material templateMat;

        public RenderTexture outTest;

        public async void Start()
        {
            var pocketdetector = GameObject.FindFirstObjectByType<PocketDetector>(FindObjectsInactive.Include);
            var hg = GameObject.FindFirstObjectByType<HeatmapGenerator>(FindObjectsInactive.Include);
            var dg = GameObject.FindFirstObjectByType<DiffusionGenerator>(FindObjectsInactive.Include);
            var mg = GameObject.FindFirstObjectByType<SMILESToBallStickMesh>(FindObjectsInactive.Include);
            var rfp = GameObject.FindFirstObjectByType<ReferenceFPGenerator>(FindObjectsInactive.Include);
            var mfp = GameObject.FindFirstObjectByType<MorganFPGenerator>(FindObjectsInactive.Include);
            var ff = GameObject.FindFirstObjectByType<FPFilter>(FindObjectsInactive.Include);

            string tempfolder = Application.persistentDataPath + "/cachepdb";
            if (Directory.Exists(tempfolder) == false)
            {
                Directory.CreateDirectory(tempfolder);
            }
            string pdbqtFullPath = tempfolder + "/" + "1AQ1" + ".pdb";

            pocketdetector.pdbqtFilePath = pdbqtFullPath;
            pocketdetector.RunFPocketGPU();
            //pocketdetector.RunFPocketCSharpDetection();

            return;

            // 2. 1AQ1活性配体SMILES列表（实验数据）
            List<string> aq1ActiveSmiles = new List<string>()
            {
                "C1=CC=C(C(=C1)C(=O)N)O",
                "CC(=O)Nc1ccc(O)cc1",
                "CN1C=NC2=C1C(=O)N(C(=O)N2C)C"
            };

            // 3. 生成ECFP4参考指纹库
            var aq1FPLibrary = rfp.GenerateReferenceFPLibrary(
                targetName: "1AQ1",
                activeSmilesList: aq1ActiveSmiles,
                fpType: ReferenceFPGenerator.FingerprintType.ECFP4,
                fpLength: 512);

            // 4. 输出核心信息
            Debug.Log($"1AQ1共识指纹长度：{aq1FPLibrary.ConsensusFP.Count}");
            Debug.Log($"校准相似度阈值：{aq1FPLibrary.CalibratedThreshold:F2}");

            foreach (var config in hg.proteinConfigs)
            {
                GameObject parentgo = new GameObject("ligands_for_"+config.proteinName);
                parentgo.transform.localScale = Vector3.one;
                parentgo.transform.localEulerAngles = Vector3.zero;
                parentgo.transform.localPosition = Vector3.zero;

                int ligandCount = 0;

                for (int iBatch = 0; iBatch < 3; iBatch++)
                {
                    var heatmap = await hg.GenerateProteinHeatmap(config);
                    var heatmap3D = await hg.GenerateProteinHeatmap3D(config);
                    var config2 = dg.diffusionConfigs.First();
                    config2.proteinActiveCenter = config.activeSiteCenter;
                    var unfilter = await dg.GenerateProteinTargetedMols(config2, heatmap, heatmap3D, iBatch * 1024);
                    var smiles = unfilter.Item1;
                    var filters = unfilter.Item2;
                    var smiletexture = unfilter.Item3;

                    Texture2D.Destroy(heatmap);
                    RenderTexture.Destroy(heatmap3D);

                    var generateFP = await mfp.Generate512BitFP(smiletexture, smiletexture.height);

                    List<int> newfilter = new List<int>();
                    for (int j = 0; j < filters.Count; j++)
                    {
                        var genfp = mfp.GetFPFromBuffer(filters[j]);
                        var similarity = rfp.CalculateFPSimilarity(genfp, aq1FPLibrary);
                        if (similarity > aq1FPLibrary.CalibratedThreshold)
                            newfilter.Add(j);
                    }

                    var meshes = await mg.GenerateBallStickMeshes(filters, smiletexture);
                    RenderTexture.Destroy(smiletexture);

                    for(int i=0;i<filters.Count;i++)
                    {
                        GameObject go = new GameObject(smiles[i]);
                        go.transform.parent = parentgo.transform;
                        go.transform.localScale = Vector3.one;
                        go.transform.localEulerAngles = Vector3.zero;
                        go.transform.localPosition = Vector3.forward * ligandCount * 2;
                        var mf = go.AddComponent<MeshFilter>();
                        mf.mesh = meshes[i];
                        var mr = go.AddComponent<MeshRenderer>();
                        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        mr.receiveShadows = false;
                        mr.material = templateMat;

                        ligandCount++;
                    }
                }
 
            }

        }


        void OnDestroy()
        {

        }
    }

}