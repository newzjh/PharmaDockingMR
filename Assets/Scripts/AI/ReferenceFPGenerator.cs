using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using System.Collections; 


namespace AIDrugDiscovery
{
    // Builds and persists target-specific reference fingerprint libraries used for filtering generated ligands.
    public class ReferenceFPGenerator : MonoBehaviour
    {
        #region Core Configuration

        // Supported fingerprint families for reference-library generation.
        public enum FingerprintType
        {
            ECFP4,
            PHFP,
            STFP,
            FusedECFP4PHFP
        }

        private const int DEFAULT_FP_LENGTH = 512;
        private const float SIMILARITY_THRESHOLD = 0.7f;
        private const string FP_LIBRARY_PATH = "ReferenceFP/";
        #endregion

        #region Data Structures
        [System.Serializable]
        public class ReferenceFPLibrary
        {
            public string TargetName;
            public FingerprintType FPType;
            public int FPLength;
            public BitArray ConsensusFP;
            public List<BitArray> IndividualFPs;
            public List<string> SourceSMILES;
            public float CalibratedThreshold;
        }
        private struct PharmacophoreFeature
        {
            public bool IsHydrophobic;
            public bool IsHBD;
            public bool IsHBA;
            public bool IsPositive;
            public bool IsNegative;

            
            public int GetHash()
            {
                return (IsHydrophobic ? 1 : 0) + (IsHBD ? 2 : 0) + (IsHBA ? 4 : 0) + (IsPositive ? 8 : 0) + (IsNegative ? 16 : 0);
            }
        }
        #endregion

        #region Core Dependencies
        private SimplifiedECFP4Generator _ecfp4Generator;
        private string _fullFPLibraryPath;
        #endregion

        #region Initialization
        private void Awake()
        {
            _ecfp4Generator = new SimplifiedECFP4Generator();
            _fullFPLibraryPath = Path.Combine(Application.streamingAssetsPath, FP_LIBRARY_PATH);
            if (!Directory.Exists(_fullFPLibraryPath))
            {
                Directory.CreateDirectory(_fullFPLibraryPath);
            }
        }
        #endregion

        #region Core API: Generate reference fingerprint libraries
        public ReferenceFPLibrary GenerateReferenceFPLibrary(
            string targetName,
            List<string> activeSmilesList,
            FingerprintType fpType = FingerprintType.ECFP4,
            int fpLength = DEFAULT_FP_LENGTH)
        {
            // Drop empty or invalid ligands before generating the library.
            List<string> validSmiles = activeSmilesList.Where(s => !string.IsNullOrEmpty(s) && IsValidSMILES(s)).ToList();
            if (validSmiles.Count == 0)
            {
                Debug.LogError($"Reference fingerprint generation failed for {targetName}: no valid active SMILES were provided.");
                return null;
            }

            
            List<BitArray> individualFPs = new List<BitArray>();
            foreach (var smiles in validSmiles)
            {
                BitArray fp = GenerateFingerprint(smiles, fpType, fpLength);
                if (fp != null && fp.Count == fpLength)
                {
                    individualFPs.Add(fp);
                }
            }

            if (individualFPs.Count == 0)
            {
                Debug.LogError($"Reference fingerprint generation status");
                return null;
            }

            
            BitArray consensusFP = GenerateConsensusFP(individualFPs, fpLength);

            
            float calibratedThreshold = CalibrateSimilarityThreshold(individualFPs, consensusFP);

            
            ReferenceFPLibrary fpLibrary = new ReferenceFPLibrary()
            {
                TargetName = targetName,
                FPType = fpType,
                FPLength = fpLength,
                ConsensusFP = consensusFP,
                IndividualFPs = individualFPs,
                SourceSMILES = validSmiles,
                CalibratedThreshold = calibratedThreshold
            };

            
            SaveFPLibrary(fpLibrary);

            Debug.Log($"Reference fingerprint library generated for target {targetName}:\n" +
                      $"Fingerprint type: {fpType}\n" +
                      $"Active ligand count: {validSmiles.Count}\n" +
                      $"Calibrated threshold: {calibratedThreshold:F2}");

            return fpLibrary;
        }
        public ReferenceFPLibrary GenerateVirtualReferenceFP(
            string targetName,
            Dictionary<string, bool> pocketFeatures,
            FingerprintType fpType = FingerprintType.PHFP)
        {
            
            BitArray virtualFP = new BitArray(DEFAULT_FP_LENGTH);// new List<int>(Enumerable.Repeat(0, DEFAULT_FP_LENGTH));

            if (fpType == FingerprintType.PHFP || fpType == FingerprintType.FusedECFP4PHFP)
            {
                PharmacophoreFeature feature = new PharmacophoreFeature()
                {
                    IsHydrophobic = pocketFeatures.ContainsKey("Hydrophobic") && pocketFeatures["Hydrophobic"],
                    IsHBD = pocketFeatures.ContainsKey("HBD") && pocketFeatures["HBD"],
                    IsHBA = pocketFeatures.ContainsKey("HBA") && pocketFeatures["HBA"],
                    IsPositive = pocketFeatures.ContainsKey("Positive") && pocketFeatures["Positive"],
                    IsNegative = pocketFeatures.ContainsKey("Negative") && pocketFeatures["Negative"]
                };

                int hash = feature.GetHash();
                int bitIndex = Mathf.Abs(hash) % DEFAULT_FP_LENGTH;
                virtualFP.Set(bitIndex, true);

                
                if (feature.IsHydrophobic) virtualFP.Set(10,true);
                if (feature.IsHBD) virtualFP.Set(20,true);
                if (feature.IsHBA) virtualFP.Set(30,true);
            }

            
            ReferenceFPLibrary fpLibrary = new ReferenceFPLibrary()
            {
                TargetName = targetName,
                FPType = fpType,
                FPLength = DEFAULT_FP_LENGTH,
                ConsensusFP = virtualFP,
                IndividualFPs = new List<BitArray>() { virtualFP },
                SourceSMILES = new List<string>() { $"Virtual_{targetName}" },
                CalibratedThreshold = SIMILARITY_THRESHOLD
            };

            
            SaveFPLibrary(fpLibrary);

            Debug.Log($"Reference fingerprint generation status");
            return fpLibrary;
        }
        #endregion

