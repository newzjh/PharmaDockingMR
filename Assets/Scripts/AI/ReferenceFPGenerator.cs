using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using System.Collections; // 需导入Newtonsoft.Json包（Unity Package Manager安装）


namespace AIDrugDiscovery
{

    /// <summary>
    /// 通用参考指纹生成器
    /// 支持ECFP4/PHFP/STFP，适配任意靶点（1AQ1/3CLpro等）
    /// </summary>
    public class ReferenceFPGenerator : MonoBehaviour
    {
        #region 核心配置
        // 指纹类型枚举
        public enum FingerprintType
        {
            ECFP4,       // 扩展连接指纹（核心）
            PHFP,        // 药效团指纹
            STFP,        // 简化拓扑指纹
            FusedECFP4PHFP // ECFP4+PHFP融合指纹
        }

        // 全局配置
        private const int DEFAULT_FP_LENGTH = 512; // 默认指纹长度
        private const float SIMILARITY_THRESHOLD = 0.7f; // 默认相似度阈值
        private const string FP_LIBRARY_PATH = "ReferenceFP/"; // 指纹库存储路径
        #endregion

        #region 数据结构
        /// <summary>
        /// 参考指纹库数据结构
        /// </summary>
        [System.Serializable]
        public class ReferenceFPLibrary
        {
            public string TargetName; // 靶点名称（如1AQ1）
            public FingerprintType FPType; // 指纹类型
            public int FPLength; // 指纹长度
            public BitArray ConsensusFP; // 共识参考指纹（核心）
            public List<BitArray> IndividualFPs; // 单个活性配体指纹列表
            public List<string> SourceSMILES; // 来源SMILES列表
            public float CalibratedThreshold; // 校准后的相似度阈值
        }

        /// <summary>
        /// 药效团特征（用于PHFP生成）
        /// </summary>
        private struct PharmacophoreFeature
        {
            public bool IsHydrophobic; // 疏水特征
            public bool IsHBD; // 氢键供体
            public bool IsHBA; // 氢键受体
            public bool IsPositive; // 正电荷
            public bool IsNegative; // 负电荷

            // 特征哈希值
            public int GetHash()
            {
                return (IsHydrophobic ? 1 : 0) + (IsHBD ? 2 : 0) + (IsHBA ? 4 : 0) + (IsPositive ? 8 : 0) + (IsNegative ? 16 : 0);
            }
        }
        #endregion

        #region 核心依赖
        private SimplifiedECFP4Generator _ecfp4Generator; // 复用之前的ECFP4生成器
        private string _fullFPLibraryPath; // 指纹库完整存储路径
        #endregion

        #region 初始化
        private void Awake()
        {
            // 初始化依赖
            _ecfp4Generator = new SimplifiedECFP4Generator();

            // 创建指纹库存储目录
            _fullFPLibraryPath = Path.Combine(Application.streamingAssetsPath, FP_LIBRARY_PATH);
            if (!Directory.Exists(_fullFPLibraryPath))
            {
                Directory.CreateDirectory(_fullFPLibraryPath);
            }
        }
        #endregion

        #region 核心接口：生成参考指纹库
        /// <summary>
        /// 一键生成靶点参考指纹库（核心接口）
        /// </summary>
        /// <param name="targetName">靶点名称（如1AQ1）</param>
        /// <param name="activeSmilesList">活性配体SMILES列表</param>
        /// <param name="fpType">指纹类型</param>
        /// <param name="fpLength">指纹长度</param>
        /// <returns>参考指纹库</returns>
        public ReferenceFPLibrary GenerateReferenceFPLibrary(
            string targetName,
            List<string> activeSmilesList,
            FingerprintType fpType = FingerprintType.ECFP4,
            int fpLength = DEFAULT_FP_LENGTH)
        {
            // 1. 数据清洗：过滤无效SMILES
            List<string> validSmiles = activeSmilesList.Where(s => !string.IsNullOrEmpty(s) && IsValidSMILES(s)).ToList();
            if (validSmiles.Count == 0)
            {
                Debug.LogError($"靶点{targetName}无有效活性配体SMILES");
                return null;
            }

            // 2. 生成单个配体指纹
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
                Debug.LogError($"靶点{targetName}指纹生成失败");
                return null;
            }

