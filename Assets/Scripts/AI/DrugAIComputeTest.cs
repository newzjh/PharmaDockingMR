using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using AIDrugDiscovery.UI;


namespace AIDrugDiscovery
{


    public class DrugAIComputeTest : MonoBehaviour
    {

        
        private const int HEATMAP_SIZE = 32;       
        private const int FP_LENGTH = 512;        
        private const int TOTAL_TIMESTEPS = 1000;  
        
        
        public bool isPaused = false;
        public bool isTerminated = false;
        public int currentBatch = 0;
        public const int TOTAL_BATCHES = 10;

        public Material templateMat;


        public bool detectPocket = true;

        [Header("Render Modes")]
        public bool generateMeshSingle = true;
        public bool generateMeshBatch = false;
        public MoleculeRenderMode renderMode = MoleculeRenderMode.BallStick;
        private SMILESToBallMesh ballGen;
        private SMILESToCartoonMesh cartoonGen;
        private SMILESToSurfaceMesh surfaceGen;
        private SMILESToBallStickMesh ballStickGen;

        public SMILESFlipPageView FlipPageView;
        private Transform currentLigandParent;
        private GameObject activePreviewMesh;

        public async void Start()
        {
            var pocketdetector = GameObject.FindFirstObjectByType<PocketDetector>(FindObjectsInactive.Include);
            var hg = GameObject.FindFirstObjectByType<HeatmapGenerator>(FindObjectsInactive.Include);
            var dg = GameObject.FindFirstObjectByType<DiffusionGenerator>(FindObjectsInactive.Include);
            ballGen = GameObject.FindFirstObjectByType<SMILESToBallMesh>(FindObjectsInactive.Include);
            ballStickGen = GameObject.FindFirstObjectByType<SMILESToBallStickMesh>(FindObjectsInactive.Include);
            cartoonGen = GameObject.FindFirstObjectByType<SMILESToCartoonMesh>(FindObjectsInactive.Include);
            surfaceGen = GameObject.FindFirstObjectByType<SMILESToSurfaceMesh>(FindObjectsInactive.Include);
            var rfp = GameObject.FindFirstObjectByType<ReferenceFPGenerator>(FindObjectsInactive.Include);
            var mfp = GameObject.FindFirstObjectByType<MorganFPGenerator>(FindObjectsInactive.Include);
            var ff = GameObject.FindFirstObjectByType<FPFilter>(FindObjectsInactive.Include);

            if (FlipPageView != null)
                FlipPageView.onSmilesSelected = HandleSmilesSelected;

            string tempfolder = Application.persistentDataPath + "/cachepdb";
            if (Directory.Exists(tempfolder) == false)
            {
                Directory.CreateDirectory(tempfolder);
            }
            string pdbFullPath = tempfolder + "/" + "1AQ1" + ".pdb";
            string pdbqtFullPath = tempfolder + "/" + "1AQ1" + ".pdbqt";

            await UniTask.NextFrame();

            OpenBabelPDBQTConverter.ConvertPDBToPDBQT(pdbFullPath, pdbqtFullPath);

            await UniTask.NextFrame();

            if (detectPocket)
            {
                pocketdetector.pdbqtFilePath = pdbqtFullPath;
                pocketdetector.RunFPocketGPU();
                //pocketdetector.RunFPocketCSharpDetection();

                await UniTask.NextFrame();
            }

            
            List<string> aq1ActiveSmiles = new List<string>()
            {
                "C1=CC=C(C(=C1)C(=O)N)O",
                "CC(=O)Nc1ccc(O)cc1",
                "CN1C=NC2=C1C(=O)N(C(=O)N2C)C"
            };

            
            var aq1FPLibrary = rfp.GenerateReferenceFPLibrary(
                targetName: "1AQ1",
                activeSmilesList: aq1ActiveSmiles,
                fpType: ReferenceFPGenerator.FingerprintType.ECFP4,
                fpLength: 512);

            
            Debug.Log($"Built the 1AQ1 reference fingerprint library from {aq1FPLibrary.IndividualFPs.Count} active ligands.");
            Debug.Log($"The calibrated similarity threshold for 1AQ1 is {aq1FPLibrary.CalibratedThreshold:F3}.");

            foreach (var config in hg.proteinConfigs)
            {
                GameObject parentgo = new GameObject("ligands_for_"+config.proteinName);
                parentgo.transform.localScale = Vector3.one;
                parentgo.transform.localEulerAngles = Vector3.zero;
                parentgo.transform.localPosition = Vector3.zero;
                currentLigandParent = parentgo.transform;

                int ligandCount = 0;


                currentBatch = 0;
                while (currentBatch < TOTAL_BATCHES && !isTerminated)
                {
       
                    while (isPaused && !isTerminated)
                    {
                        await UniTask.Yield();
                    }

                    if (isTerminated)
                        break;

                    if (!Application.isPlaying)
                        return;

                    var heatmap = await hg.GenerateProteinHeatmap(config);
                    var heatmap3D = await hg.GenerateProteinHeatmap3D(config);
                    var config2 = dg.diffusionConfigs.First();
                    config2.proteinActiveCenter = config.activeSiteCenter;
                    var unfilter = await dg.GenerateProteinTargetedMols(config2, heatmap, heatmap3D, currentBatch * 1024);
                    var smiles = unfilter.Item1;
                    var filters = unfilter.Item2;
                    var smilesBatch = unfilter.Item3;

                    if (!Application.isPlaying)
                        return;

                    Texture2D.Destroy(heatmap);
                    RenderTexture.Destroy(heatmap3D);

                    await mfp.Generate512BitFP(smilesBatch.SmilesBuffer, smilesBatch.BatchSize, smilesBatch.SmilesTexture, smiles);

                    if (!Application.isPlaying)
                        return;

                    List<int> newfilter = new List<int>();
                    for (int j = 0; j < filters.Count; j++)
                    {
                        var genfp = mfp.GetFPFromBuffer(filters[j]);
                        var similarity = rfp.CalculateFPSimilarity(genfp, aq1FPLibrary);
                        if (similarity > aq1FPLibrary.CalibratedThreshold)
                            newfilter.Add(j);
                    }

                    List<Mesh> meshes = null;
                    if (generateMeshBatch)
                    {
                        meshes = await ballStickGen.GenerateBallStickMeshes(filters, smilesBatch.SmilesBuffer, smilesBatch.BatchSize, smilesBatch.SmilesTexture);
                    }

                    if (!Application.isPlaying)
                        return;

                    smilesBatch.Dispose();

                    await UniTask.NextFrame();

                    if (generateMeshBatch)
                    {
                        for (int i = 0; i < smiles.Count && i < meshes.Count; i++)
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

                    for (int i = 0; i < smiles.Count; i++)
                    {
                        FlipPageView.allSMILES.Add(smiles[i]);
                    }
                    FlipPageView.UpdatePageDisplay();

                    if (!Application.isPlaying)
                        return;

                    currentBatch++;
                    Debug.Log($"finish: {currentBatch}/{TOTAL_BATCHES}");

                    await UniTask.WaitForSeconds(15);
                }

                if (isTerminated)
                {
                    break;
                }
 
            }

        }

        public void Pause()
        {
            isPaused = true;
        }

        public void Resume()
        {
            isPaused = false;
        }

        public void Terminate()
        {
            isTerminated = true;
            isPaused = false;
        }

        public void Reset()
        {
            isPaused = false;
            isTerminated = false;
            currentBatch = 0;
        }

        void OnDestroy()
        {
            Terminate();
        }

        private async void HandleSmilesSelected(int smilesIndex, string smiles)
        {
            if (!generateMeshSingle || string.IsNullOrEmpty(smiles))
                return;

            if (activePreviewMesh != null)
            {
                Destroy(activePreviewMesh);
                activePreviewMesh = null;
            }

            Mesh mesh = null;
            switch (renderMode)
            {
                case MoleculeRenderMode.Ball:
                    if (ballGen != null)
                        mesh = await ballGen.GenerateSingleBallMesh(smiles);
                    break;
                case MoleculeRenderMode.Cartoon:
                    if (cartoonGen != null)
                        mesh = await cartoonGen.GenerateSingleCartoonMesh(smiles);
                    break;
                case MoleculeRenderMode.Surface:
                    if (surfaceGen != null)
                        mesh = await surfaceGen.GenerateSingleSurfaceMesh(smiles);
                    break;
                default:
                    if (ballStickGen != null)
                        mesh = await ballStickGen.GenerateSingleBallStickMesh(smiles);
                    break;
            }

            if (mesh == null || !Application.isPlaying)
                return;

            GameObject go = new GameObject(smiles);
            if (currentLigandParent != null)
                go.transform.SetParent(currentLigandParent, false);
            go.transform.localScale = Vector3.one;
            go.transform.localEulerAngles = Vector3.zero;
            go.transform.localPosition = Vector3.zero;

            var mf = go.AddComponent<MeshFilter>();
            mf.mesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.material = templateMat;
            activePreviewMesh = go;
        }
    }

}