        #region Helpers: fingerprint generation, consensus, and threshold calibration
        private BitArray GenerateFingerprint(string smiles, FingerprintType fpType, int fpLength)
        {
            List<int> fp = null;
            switch (fpType)
            {
                case FingerprintType.ECFP4:
                    fp = _ecfp4Generator.GenerateECFP4(smiles).Take(fpLength).ToList();
                    break;
                case FingerprintType.PHFP:
                    fp = GeneratePHFP(smiles, fpLength);
                    break;
                case FingerprintType.STFP:
                    fp = GenerateSTFP(smiles, fpLength);
                    break;
                case FingerprintType.FusedECFP4PHFP:
                    var ecfp4 = _ecfp4Generator.GenerateECFP4(smiles);
                    var phfp = GeneratePHFP(smiles, fpLength);
                    fp = ecfp4.Take(fpLength / 2).Concat(phfp.Take(fpLength / 2)).ToList();
                    break;
                default:
                    fp = _ecfp4Generator.GenerateECFP4(smiles).Take(fpLength).ToList();
                    break;
            }

            var bits = new BitArray(fp.Count);
            for (int i = 0; i < fp.Count; i++)
                bits.Set(i, fp[i]>0);
            return bits;
        }
        private List<int> GeneratePHFP(string smiles, int fpLength)
        {
            List<int> phfp = new List<int>(Enumerable.Repeat(0, fpLength));
            List<SimplifiedECFP4Generator.Atom> atoms = _ecfp4Generator.ParseSMILESToAtoms(smiles);

            foreach (var atom in atoms)
            {
                PharmacophoreFeature feature = GetPharmacophoreFeature(atom);
                int hash = feature.GetHash() + atom.AtomicNumber;
                int bitIndex = Mathf.Abs(hash) % fpLength;
                phfp[bitIndex] = 1;
            }

            return phfp;
        }
        private List<int> GenerateSTFP(string smiles, int fpLength)
        {
            List<int> stfp = new List<int>(Enumerable.Repeat(0, fpLength));
            List<SimplifiedECFP4Generator.Atom> atoms = _ecfp4Generator.ParseSMILESToAtoms(smiles);

            
            int atomCount = atoms.Count;
            int bondCount = atoms.Sum(a => a.BondCount) / 2; 
            int heteroAtomCount = atoms.Count(a => a.AtomicNumber != 6 && a.AtomicNumber != 1);

            
            stfp[Mathf.Abs(atomCount) % fpLength] = 1;
            stfp[Mathf.Abs(bondCount + 100) % fpLength] = 1;
            stfp[Mathf.Abs(heteroAtomCount + 200) % fpLength] = 1;

            return stfp;
        }
        private PharmacophoreFeature GetPharmacophoreFeature(SimplifiedECFP4Generator.Atom atom)
        {
            PharmacophoreFeature feature = new PharmacophoreFeature();

            
            if (atom.AtomicNumber == 6 || atom.AtomicNumber == 16)
            {
                feature.IsHydrophobic = true;
            }

            
            if (atom.AtomicNumber == 7 && atom.BondCount >= 3)
            {
                feature.IsHBD = true;
            }

            
            if (atom.AtomicNumber == 8 || atom.AtomicNumber == 7 || atom.AtomicNumber == 9)
            {
                feature.IsHBA = true;
            }

            
            if (atom.AtomicNumber == 7 && atom.BondCount == 4) feature.IsPositive = true; 
            if (atom.AtomicNumber == 8 && atom.BondCount == 1) feature.IsNegative = true; 

            return feature;
        }
        private BitArray GenerateConsensusFP(List<BitArray> individualFPs, int fpLength)
        {
            BitArray consensusFP = new BitArray(fpLength);//new List<int>(Enumerable.Repeat(0, fpLength));

            for (int i = 0; i < fpLength; i++)
            {
                int count = individualFPs.Count(fp => fp.Get(i) == true);
                if (count > individualFPs.Count / 2)
                {
                    consensusFP.Set(i,true);
                }
            }

            return consensusFP;
        }
        private float CalibrateSimilarityThreshold(List<BitArray> individualFPs, BitArray consensusFP)
        {
            List<float> similarities = new List<float>();
            foreach (var fp in individualFPs)
            {
                similarities.Add(_ecfp4Generator.CalculateTanimotoSimilarity(fp, consensusFP));
            }

            
            similarities.Sort();
            float median = similarities.Count % 2 == 0
                ? (similarities[similarities.Count / 2] + similarities[similarities.Count / 2 - 1]) / 2
                : similarities[similarities.Count / 2];

            
            return Mathf.Clamp(median, 0.6f, 0.8f);
        }
        private bool IsValidSMILES(string smiles)
        {
            try
            {
                var atoms = _ecfp4Generator.ParseSMILESToAtoms(smiles);
                return atoms.Count > 0 && atoms.Count < 100; 
            }
            catch
            {
                return false;
            }
        }
        #endregion