            // 3. 生成共识指纹（多数投票）
            BitArray consensusFP = GenerateConsensusFP(individualFPs, fpLength);

            // 4. 校准相似度阈值
            float calibratedThreshold = CalibrateSimilarityThreshold(individualFPs, consensusFP);

            // 5. 构建指纹库
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

            // 6. 保存指纹库到本地
            SaveFPLibrary(fpLibrary);

            Debug.Log($"靶点{targetName}参考指纹库生成完成：\n" +
                      $"指纹类型：{fpType}\n" +
                      $"活性配体数：{validSmiles.Count}\n" +
                      $"校准阈值：{calibratedThreshold:F2}");

            return fpLibrary;
        }

        /// <summary>
        /// 从靶点口袋特征生成虚拟参考指纹（无实验数据时兜底）
        /// </summary>
        /// <param name="targetName">靶点名称</param>
        /// <param name="pocketFeatures">口袋特征（疏水/氢键供体/受体/电荷）</param>
        /// <param name="fpType">指纹类型</param>
        /// <returns>虚拟参考指纹库</returns>
        public ReferenceFPLibrary GenerateVirtualReferenceFP(
            string targetName,
            Dictionary<string, bool> pocketFeatures,
            FingerprintType fpType = FingerprintType.PHFP)
        {
            // 1. 构建虚拟药效团指纹
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

                // 补充口袋核心特征位
                if (feature.IsHydrophobic) virtualFP.Set(10,true);
                if (feature.IsHBD) virtualFP.Set(20,true);
                if (feature.IsHBA) virtualFP.Set(30,true);
            }

            // 2. 构建虚拟指纹库
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

            // 3. 保存虚拟指纹库
            SaveFPLibrary(fpLibrary);

