using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

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
            var hg = GameObject.FindFirstObjectByType<HeatmapGenerator>(FindObjectsInactive.Include);
            var dg = GameObject.FindFirstObjectByType<DiffusionGenerator>(FindObjectsInactive.Include);
            var mg = GameObject.FindFirstObjectByType<SMILESToBallStickMesh>(FindObjectsInactive.Include);
            var fp = GameObject.FindFirstObjectByType<MorganFPGenerator>(FindObjectsInactive.Include);

            foreach (var config in hg.proteinConfigs)
            {
                var heatmap = await hg.GenerateProteinHeatmap(config);
                var heatmap3D = await hg.GenerateProteinHeatmap3D(config);
                var unfilter = await dg.GenerateProteinTargetedMols(dg.diffusionConfigs.First(), heatmap, heatmap3D);
                var smiles = unfilter.Item1;
                var filters = unfilter.Item2;
                var smilebuffer = unfilter.Item3;
                var smiletexture = unfilter.Item4;

                Texture2D.Destroy(heatmap);

                outTest = heatmap3D;
                //RenderTexture.Destroy(heatmap3D);

                var generateFP = await fp.Generate512BitFP(smilebuffer, smiletexture, smiletexture.height);

                var meshes = await mg.GenerateBallStickMeshes(filters, smilebuffer, smiletexture);
                smilebuffer.Dispose();
                RenderTexture.Destroy(smiletexture);

                GameObject parentgo = new GameObject("ligands");
                parentgo.transform.localScale = Vector3.one;
                parentgo.transform.localEulerAngles = Vector3.zero;
                parentgo.transform.localPosition = Vector3.zero;

                int count = 0;
                foreach(var mesh in meshes)
                {
                    GameObject go = new GameObject();
                    go.transform.parent = parentgo.transform;
                    go.transform.localScale = Vector3.one;
                    go.transform.localEulerAngles = Vector3.zero;
                    go.transform.localPosition = Vector3.forward * count * 2;
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