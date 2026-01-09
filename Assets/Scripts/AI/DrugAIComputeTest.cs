using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AIDrugDiscovery
{


    public class DrugAIComputeTest : MonoBehaviour
    {

        // 测试参数
        private const int BATCH_SIZE = 2;          // 测试用2个分子
        private const int HEATMAP_SIZE = 32;       // 32×32热力图
        private const int FP_LENGTH = 512;        // Morgan指纹长度
        private const int TOTAL_TIMESTEPS = 1000;  // Diffusion总时间步

        public Material templateMat;

        public async void Start()
        {
            var hg = GameObject.FindFirstObjectByType<HeatmapGenerator>(FindObjectsInactive.Include);
            var dg = GameObject.FindFirstObjectByType<DiffusionGenerator>(FindObjectsInactive.Include);
            var mg = GameObject.FindFirstObjectByType<SMILESToBallStickMesh>(FindObjectsInactive.Include);
            var fp = GameObject.FindFirstObjectByType<MorganFPGenerator>(FindObjectsInactive.Include);

            foreach (var config in hg.proteinConfigs)
            {
                var heatmap = hg.GenerateProteinHeatmap(config);
                var unfilter = await dg.GenerateProteinTargetedMols(dg.diffusionConfigs.First(), heatmap);
                var smiles = unfilter.Item1;
                var filters = unfilter.Item2;
                var smilebuffer = unfilter.Item3;
                var smiletexture = unfilter.Item4;

                var generateFP = await fp.Generate512BitFP(smilebuffer, smiletexture, smiletexture.height);

                var meshes = await mg.GenerateBallStickMeshes(filters, smilebuffer, smiletexture);
                smilebuffer.Dispose();

                int count = 0;
                foreach(var mesh in meshes)
                {
                    GameObject go = new GameObject();
                    go.transform.localScale = Vector3.one;
                    go.transform.localEulerAngles = Vector3.zero;
                    go.transform.localPosition = Vector3.zero;
                    var mf = go.AddComponent<MeshFilter>();
                    mf.mesh = mesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    mr.material = templateMat;

                    count++;
                }
            }

        }


        void OnDestroy()
        {

        }
    }

}