            Debug.Log($"靶点{targetName}虚拟参考指纹库生成完成（基于口袋特征）");
            return fpLibrary;
        }
        #endregion

        #region 辅助方法：指纹生成/共识计算/阈值校准
        /// <summary>
        /// 生成单个分子的指定类型指纹
        /// </summary>
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

        /// <summary>
        /// 生成药效团指纹（PHFP）
        /// </summary>
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

        /// <summary>
        /// 生成简化拓扑指纹（STFP）
        /// </summary>
        private List<int> GenerateSTFP(string smiles, int fpLength)
        {
            List<int> stfp = new List<int>(Enumerable.Repeat(0, fpLength));
            List<SimplifiedECFP4Generator.Atom> atoms = _ecfp4Generator.ParseSMILESToAtoms(smiles);

            // 拓扑特征：原子数、键数、杂原子数
            int atomCount = atoms.Count;
            int bondCount = atoms.Sum(a => a.BondCount) / 2; // 避免重复计数
            int heteroAtomCount = atoms.Count(a => a.AtomicNumber != 6 && a.AtomicNumber != 1);

            // 映射到指纹位
            stfp[Mathf.Abs(atomCount) % fpLength] = 1;
            stfp[Mathf.Abs(bondCount + 100) % fpLength] = 1;
            stfp[Mathf.Abs(heteroAtomCount + 200) % fpLength] = 1;

            return stfp;
        }

        /// <summary>
        /// 获取原子的药效团特征
        /// </summary>
        private PharmacophoreFeature GetPharmacophoreFeature(SimplifiedECFP4Generator.Atom atom)
        {
            PharmacophoreFeature feature = new PharmacophoreFeature();

            // 疏水特征：C/S（非极性）
            if (atom.AtomicNumber == 6 || atom.AtomicNumber == 16)
            {
                feature.IsHydrophobic = true;
            }

            // 氢键供体：N/H（有氢连接）
            if (atom.AtomicNumber == 7 && atom.BondCount >= 3)
            {
                feature.IsHBD = true;
            }

            // 氢键受体：O/N/F（孤对电子）
            if (atom.AtomicNumber == 8 || atom.AtomicNumber == 7 || atom.AtomicNumber == 9)
            {
                feature.IsHBA = true;
            }

            // 电荷特征（简化）
            if (atom.AtomicNumber == 7 && atom.BondCount == 4) feature.IsPositive = true; // 带正电N
            if (atom.AtomicNumber == 8 && atom.BondCount == 1) feature.IsNegative = true; // 带负电O

            return feature;
        }

        /// <summary>
        /// 生成共识指纹（多数投票：超过50%配体的指纹位为1则置1）
        /// </summary>
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

        /// <summary>
        /// 校准相似度阈值（基于活性配体自比对）
        /// </summary>
        private float CalibrateSimilarityThreshold(List<BitArray> individualFPs, BitArray consensusFP)
        {
            List<float> similarities = new List<float>();
            foreach (var fp in individualFPs)
            {
                similarities.Add(_ecfp4Generator.CalculateTanimotoSimilarity(fp, consensusFP));
            }

            // 取中位数作为校准阈值（平衡召回率/精准率）
            similarities.Sort();
            float median = similarities.Count % 2 == 0
                ? (similarities[similarities.Count / 2] + similarities[similarities.Count / 2 - 1]) / 2
                : similarities[similarities.Count / 2];

            // 阈值下限0.6，上限0.8
            return Mathf.Clamp(median, 0.6f, 0.8f);
        }

        /// <summary>
        /// 验证SMILES有效性（简化版）
        /// </summary>
        private bool IsValidSMILES(string smiles)
        {
            try
            {
                var atoms = _ecfp4Generator.ParseSMILESToAtoms(smiles);
                return atoms.Count > 0 && atoms.Count < 100; // 原子数0或过多均无效
            }
            catch
            {
                return false;
            }
        }
        #endregion

        #region 指纹库管理：加载/保存/更新
        /// <summary>
        /// 保存指纹库到本地
        /// </summary>
        private void SaveFPLibrary(ReferenceFPLibrary fpLibrary)
        {
            string filePath = Path.Combine(_fullFPLibraryPath, $"{fpLibrary.TargetName}_{fpLibrary.FPType}.json");
            string json = JsonConvert.SerializeObject(fpLibrary, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// 加载靶点参考指纹库
        /// </summary>
        public ReferenceFPLibrary LoadFPLibrary(string targetName, FingerprintType fpType)
        {
            string filePath = Path.Combine(_fullFPLibraryPath, $"{targetName}_{fpType}.json");
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"靶点{targetName}的{fpType}指纹库不存在");
                return null;
            }

            string json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<ReferenceFPLibrary>(json);
        }

        /// <summary>
        /// 更新指纹库（新增活性配体）
        /// </summary>
        public ReferenceFPLibrary UpdateFPLibrary(
            string targetName,
            FingerprintType fpType,
            List<string> newActiveSmiles)
        {
            // 1. 加载现有指纹库
            ReferenceFPLibrary existingLibrary = LoadFPLibrary(targetName, fpType);
            if (existingLibrary == null)
            {
                return GenerateReferenceFPLibrary(targetName, newActiveSmiles, fpType);
            }

            // 2. 新增指纹
            foreach (var smiles in newActiveSmiles)
            {
                if (!existingLibrary.SourceSMILES.Contains(smiles) && IsValidSMILES(smiles))
                {
                    BitArray newFP = GenerateFingerprint(smiles, fpType, existingLibrary.FPLength);
                    existingLibrary.IndividualFPs.Add(newFP);
                    existingLibrary.SourceSMILES.Add(smiles);
                }
            }

            // 3. 重新生成共识指纹和校准阈值
            existingLibrary.ConsensusFP = GenerateConsensusFP(existingLibrary.IndividualFPs, existingLibrary.FPLength);
            existingLibrary.CalibratedThreshold = CalibrateSimilarityThreshold(existingLibrary.IndividualFPs, existingLibrary.ConsensusFP);

            // 4. 保存更新后的指纹库
            SaveFPLibrary(existingLibrary);

            Debug.Log($"靶点{targetName}指纹库已更新：新增{newActiveSmiles.Count}个活性配体");
            return existingLibrary;
        }
        #endregion

        #region 工具接口：指纹相似度计算
        /// <summary>
        /// 计算分子指纹与参考指纹的相似度
        /// </summary>
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

    // 复用之前的简化ECFP4生成器（确保代码完整性）
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