        #region Fingerprint library management: load, save, and update
        private void SaveFPLibrary(ReferenceFPLibrary fpLibrary)
        {
            string filePath = Path.Combine(_fullFPLibraryPath, $"{fpLibrary.TargetName}_{fpLibrary.FPType}.json");
            string json = JsonConvert.SerializeObject(fpLibrary, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }
        public ReferenceFPLibrary LoadFPLibrary(string targetName, FingerprintType fpType)
        {
            string filePath = Path.Combine(_fullFPLibraryPath, $"{targetName}_{fpType}.json");
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"Reference fingerprint generation status");
                return null;
            }

            string json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<ReferenceFPLibrary>(json);
        }
        public ReferenceFPLibrary UpdateFPLibrary(
            string targetName,
            FingerprintType fpType,
            List<string> newActiveSmiles)
        {
            
            ReferenceFPLibrary existingLibrary = LoadFPLibrary(targetName, fpType);
            if (existingLibrary == null)
            {
                return GenerateReferenceFPLibrary(targetName, newActiveSmiles, fpType);
            }

            
            foreach (var smiles in newActiveSmiles)
            {
                if (!existingLibrary.SourceSMILES.Contains(smiles) && IsValidSMILES(smiles))
                {
                    BitArray newFP = GenerateFingerprint(smiles, fpType, existingLibrary.FPLength);
                    existingLibrary.IndividualFPs.Add(newFP);
                    existingLibrary.SourceSMILES.Add(smiles);
                }
            }

            
            existingLibrary.ConsensusFP = GenerateConsensusFP(existingLibrary.IndividualFPs, existingLibrary.FPLength);
            existingLibrary.CalibratedThreshold = CalibrateSimilarityThreshold(existingLibrary.IndividualFPs, existingLibrary.ConsensusFP);

            
            SaveFPLibrary(existingLibrary);

            Debug.Log($"Reference fingerprint generation status");
            return existingLibrary;
        }
        #endregion

        #region Utility API: fingerprint similarity calculation
        public float CalculateFPSimilarity(
            string moleculeSmiles,
            ReferenceFPLibrary fpLibrary)
        {
            BitArray moleculeFP = GenerateFingerprint(moleculeSmiles, fpLibrary.FPType, fpLibrary.FPLength);
            return _ecfp4Generator.CalculateTanimotoSimilarity(moleculeFP, fpLibrary.ConsensusFP);
        }

        public float CalculateFPSimilarity(
            BitArray moleculeFP,
            ReferenceFPLibrary fpLibrary)
        {
            return _ecfp4Generator.CalculateTanimotoSimilarity(moleculeFP, fpLibrary.ConsensusFP);
        }

        #endregion
    }

    
    public class SimplifiedECFP4Generator
    {
        private const int ECFP4_RADIUS = 2;
        private const int FP_BIT_COUNT = 512;
        private const int HASH_SEED = 31;

        private readonly Dictionary<int, int> _atomicFeatureWeights = new Dictionary<int, int>()
    {
        {1, 1}, {6, 10}, {7, 20}, {8, 30}, {9, 40}, {16, 50}, {17, 60}
    };

        public class Atom
        {
            public int AtomicNumber;
            public int BondCount;
            public List<int> Neighbors;
            public int FeatureHash;

            public Atom(int atomicNumber)
            {
                AtomicNumber = atomicNumber;
                BondCount = 0;
                Neighbors = new List<int>();
                FeatureHash = 0;
            }
        }

        public List<int> GenerateECFP4(string smiles)
        {
            List<Atom> atoms = ParseSMILESToAtoms(smiles);
            if (atoms.Count == 0) return Enumerable.Repeat(0, FP_BIT_COUNT).ToList();

            InitAtomFeatureHashes(atoms);
            Dictionary<int, int> substructureHashes = new Dictionary<int, int>();
            for (int radius = 0; radius <= ECFP4_RADIUS; radius++)
            {
                substructureHashes = CalculateSubstructureHashes(atoms, substructureHashes, radius);
            }

            List<int> fingerprint = Enumerable.Repeat(0, FP_BIT_COUNT).ToList();
            foreach (var hash in substructureHashes.Values)
            {
                int bitIndex = Mathf.Abs(hash) % FP_BIT_COUNT;
                fingerprint[bitIndex] = 1;
            }

            return fingerprint;
        }

        public List<Atom> ParseSMILESToAtoms(string smiles)
        {
            List<Atom> atoms = new List<Atom>();
            if (string.IsNullOrEmpty(smiles)) return atoms;

            for (int i = 0; i < smiles.Length; i++)
            {
                char c = smiles[i];
                if (char.IsDigit(c) || c == '(' || c == ')' || c == '=' || c == '#' || c == '-')
                    continue;

                if (char.IsUpper(c))
                {
                    int atomicNumber = SymbolToAtomicNumber(c.ToString());
                    if (i + 1 < smiles.Length && char.IsLower(smiles[i + 1]))
                    {
                        string symbol = c.ToString() + smiles[i + 1];
                        atomicNumber = SymbolToAtomicNumber(symbol);
                        i++;
                    }

                    Atom atom = new Atom(atomicNumber);
                    atoms.Add(atom);
                    if (atoms.Count > 1)
                    {
                        int prevIdx = atoms.Count - 2;
                        atoms[prevIdx].Neighbors.Add(atoms.Count - 1);
                        atoms[prevIdx].BondCount++;
                        atom.Neighbors.Add(prevIdx);
                        atom.BondCount++;
                    }
                }
            }

            return atoms;
        }

        private int SymbolToAtomicNumber(string symbol)
        {
            return symbol.ToUpper() switch
            {
                "H" => 1,
                "C" => 6,
                "N" => 7,
                "O" => 8,
                "F" => 9,
                "S" => 16,
                "CL" => 17,
                _ => 6
            };
        }

        private void InitAtomFeatureHashes(List<Atom> atoms)
        {
            foreach (var atom in atoms)
            {
                int weight = _atomicFeatureWeights.TryGetValue(atom.AtomicNumber, out int w) ? w : 10;
                atom.FeatureHash = weight + atom.BondCount * HASH_SEED;
            }
        }

        private Dictionary<int, int> CalculateSubstructureHashes(List<Atom> atoms, Dictionary<int, int> prevHashes, int radius)
        {
            Dictionary<int, int> currentHashes = new Dictionary<int, int>();

            for (int i = 0; i < atoms.Count; i++)
            {
                if (radius == 0)
                {
                    currentHashes[i] = atoms[i].FeatureHash;
                }
                else
                {
                    List<int> neighborHashes = new List<int>();
                    foreach (int neighborIdx in atoms[i].Neighbors)
                    {
                        if (prevHashes.ContainsKey(neighborIdx))
                        {
                            neighborHashes.Add(prevHashes[neighborIdx]);
                        }
                    }

                    neighborHashes.Sort();
                    int hash = atoms[i].FeatureHash;
                    foreach (var nh in neighborHashes)
                    {
                        hash = hash * HASH_SEED + nh;
                    }
                    currentHashes[i] = hash;
                }
            }

            return currentHashes;
        }

        public float CalculateTanimotoSimilarity(BitArray fp1, BitArray fp2)
        {
            if (fp1.Count != fp2.Count) return 0f;

            int intersection = 0;
            int union = 0;

            for (int i = 0; i < fp1.Count; i++)
            {
                bool f1 = fp1.Get(i);
                bool f2 = fp2.Get(i);
                if (f1 && f2) intersection++;
                if (f1 || f2) union++;
            }

            return union == 0 ? 0f : (float)intersection / union;
        }
    }

}
