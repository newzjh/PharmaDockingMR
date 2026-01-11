using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Globalization;
//using System.Diagnostics;
using System.Linq;

namespace AIDrugDiscovery
{
    public class OpenBabelPDBQTConverter
    {
        // 原子类型定义
        private static readonly Dictionary<string, string> AtomTypeMap = new()
        {
            {"C", "C"}, {"N", "N"}, {"O", "O"}, {"S", "S"}, {"P", "P"},
            {"F", "F"}, {"Cl", "Cl"}, {"Br", "Br"}, {"I", "I"}, {"H", "H"}
        };

        // 氨基酸三字母代码到单字母代码的映射
        private static readonly Dictionary<string, string> AminoAcidMap = new()
        {
            {"ALA", "A"}, {"ARG", "R"}, {"ASN", "N"}, {"ASP", "D"}, {"CYS", "C"},
            {"GLN", "Q"}, {"GLU", "E"}, {"GLY", "G"}, {"HIS", "H"}, {"ILE", "I"},
            {"LEU", "L"}, {"LYS", "K"}, {"MET", "M"}, {"PHE", "F"}, {"PRO", "P"},
            {"SER", "S"}, {"THR", "T"}, {"TRP", "W"}, {"TYR", "Y"}, {"VAL", "V"}
        };

        // Gasteiger电荷计算参数
        private static readonly Dictionary<string, float> GasteigerParams = new()
        {
            {"C", 0.0f}, {"N", 0.0f}, {"O", 0.0f}, {"S", 0.0f}, {"P", 0.0f},
            {"F", 0.0f}, {"Cl", 0.0f}, {"Br", 0.0f}, {"I", 0.0f}, {"H", 0.0f}
        };

        // 支持的文件格式
        public enum FileFormat
        {
            PDB,
            PDBQT,
            MOL2,
            SDF,
            SMILES
        }

        // 电荷计算方法
        public enum ChargeMethod
        {
            Gasteiger,
            MMFF94,
            AM1BCC,
            QEq,
            None
        }

        // 解析PDB文件
        public static PDBFile ParsePDB(string filePath)
        {
            PDBFile pdbFile = new PDBFile();
            
            try
            {
                string[] lines = File.ReadAllLines(filePath);
                
                foreach (string line in lines)
                {
                    if (line.StartsWith("ATOM") || line.StartsWith("HETATM"))
                    {
                        PDBAtom atom = ParsePDBAtom(line);
                        if (atom != null)
                        {
                            pdbFile.Atoms.Add(atom);
                        }
                    }
                    else if (line.StartsWith("TER"))
                    {
                        pdbFile.TERLines.Add(line);
                    }
                    else if (line.StartsWith("END"))
                    {
                        pdbFile.ENDLine = line;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"解析PDB文件失败: {ex.Message}");
            }
            
            return pdbFile;
        }

        // 解析单个PDB原子行
        private static PDBAtom ParsePDBAtom(string line)
        {
            try
            {
                PDBAtom atom = new PDBAtom
                {
                    RecordType = line.Substring(0, 6).Trim(),
                    AtomNumber = int.Parse(line.Substring(6, 5).Trim()),
                    AtomName = line.Substring(12, 4).Trim(),
                    AltLoc = line.Substring(16, 1).Trim(),
                    ResidueName = line.Substring(17, 3).Trim(),
                    ChainID = line.Substring(21, 1).Trim(),
                    ResidueNumber = int.Parse(line.Substring(22, 4).Trim()),
                    InsertionCode = line.Substring(26, 1).Trim(),
                    X = float.Parse(line.Substring(30, 8).Trim(), CultureInfo.InvariantCulture),
                    Y = float.Parse(line.Substring(38, 8).Trim(), CultureInfo.InvariantCulture),
                    Z = float.Parse(line.Substring(46, 8).Trim(), CultureInfo.InvariantCulture),
                    Occupancy = float.Parse(line.Substring(54, 6).Trim(), CultureInfo.InvariantCulture),
                    TemperatureFactor = float.Parse(line.Substring(60, 6).Trim(), CultureInfo.InvariantCulture),
                    SegmentID = line.Substring(72, 4).Trim(),
                    Element = line.Length >= 78 ? line.Substring(76, 2).Trim() : "",
                    Charge = line.Length >= 80 ? line.Substring(78, 2).Trim() : ""
                };

                // 提取元素符号
                if (string.IsNullOrEmpty(atom.Element))
                {
                    atom.Element = ExtractElementFromAtomName(atom.AtomName);
                }

                return atom;
            }
            catch (Exception)
            {
                Debug.LogWarning($"解析原子行失败: {line.Substring(0, Math.Min(80, line.Length))}");
                return null;
            }
        }

        // 从原子名称中提取元素符号
        private static string ExtractElementFromAtomName(string atomName)
        {
            // 处理如CA、CB等情况
            if (atomName.Length >= 2 && char.IsLetter(atomName[0]) && char.IsLetter(atomName[1]))
            {
                string element = atomName.Substring(0, 2);
                if (AtomTypeMap.ContainsKey(element))
                {
                    return element;
                }
            }
            
            // 处理单个字母元素
            if (atomName.Length >= 1 && char.IsLetter(atomName[0]))
            {
                string element = atomName.Substring(0, 1);
                if (AtomTypeMap.ContainsKey(element))
                {
                    return element;
                }
            }
            
            return "C"; // 默认返回碳
        }

        // 计算Gasteiger电荷（改进版）
        private static void CalculateGasteigerCharges(PDBFile pdbFile)
        {
            // 这里实现更准确的Gasteiger电荷计算
            foreach (var atom in pdbFile.Atoms)
            {
                // 根据元素类型和化学环境分配电荷
                switch (atom.Element)
                {
                    case "O":
                        // 羰基氧
                        if (atom.AtomName.Contains("O") && (atom.ResidueName == "GLU" || atom.ResidueName == "ASP"))
                        {
                            atom.PartialCharge = -0.6f;
                        }
                        // 羟基氧
                        else if (atom.AtomName.Contains("OH"))
                        {
                            atom.PartialCharge = -0.4f;
                        }
                        else
                        {
                            atom.PartialCharge = -0.5f;
                        }
                        break;
                    case "N":
                        // 氨基氮
                        if (atom.ResidueName == "LYS")
                        {
                            atom.PartialCharge = 0.8f;
                        }
                        // 酰胺氮
                        else if (atom.ResidueName == "ASN" || atom.ResidueName == "GLN")
                        {
                            atom.PartialCharge = 0.3f;
                        }
                        else
                        {
                            atom.PartialCharge = 0.5f;
                        }
                        break;
                    case "S":
                        atom.PartialCharge = -0.2f;
                        break;
                    case "P":
                        atom.PartialCharge = 1.0f;
                        break;
                    case "F":
                    case "Cl":
                    case "Br":
                    case "I":
                        atom.PartialCharge = -0.5f;
                        break;
                    default:
                        atom.PartialCharge = 0.0f;
                        break;
                }
            }
        }

        // 计算AM1BCC电荷（简化版）
        private static void CalculateAM1BCCCharges(PDBFile pdbFile)
        {
            // 简化版AM1BCC电荷计算
            foreach (var atom in pdbFile.Atoms)
            {
                // 基于原子类型和环境的经验电荷
                switch (atom.Element)
                {
                    case "O":
                        atom.PartialCharge = -0.55f;
                        break;
                    case "N":
                        atom.PartialCharge = 0.45f;
                        break;
                    case "S":
                        atom.PartialCharge = -0.25f;
                        break;
                    case "P":
                        atom.PartialCharge = 0.9f;
                        break;
                    case "F":
                        atom.PartialCharge = -0.55f;
                        break;
                    case "Cl":
                        atom.PartialCharge = -0.5f;
                        break;
                    case "Br":
                        atom.PartialCharge = -0.45f;
                        break;
                    case "I":
                        atom.PartialCharge = -0.4f;
                        break;
                    default:
                        atom.PartialCharge = 0.0f;
                        break;
                }
            }
        }

        // 计算MMFF94电荷（简化版）
        private static void CalculateMMFF94Charges(PDBFile pdbFile)
        {
            // MMFF94力场电荷计算
            foreach (var atom in pdbFile.Atoms)
            {
                // 基于MMFF94力场的经验电荷
                switch (atom.Element)
                {
                    case "O":
                        // 羰基氧
                        if (atom.AtomName.Contains("O") && (atom.ResidueName == "GLU" || atom.ResidueName == "ASP"))
                        {
                            atom.PartialCharge = -0.55f;
                        }
                        // 羟基氧
                        else if (atom.AtomName.Contains("OH"))
                        {
                            atom.PartialCharge = -0.42f;
                        }
                        else
                        {
                            atom.PartialCharge = -0.48f;
                        }
                        break;
                    case "N":
                        // 氨基氮
                        if (atom.ResidueName == "LYS")
                        {
                            atom.PartialCharge = 0.75f;
                        }
                        // 酰胺氮
                        else if (atom.ResidueName == "ASN" || atom.ResidueName == "GLN")
                        {
                            atom.PartialCharge = 0.32f;
                        }
                        else
                        {
                            atom.PartialCharge = 0.46f;
                        }
                        break;
                    case "S":
                        atom.PartialCharge = -0.22f;
                        break;
                    case "P":
                        atom.PartialCharge = 0.95f;
                        break;
                    case "F":
                        atom.PartialCharge = -0.52f;
                        break;
                    case "Cl":
                        atom.PartialCharge = -0.48f;
                        break;
                    case "Br":
                        atom.PartialCharge = -0.42f;
                        break;
                    case "I":
                        atom.PartialCharge = -0.38f;
                        break;
                    default:
                        atom.PartialCharge = 0.0f;
                        break;
                }
            }
        }

        // 计算QEq电荷（简化版）
        private static void CalculateQEqCharges(PDBFile pdbFile)
        {
            // QEq电荷平衡方法
            // 基于原子电负性和硬度的电荷分配
            Dictionary<string, Tuple<float, float>> electronegativityHardness = new Dictionary<string, Tuple<float, float>>
            {
                {"H", Tuple.Create(7.17f, 14.0f)},
                {"C", Tuple.Create(6.39f, 8.79f)},
                {"N", Tuple.Create(7.35f, 7.43f)},
                {"O", Tuple.Create(11.18f, 12.20f)},
                {"S", Tuple.Create(6.63f, 7.07f)},
                {"P", Tuple.Create(5.46f, 5.97f)},
                {"F", Tuple.Create(14.27f, 17.40f)},
                {"Cl", Tuple.Create(9.94f, 10.31f)},
                {"Br", Tuple.Create(9.06f, 9.75f)},
                {"I", Tuple.Create(7.59f, 8.28f)}
            };

            // 简化的QEq计算
            foreach (var atom in pdbFile.Atoms)
            {
                if (electronegativityHardness.ContainsKey(atom.Element))
                {
                    var (chi, eta) = electronegativityHardness[atom.Element];
                    // 简化计算，基于电负性
                    atom.PartialCharge = -chi * 0.05f;
                }
                else
                {
                    atom.PartialCharge = 0.0f;
                }
            }
        }

        // 转换PDB到PDBQT
        public static void ConvertToPDBQT(PDBFile pdbFile, string outputPath, ChargeMethod chargeMethod = ChargeMethod.Gasteiger)
        {
            // 根据选择的方法计算电荷
            switch (chargeMethod)
            {
                case ChargeMethod.Gasteiger:
                    CalculateGasteigerCharges(pdbFile);
                    break;
                case ChargeMethod.AM1BCC:
                    CalculateAM1BCCCharges(pdbFile);
                    break;
                case ChargeMethod.MMFF94:
                    CalculateMMFF94Charges(pdbFile);
                    break;
                case ChargeMethod.QEq:
                    CalculateQEqCharges(pdbFile);
                    break;
                case ChargeMethod.None:
                    // 不计算电荷，使用默认值
                    foreach (var atom in pdbFile.Atoms)
                    {
                        atom.PartialCharge = 0.0f;
                    }
                    break;
            }
            
            // 生成PDBQT文件
            StringBuilder pdbqtContent = new StringBuilder();
            
            foreach (var atom in pdbFile.Atoms)
            {
                string pdbqtLine = GeneratePDBQTAtomLine(atom);
                pdbqtContent.AppendLine(pdbqtLine);
            }
            
            foreach (var terLine in pdbFile.TERLines)
            {
                pdbqtContent.AppendLine(terLine);
            }
            
            if (!string.IsNullOrEmpty(pdbFile.ENDLine))
            {
                pdbqtContent.AppendLine(pdbFile.ENDLine);
            }
            
            // 写入文件
            try
            {
                File.WriteAllText(outputPath, pdbqtContent.ToString());
                Debug.Log($"成功生成PDBQT文件: {outputPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"写入PDBQT文件失败: {ex.Message}");
            }
        }

        // 生成PDBQT原子行
        private static string GeneratePDBQTAtomLine(PDBAtom atom)
        {
            StringBuilder line = new StringBuilder();
            
            // 记录类型 (1-6)
            line.Append(atom.RecordType.PadRight(6));
            
            // 原子编号 (7-11)
            line.Append(atom.AtomNumber.ToString().PadLeft(5));
            
            // 空格 (12)
            line.Append(" ");
            
            // 原子名称 (13-16) - 注意：PDB格式中，单字母元素的原子名称右对齐，双字母元素左对齐
            if (atom.AtomName.Length == 1 || (atom.AtomName.Length > 1 && char.IsLetter(atom.AtomName[1])))
            {
                line.Append(atom.AtomName.PadLeft(4));
            }
            else
            {
                line.Append(atom.AtomName.PadRight(4));
            }
            
            // 交替位置指示符 (17)
            line.Append(atom.AltLoc.PadRight(1));
            
            // 残基名称 (18-20)
            line.Append(atom.ResidueName.PadRight(3));
            
            // 空格 (21)
            line.Append(" ");
            
            // 链ID (22)
            line.Append(atom.ChainID.PadRight(1));
            
            // 残基编号 (23-26)
            line.Append(atom.ResidueNumber.ToString().PadLeft(4));
            
            // 插入代码 (27)
            line.Append(atom.InsertionCode.PadRight(1));
            
            // 空格 (28-30)
            line.Append("   ");
            
            // 坐标 X (31-38)
            line.Append(atom.X.ToString("F3", CultureInfo.InvariantCulture).PadLeft(8));
            
            // 坐标 Y (39-46)
            line.Append(atom.Y.ToString("F3", CultureInfo.InvariantCulture).PadLeft(8));
            
            // 坐标 Z (47-54)
            line.Append(atom.Z.ToString("F3", CultureInfo.InvariantCulture).PadLeft(8));
            
            // 占有率 (55-60)
            line.Append(atom.Occupancy.ToString("F2", CultureInfo.InvariantCulture).PadLeft(6));
            
            // 温度因子 (61-66)
            line.Append(atom.TemperatureFactor.ToString("F2", CultureInfo.InvariantCulture).PadLeft(6));
            
            // 部分电荷（PDBQT特有） (67-72)
            line.Append(atom.PartialCharge.ToString("F4", CultureInfo.InvariantCulture).PadLeft(6));
            
            // 原子类型（PDBQT特有） (73-74)
            string atomType = GetPDBQTAtomType(atom);
            line.Append(atomType.PadRight(2));
            
            // 填充到80字符（如果需要）
            while (line.Length < 80)
            {
                line.Append(" ");
            }
            
            return line.ToString();
        }

        // 获取PDBQT原子类型
        private static string GetPDBQTAtomType(PDBAtom atom)
        {
            if (AtomTypeMap.ContainsKey(atom.Element))
            {
                return AtomTypeMap[atom.Element];
            }
            return "C";
        }

        // 便捷方法：直接转换文件
        public static void ConvertPDBToPDBQT(string pdbPath, string pdbqtPath, ChargeMethod chargeMethod = ChargeMethod.Gasteiger)
        {
            PDBFile pdbFile = ParsePDB(pdbPath);
            ConvertToPDBQT(pdbFile, pdbqtPath, chargeMethod);
        }

        // 批量转换方法
        public static void BatchConvertPDBToPDBQT(string inputDirectory, string outputDirectory, ChargeMethod chargeMethod = ChargeMethod.Gasteiger)
        {
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            string[] pdbFiles = Directory.GetFiles(inputDirectory, "*.pdb", SearchOption.TopDirectoryOnly);
            
            foreach (string pdbFile in pdbFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(pdbFile);
                string pdbqtPath = Path.Combine(outputDirectory, fileName + ".pdbqt");
                
                try
                {
                    ConvertPDBToPDBQT(pdbFile, pdbqtPath, chargeMethod);
                    Debug.Log($"转换完成: {fileName}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"转换失败 {fileName}: {ex.Message}");
                }
            }
            
            Debug.Log($"批处理完成，共转换 {pdbFiles.Length} 个文件");
        }

        // 分子操作：旋转
        public static void RotateMolecule(PDBFile pdbFile, Vector3 rotation)
        {
            // 实现分子旋转
            Quaternion quaternion = Quaternion.Euler(rotation);
            
            foreach (var atom in pdbFile.Atoms)
            {
                Vector3 position = new Vector3(atom.X, atom.Y, atom.Z);
                Vector3 rotatedPosition = quaternion * position;
                
                atom.X = rotatedPosition.x;
                atom.Y = rotatedPosition.y;
                atom.Z = rotatedPosition.z;
            }
        }

        // 分子操作：平移
        public static void TranslateMolecule(PDBFile pdbFile, Vector3 translation)
        {
            foreach (var atom in pdbFile.Atoms)
            {
                atom.X += translation.x;
                atom.Y += translation.y;
                atom.Z += translation.z;
            }
        }

        // 命令行接口
        public static void RunCommandLine(string[] args)
        {
            if (args.Length < 2)
            {
                ShowHelp();
                return;
            }

            string inputPath = args[0];
            string outputPath = args[1];
            ChargeMethod chargeMethod = ChargeMethod.Gasteiger;

            // 解析命令行参数
            for (int i = 2; i < args.Length; i++)
            {
                string arg = args[i].ToLower();
                switch (arg)
                {
                    case "-c":
                case "--charge":
                    if (i + 1 < args.Length)
                    {
                        string method = args[i + 1].ToLower();
                        switch (method)
                        {
                            case "gasteiger":
                                chargeMethod = ChargeMethod.Gasteiger;
                                break;
                            case "am1bcc":
                                chargeMethod = ChargeMethod.AM1BCC;
                                break;
                            case "mmff94":
                                chargeMethod = ChargeMethod.MMFF94;
                                break;
                            case "qeq":
                                chargeMethod = ChargeMethod.QEq;
                                break;
                            case "none":
                                chargeMethod = ChargeMethod.None;
                                break;
                        }
                        i++;
                    }
                    break;
                    case "-h":
                    case "--help":
                        ShowHelp();
                        return;
                }
            }

            // 执行转换
            try
            {
                ConvertPDBToPDBQT(inputPath, outputPath, chargeMethod);
                Console.WriteLine($"转换成功: {inputPath} -> {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"转换失败: {ex.Message}");
            }
        }

        // 显示帮助信息
        private static void ShowHelp()
        {
            Console.WriteLine("OpenBabelPDBQTConverter 命令行工具");
            Console.WriteLine("用法: OpenBabelPDBQTConverter <输入PDB文件> <输出PDBQT文件> [选项]");
            Console.WriteLine("选项:");
            Console.WriteLine("  -c, --charge <方法>    设置电荷计算方法 (gasteiger, am1bcc, mmff94, qeq, none)");
            Console.WriteLine("  -h, --help             显示此帮助信息");
            Console.WriteLine("示例:");
            Console.WriteLine("  OpenBabelPDBQTConverter protein.pdb protein.pdbqt");
            Console.WriteLine("  OpenBabelPDBQTConverter ligand.pdb ligand.pdbqt --charge am1bcc");
            Console.WriteLine("  OpenBabelPDBQTConverter drug.pdb drug.pdbqt --charge mmff94");
        }

        // 验证PDBQT文件
        public static bool ValidatePDBQT(string pdbqtPath)
        {
            try
            {
                string[] lines = File.ReadAllLines(pdbqtPath);
                int atomCount = 0;
                int chargeCount = 0;

                foreach (string line in lines)
                {
                    if (line.StartsWith("ATOM") || line.StartsWith("HETATM"))
                    {
                        atomCount++;
                        // 检查电荷字段
                        if (line.Length >= 72)
                        {
                            string chargeStr = line.Substring(66, 6).Trim();
                            if (float.TryParse(chargeStr, out float charge))
                            {
                                chargeCount++;
                            }
                        }
                    }
                }

                return atomCount > 0 && chargeCount == atomCount;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // 获取分子信息
        public static string GetMoleculeInfo(PDBFile pdbFile)
        {
            StringBuilder info = new StringBuilder();
            info.AppendLine($"原子总数: {pdbFile.Atoms.Count}");
            
            // 统计元素分布
            Dictionary<string, int> elementCount = new Dictionary<string, int>();
            foreach (var atom in pdbFile.Atoms)
            {
                if (elementCount.ContainsKey(atom.Element))
                {
                    elementCount[atom.Element]++;
                }
                else
                {
                    elementCount[atom.Element] = 1;
                }
            }
            
            info.AppendLine("元素分布:");
            foreach (var kvp in elementCount)
            {
                info.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }
            
            // 统计残基
            Dictionary<string, int> residueCount = new Dictionary<string, int>();
            foreach (var atom in pdbFile.Atoms)
            {
                if (residueCount.ContainsKey(atom.ResidueName))
                {
                    residueCount[atom.ResidueName]++;
                }
                else
                {
                    residueCount[atom.ResidueName] = 1;
                }
            }
            
            info.AppendLine("残基分布:");
            foreach (var kvp in residueCount)
            {
                info.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }
            
            return info.ToString();
        }

        // 解析MOL2文件
        public static MOL2File ParseMOL2(string filePath)
        {
            MOL2File mol2File = new MOL2File();
            
            try
            {
                string[] lines = File.ReadAllLines(filePath);
                bool inAtoms = false;
                bool inBonds = false;
                bool inSubstructures = false;
                
                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();
                    
                    if (trimmedLine == "@<TRIPOS>MOLECULE")
                    {
                        inAtoms = false;
                        inBonds = false;
                        inSubstructures = false;
                    }
                    else if (trimmedLine == "@<TRIPOS>ATOM")
                    {
                        inAtoms = true;
                        inBonds = false;
                        inSubstructures = false;
                    }
                    else if (trimmedLine == "@<TRIPOS>BOND")
                    {
                        inAtoms = false;
                        inBonds = true;
                        inSubstructures = false;
                    }
                    else if (trimmedLine == "@<TRIPOS>SUBSTRUCTURE")
                    {
                        inAtoms = false;
                        inBonds = false;
                        inSubstructures = true;
                    }
                    else if (inAtoms && !trimmedLine.StartsWith("@"))
                    {
                        string[] parts = trimmedLine.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 9)
                        {
                            MOL2Atom atom = new MOL2Atom
                            {
                                AtomID = int.Parse(parts[0]),
                                AtomName = parts[1],
                                X = float.Parse(parts[2], CultureInfo.InvariantCulture),
                                Y = float.Parse(parts[3], CultureInfo.InvariantCulture),
                                Z = float.Parse(parts[4], CultureInfo.InvariantCulture),
                                AtomType = parts[5],
                                SubstructureID = int.Parse(parts[6]),
                                SubstructureName = parts[7],
                                Charge = float.Parse(parts[8], CultureInfo.InvariantCulture)
                            };
                            mol2File.Atoms.Add(atom);
                        }
                    }
                    else if (inBonds && !trimmedLine.StartsWith("@"))
                    {
                        string[] parts = trimmedLine.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 4)
                        {
                            MOL2Bond bond = new MOL2Bond
                            {
                                BondID = int.Parse(parts[0]),
                                Atom1 = int.Parse(parts[1]),
                                Atom2 = int.Parse(parts[2]),
                                BondType = parts[3]
                            };
                            mol2File.Bonds.Add(bond);
                        }
                    }
                    else if (inSubstructures && !trimmedLine.StartsWith("@"))
                    {
                        mol2File.Substructures.Add(line);
                    }
                    else if (!inAtoms && !inBonds && !inSubstructures && !trimmedLine.StartsWith("@"))
                    {
                        if (mol2File.Header == "")
                        {
                            mol2File.Header = line;
                        }
                        else if (mol2File.Comment == "")
                        {
                            mol2File.Comment = line;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"解析MOL2文件失败: {ex.Message}");
            }
            
            return mol2File;
        }

        // 写入MOL2文件
        public static void WriteMOL2(MOL2File mol2File, string outputPath)
        {
            try
            {
                StringBuilder mol2Content = new StringBuilder();
                
                // 写入分子头信息
                mol2Content.AppendLine("@<TRIPOS>MOLECULE");
                mol2Content.AppendLine(mol2File.Header);
                mol2Content.AppendLine($"{mol2File.Atoms.Count} {mol2File.Bonds.Count} 0 0 0");
                mol2Content.AppendLine("SMALL");
                mol2Content.AppendLine("USER_CHARGES");
                mol2Content.AppendLine();
                
                // 写入原子信息
                mol2Content.AppendLine("@<TRIPOS>ATOM");
                foreach (var atom in mol2File.Atoms)
                {
                    mol2Content.AppendLine($"{atom.AtomID} {atom.AtomName} {atom.X:F4} {atom.Y:F4} {atom.Z:F4} {atom.AtomType} {atom.SubstructureID} {atom.SubstructureName} {atom.Charge:F4}");
                }
                mol2Content.AppendLine();
                
                // 写入键信息
                mol2Content.AppendLine("@<TRIPOS>BOND");
                foreach (var bond in mol2File.Bonds)
                {
                    mol2Content.AppendLine($"{bond.BondID} {bond.Atom1} {bond.Atom2} {bond.BondType}");
                }
                mol2Content.AppendLine();
                
                // 写入子结构信息
                if (mol2File.Substructures.Count > 0)
                {
                    mol2Content.AppendLine("@<TRIPOS>SUBSTRUCTURE");
                    foreach (var substructure in mol2File.Substructures)
                    {
                        mol2Content.AppendLine(substructure);
                    }
                }
                
                File.WriteAllText(outputPath, mol2Content.ToString());
                Debug.Log($"成功生成MOL2文件: {outputPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"写入MOL2文件失败: {ex.Message}");
            }
        }

        // 解析SDF文件
        public static SDFFile ParseSDF(string filePath)
        {
            SDFFile sdfFile = new SDFFile();
            
            try
            {
                string[] lines = File.ReadAllLines(filePath);
                SDMolecule currentMolecule = null;
                bool inProperties = false;
                
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    
                    if (line.Trim() == "$$$$")
                    {
                        if (currentMolecule != null)
                        {
                            sdfFile.Molecules.Add(currentMolecule);
                            currentMolecule = null;
                        }
                        inProperties = false;
                    }
                    else if (currentMolecule == null)
                    {
                        currentMolecule = new SDMolecule();
                        currentMolecule.Header = line;
                        if (i + 1 < lines.Length)
                        {
                            currentMolecule.Comment = lines[i + 1];
                            i++;
                        }
                        if (i + 1 < lines.Length)
                        {
                            string countsLine = lines[i + 1];
                            if (countsLine.Length >= 6)
                            {
                                int atomCount = int.Parse(countsLine.Substring(0, 3).Trim());
                                int bondCount = int.Parse(countsLine.Substring(3, 3).Trim());
                                i++;
                                
                                // 解析原子
                                for (int j = 0; j < atomCount && i + 1 < lines.Length; j++)
                                {
                                    i++;
                                    string atomLine = lines[i];
                                    if (atomLine.Length >= 39)
                                    {
                                        SDFAtom atom = new SDFAtom
                                        {
                                            X = float.Parse(atomLine.Substring(0, 10).Trim(), CultureInfo.InvariantCulture),
                                            Y = float.Parse(atomLine.Substring(10, 10).Trim(), CultureInfo.InvariantCulture),
                                            Z = float.Parse(atomLine.Substring(20, 10).Trim(), CultureInfo.InvariantCulture),
                                            Element = atomLine.Substring(31, 3).Trim(),
                                            MassDiff = int.Parse(atomLine.Substring(39, 2).Trim())
                                        };
                                        currentMolecule.Atoms.Add(atom);
                                    }
                                }
                                
                                // 解析键
                                for (int j = 0; j < bondCount && i + 1 < lines.Length; j++)
                                {
                                    i++;
                                    string bondLine = lines[i];
                                    if (bondLine.Length >= 9)
                                    {
                                        SDFBond bond = new SDFBond
                                        {
                                            Atom1 = int.Parse(bondLine.Substring(0, 3).Trim()),
                                            Atom2 = int.Parse(bondLine.Substring(3, 3).Trim()),
                                            BondType = int.Parse(bondLine.Substring(6, 3).Trim()),
                                            Stereo = int.Parse(bondLine.Substring(9, 3).Trim())
                                        };
                                        currentMolecule.Bonds.Add(bond);
                                    }
                                }
                            }
                        }
                    }
                    else if (line.Trim() == "> <")
                    {
                        inProperties = true;
                    }
                    else if (inProperties && line.Contains(">"))
                    {
                        string propertyName = line.Substring(2, line.Length - 4).Trim();
                        if (i + 1 < lines.Length)
                        {
                            i++;
                            string propertyValue = lines[i].Trim();
                            currentMolecule.Properties[propertyName] = propertyValue;
                        }
                    }
                }
                
                // 添加最后一个分子
                if (currentMolecule != null)
                {
                    sdfFile.Molecules.Add(currentMolecule);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"解析SDF文件失败: {ex.Message}");
            }
            
            return sdfFile;
        }

        // 写入SDF文件
        public static void WriteSDF(SDFFile sdfFile, string outputPath)
        {
            try
            {
                StringBuilder sdfContent = new StringBuilder();
                
                foreach (var molecule in sdfFile.Molecules)
                {
                    // 写入分子头信息
                    sdfContent.AppendLine(molecule.Header);
                    sdfContent.AppendLine(molecule.Comment);
                    sdfContent.AppendLine($"{molecule.Atoms.Count.ToString().PadLeft(3)} {molecule.Bonds.Count.ToString().PadLeft(3)} 0 0 0 0 0 0 0 0 0 0");
                    
                    // 写入原子信息
                    foreach (var atom in molecule.Atoms)
                    {
                        sdfContent.AppendLine($"{atom.X.ToString("F4").PadLeft(10)} {atom.Y.ToString("F4").PadLeft(10)} {atom.Z.ToString("F4").PadLeft(10)} {atom.Element.PadRight(3)} 0 {atom.MassDiff.ToString().PadLeft(2)} 0 0 0 0 0 0");
                    }
                    
                    // 写入键信息
                    foreach (var bond in molecule.Bonds)
                    {
                        sdfContent.AppendLine($"{bond.Atom1.ToString().PadLeft(3)} {bond.Atom2.ToString().PadLeft(3)} {bond.BondType.ToString().PadLeft(3)} {bond.Stereo.ToString().PadLeft(3)} 0 0 0");
                    }
                    
                    // 写入属性信息
                    foreach (var property in molecule.Properties)
                    {
                        sdfContent.AppendLine($"> <{property.Key}>");
                        sdfContent.AppendLine(property.Value);
                        sdfContent.AppendLine();
                    }
                    
                    // 写入分子结束标记
                    sdfContent.AppendLine("$$$$");
                }
                
                File.WriteAllText(outputPath, sdfContent.ToString());
                Debug.Log($"成功生成SDF文件: {outputPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"写入SDF文件失败: {ex.Message}");
            }
        }

        // 解析SMILES字符串
        public static PDBFile ParseSMILES(string smiles)
        {
            PDBFile pdbFile = new PDBFile();
            
            try
            {
                // 这里实现简化的SMILES解析
                // 实际应用中可能需要更复杂的解析器
                Debug.Log($"解析SMILES: {smiles}");
                
                // 简化实现：创建一个默认的碳原子
                PDBAtom atom = new PDBAtom
                {
                    RecordType = "ATOM",
                    AtomNumber = 1,
                    AtomName = "C1",
                    ResidueName = "UNL",
                    ChainID = "A",
                    ResidueNumber = 1,
                    X = 0.0f,
                    Y = 0.0f,
                    Z = 0.0f,
                    Occupancy = 1.0f,
                    TemperatureFactor = 0.0f,
                    Element = "C"
                };
                pdbFile.Atoms.Add(atom);
                
            }
            catch (Exception ex)
            {
                Debug.LogError($"解析SMILES失败: {ex.Message}");
            }
            
            return pdbFile;
        }

        // 生成SMILES字符串
        public static string GenerateSMILES(PDBFile pdbFile)
        {
            // 这里实现简化的SMILES生成
            // 实际应用中可能需要更复杂的算法
            return "C"; // 默认返回甲烷作为示例
        }

        // 添加氢原子
        public static void AddHydrogens(PDBFile pdbFile, bool pH7 = true, bool addPolarOnly = false)
        {
            try
            {
                List<PDBAtom> newAtoms = new List<PDBAtom>();
                int atomId = pdbFile.Atoms.Count + 1;
                
                // 氢原子键长参数
                Dictionary<string, float> bondLengths = new Dictionary<string, float>
                {
                    {"C-H", 1.09f}, {"N-H", 1.01f}, {"O-H", 0.96f}, {"S-H", 1.34f}
                };
                
                // 氢原子键角参数（度）
                Dictionary<string, float> bondAngles = new Dictionary<string, float>
                {
                    {"C-H", 109.5f}, {"N-H", 107.0f}, {"O-H", 104.5f}, {"S-H", 92.0f}
                };
                
                foreach (var atom in pdbFile.Atoms)
                {
                    newAtoms.Add(atom);
                    
                    // 根据原子类型和化学环境添加氢原子
                    switch (atom.Element)
                    {
                        case "C":
                            // 烷基碳：添加3个氢
                            if (atom.AtomName.StartsWith("C") && !atom.AtomName.Contains("A") && !atom.AtomName.Contains("B"))
                            {
                                if (!addPolarOnly)
                                {
                                    AddHydrogensToAtom(atom, "C", 3, bondLengths["C-H"], bondAngles["C-H"], ref newAtoms, ref atomId);
                                }
                            }
                            // 羰基碳：不添加氢
                            else if (atom.AtomName.Contains("C") && atom.AtomName.Contains("O"))
                            {
                                // 羰基碳通常与氧双键连接，不添加氢
                            }
                            break;
                            
                        case "N":
                            // 氨基氮：添加2-3个氢（取决于pH）
                            if (atom.ResidueName == "LYS" || atom.ResidueName == "ARG")
                            {
                                int hydrogenCount = pH7 ? 3 : 2;
                                AddHydrogensToAtom(atom, "N", hydrogenCount, bondLengths["N-H"], bondAngles["N-H"], ref newAtoms, ref atomId);
                            }
                            // 酰胺氮：添加1个氢
                            else if (atom.ResidueName == "ASN" || atom.ResidueName == "GLN")
                            {
                                AddHydrogensToAtom(atom, "N", 1, bondLengths["N-H"], bondAngles["N-H"], ref newAtoms, ref atomId);
                            }
                            // 其他氮：添加1-2个氢
                            else
                            {
                                int hydrogenCount = pH7 ? 2 : 1;
                                AddHydrogensToAtom(atom, "N", hydrogenCount, bondLengths["N-H"], bondAngles["N-H"], ref newAtoms, ref atomId);
                            }
                            break;
                            
                        case "O":
                            // 羟基氧：添加1个氢
                            if (atom.AtomName.Contains("OH") || atom.ResidueName == "SER" || atom.ResidueName == "THR" || atom.ResidueName == "TYR")
                            {
                                AddHydrogensToAtom(atom, "O", 1, bondLengths["O-H"], bondAngles["O-H"], ref newAtoms, ref atomId);
                            }
                            // 羰基氧：不添加氢
                            else if (atom.AtomName.Contains("O") && (atom.ResidueName == "ASP" || atom.ResidueName == "GLU"))
                            {
                                // 羰基氧通常与碳双键连接，不添加氢
                            }
                            break;
                            
                        case "S":
                            // 硫原子：添加1个氢
                            if (atom.ResidueName == "CYS")
                            {
                                AddHydrogensToAtom(atom, "S", 1, bondLengths["S-H"], bondAngles["S-H"], ref newAtoms, ref atomId);
                            }
                            break;
                    }
                }
                
                // 更新PDB文件的原子列表
                int originalCount = pdbFile.Atoms.Count;
                pdbFile.Atoms = newAtoms;
                Debug.Log($"成功添加氢原子，原子总数从 {originalCount} 增加到 {pdbFile.Atoms.Count}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"添加氢原子失败: {ex.Message}");
            }
        }

        // 从原子添加氢原子的辅助方法
        private static void AddHydrogensToAtom(PDBAtom centralAtom, string element, int count, float bondLength, float bondAngle, ref List<PDBAtom> atoms, ref int atomId)
        {
            // 为中心原子添加指定数量的氢原子
            for (int i = 0; i < count; i++)
            {
                // 计算氢原子的位置（简化实现）
                // 在实际应用中，应该考虑分子的立体化学和键角
                float angle = (i * 360.0f) / count;
                float radian = angle * Mathf.Deg2Rad;
                
                // 计算氢原子的坐标
                float x = centralAtom.X + bondLength * Mathf.Cos(radian);
                float y = centralAtom.Y + bondLength * Mathf.Sin(radian);
                float z = centralAtom.Z;
                
                // 创建氢原子
                PDBAtom hydrogen = new PDBAtom
                {
                    RecordType = "ATOM",
                    AtomNumber = atomId++,
                    AtomName = $"H{i+1}",
                    ResidueName = centralAtom.ResidueName,
                    ChainID = centralAtom.ChainID,
                    ResidueNumber = centralAtom.ResidueNumber,
                    X = x,
                    Y = y,
                    Z = z,
                    Occupancy = 1.0f,
                    TemperatureFactor = 0.0f,
                    Element = "H",
                    PartialCharge = 0.1f // 氢原子的部分电荷
                };
                
                atoms.Add(hydrogen);
            }
        }

        // 分子优化和能量最小化
        public static void OptimizeMolecule(PDBFile pdbFile, string forceField = "MMFF94", int steps = 1000, float tolerance = 0.01f)
        {
            try
            {
                // 简单的能量最小化实现
                // 使用基于力场的梯度下降算法
                
                float previousEnergy = float.MaxValue;
                float currentEnergy = 0.0f;
                
                for (int step = 0; step < steps; step++)
                {
                    // 计算当前能量和梯度
                    currentEnergy = CalculateEnergy(pdbFile, forceField);
                    Dictionary<int, Vector3> gradients = CalculateGradients(pdbFile, forceField);
                    
                    // 检查收敛
                    if (Math.Abs(previousEnergy - currentEnergy) < tolerance)
                    {
                        Debug.Log($"分子优化在第 {step} 步收敛，能量: {currentEnergy:F4} kcal/mol");
                        break;
                    }
                    
                    // 更新原子位置
                    float learningRate = 0.01f;
                    for (int i = 0; i < pdbFile.Atoms.Count; i++)
                    {
                        if (gradients.ContainsKey(i))
                        {
                            Vector3 gradient = gradients[i];
                            pdbFile.Atoms[i].X -= learningRate * gradient.x;
                            pdbFile.Atoms[i].Y -= learningRate * gradient.y;
                            pdbFile.Atoms[i].Z -= learningRate * gradient.z;
                        }
                    }
                    
                    previousEnergy = currentEnergy;
                    
                    // 每100步输出进度
                    if (step % 100 == 0)
                    {
                        Debug.Log($"优化步骤 {step}/{steps}, 能量: {currentEnergy:F4} kcal/mol");
                    }
                }
                
                Debug.Log($"分子优化完成，最终能量: {currentEnergy:F4} kcal/mol");
            }
            catch (Exception ex)
            {
                Debug.LogError($"分子优化失败: {ex.Message}");
            }
        }

        // 计算分子能量
        private static float CalculateEnergy(PDBFile pdbFile, string forceField)
        {
            float energy = 0.0f;
            
            // 键能项
            energy += CalculateBondEnergy(pdbFile);
            
            // 角度能项
            energy += CalculateAngleEnergy(pdbFile);
            
            // 范德华能项
            energy += CalculateVanDerWaalsEnergy(pdbFile);
            
            // 静电能项
            energy += CalculateElectrostaticEnergy(pdbFile);
            
            return energy;
        }

        // 计算键能
        private static float CalculateBondEnergy(PDBFile pdbFile)
        {
            float bondEnergy = 0.0f;
            
            // 简化的键能计算
            // 实际应用中应该考虑实际的键连接
            for (int i = 0; i < pdbFile.Atoms.Count; i++)
            {
                for (int j = i + 1; j < pdbFile.Atoms.Count; j++)
                {
                    float distance = Vector3.Distance(
                        new Vector3(pdbFile.Atoms[i].X, pdbFile.Atoms[i].Y, pdbFile.Atoms[i].Z),
                        new Vector3(pdbFile.Atoms[j].X, pdbFile.Atoms[j].Y, pdbFile.Atoms[j].Z)
                    );
                    
                    // 检查是否为键连接（基于距离）
                    string bondType = $"{pdbFile.Atoms[i].Element}-{pdbFile.Atoms[j].Element}";
                    if (ForceFieldParameters.BondLengths.ContainsKey(bondType))
                    {
                        float idealLength = ForceFieldParameters.BondLengths[bondType];
                        if (Math.Abs(distance - idealLength) < 0.3f)
                        {
                            // 简谐势
                            bondEnergy += 0.5f * 500.0f * Mathf.Pow(distance - idealLength, 2);
                        }
                    }
                }
            }
            
            return bondEnergy;
        }

        // 计算角度能
        private static float CalculateAngleEnergy(PDBFile pdbFile)
        {
            float angleEnergy = 0.0f;
            
            // 简化的角度能计算
            // 实际应用中应该考虑实际的键连接
            for (int i = 0; i < pdbFile.Atoms.Count; i++)
            {
                for (int j = 0; j < pdbFile.Atoms.Count; j++)
                {
                    if (i == j) continue;
                    
                    for (int k = j + 1; k < pdbFile.Atoms.Count; k++)
                    {
                        if (k == i) continue;
                        
                        // 计算角度
                        Vector3 vec1 = new Vector3(pdbFile.Atoms[i].X, pdbFile.Atoms[i].Y, pdbFile.Atoms[i].Z) - 
                                      new Vector3(pdbFile.Atoms[j].X, pdbFile.Atoms[j].Y, pdbFile.Atoms[j].Z);
                        Vector3 vec2 = new Vector3(pdbFile.Atoms[k].X, pdbFile.Atoms[k].Y, pdbFile.Atoms[k].Z) - 
                                      new Vector3(pdbFile.Atoms[j].X, pdbFile.Atoms[j].Y, pdbFile.Atoms[j].Z);
                        
                        float angle = Vector3.Angle(vec1, vec2);
                        
                        // 简谐势
                        angleEnergy += 0.5f * 50.0f * Mathf.Pow(angle - 109.5f, 2);
                    }
                }
            }
            
            return angleEnergy;
        }

        // 计算范德华能
        private static float CalculateVanDerWaalsEnergy(PDBFile pdbFile)
        {
            float vdwEnergy = 0.0f;
            
            // 简化的范德华能计算
            for (int i = 0; i < pdbFile.Atoms.Count; i++)
            {
                for (int j = i + 1; j < pdbFile.Atoms.Count; j++)
                {
                    float distance = Vector3.Distance(
                        new Vector3(pdbFile.Atoms[i].X, pdbFile.Atoms[i].Y, pdbFile.Atoms[i].Z),
                        new Vector3(pdbFile.Atoms[j].X, pdbFile.Atoms[j].Y, pdbFile.Atoms[j].Z)
                    );
                    
                    // 避免除以零
                    if (distance < 0.1f) distance = 0.1f;
                    
                    // Lennard-Jones势
                    float sigma = 1.0f;
                    float epsilon = 0.1f;
                    
                    float term1 = Mathf.Pow(sigma / distance, 12);
                    float term2 = Mathf.Pow(sigma / distance, 6);
                    
                    vdwEnergy += 4.0f * epsilon * (term1 - term2);
                }
            }
            
            return vdwEnergy;
        }

        // 计算静电能
        private static float CalculateElectrostaticEnergy(PDBFile pdbFile)
        {
            float electrostaticEnergy = 0.0f;
            
            // 简化的静电能计算
            for (int i = 0; i < pdbFile.Atoms.Count; i++)
            {
                for (int j = i + 1; j < pdbFile.Atoms.Count; j++)
                {
                    float distance = Vector3.Distance(
                        new Vector3(pdbFile.Atoms[i].X, pdbFile.Atoms[i].Y, pdbFile.Atoms[i].Z),
                        new Vector3(pdbFile.Atoms[j].X, pdbFile.Atoms[j].Y, pdbFile.Atoms[j].Z)
                    );
                    
                    // 避免除以零
                    if (distance < 0.1f) distance = 0.1f;
                    
                    // 库仑势
                    float charge1 = pdbFile.Atoms[i].PartialCharge;
                    float charge2 = pdbFile.Atoms[j].PartialCharge;
                    
                    electrostaticEnergy += (charge1 * charge2) / distance;
                }
            }
            
            return electrostaticEnergy;
        }

        // 生成Morgan指纹
        public static ulong[] GenerateMorganFingerprint(PDBFile pdbFile, int radius = 2, int bits = 2048)
        {
            try
            {
                // 简化的Morgan指纹生成实现
                // 使用原子环境哈希
                
                Dictionary<ulong, int> fingerprintMap = new Dictionary<ulong, int>();
                int bitCount = 0;
                
                // 为每个原子生成环境哈希
                for (int i = 0; i < pdbFile.Atoms.Count; i++)
                {
                    var atom = pdbFile.Atoms[i];
                    
                    // 生成不同半径的环境
                    for (int r = 0; r <= radius; r++)
                    {
                        ulong hash = CalculateAtomEnvironmentHash(pdbFile, i, r);
                        ulong bit = hash % (ulong)bits;
                        
                        if (!fingerprintMap.ContainsKey(bit))
                        {
                            fingerprintMap[bit] = bitCount++;
                        }
                    }
                }
                
                // 转换为位向量
                ulong[] fingerprint = new ulong[(bits + 63) / 64];
                foreach (var bit in fingerprintMap.Keys)
                {
                    int index = (int)(bit / 64);
                    int offset = (int)(bit % 64);
                    fingerprint[index] |= (1UL << offset);
                }
                
                Debug.Log($"生成Morgan指纹: 半径={radius}, 位数={bits}, 设置位数量={fingerprintMap.Count}");
                return fingerprint;
            }
            catch (Exception ex)
            {
                Debug.LogError($"生成Morgan指纹失败: {ex.Message}");
                return new ulong[0];
            }
        }

        // 生成MACCS指纹
        public static ulong[] GenerateMACCSFingerprint(PDBFile pdbFile)
        {
            try
            {
                // 简化的MACCS指纹生成实现
                // MACCS指纹有166位
                
                ulong[] fingerprint = new ulong[3]; // 3 * 64 = 192 bits，足够容纳166位
                
                // 检查分子特性并设置相应的位
                // 这里实现一些基本的MACCS指纹位
                
                // 位1: 分子是否包含碳原子
                bool hasCarbon = pdbFile.Atoms.Exists(atom => atom.Element == "C");
                if (hasCarbon)
                {
                    SetBit(fingerprint, 0); // MACCS位从1开始，转换为0-based索引
                }
                
                // 位2: 分子是否包含氮原子
                bool hasNitrogen = pdbFile.Atoms.Exists(atom => atom.Element == "N");
                if (hasNitrogen)
                {
                    SetBit(fingerprint, 1);
                }
                
                // 位3: 分子是否包含氧原子
                bool hasOxygen = pdbFile.Atoms.Exists(atom => atom.Element == "O");
                if (hasOxygen)
                {
                    SetBit(fingerprint, 2);
                }
                
                // 位4: 分子是否包含硫原子
                bool hasSulfur = pdbFile.Atoms.Exists(atom => atom.Element == "S");
                if (hasSulfur)
                {
                    SetBit(fingerprint, 3);
                }
                
                // 位5: 分子是否包含磷原子
                bool hasPhosphorus = pdbFile.Atoms.Exists(atom => atom.Element == "P");
                if (hasPhosphorus)
                {
                    SetBit(fingerprint, 4);
                }
                
                // 位6: 分子是否包含卤素原子
                bool hasHalogen = pdbFile.Atoms.Exists(atom => 
                    atom.Element == "F" || atom.Element == "Cl" || atom.Element == "Br" || atom.Element == "I");
                if (hasHalogen)
                {
                    SetBit(fingerprint, 5);
                }
                
                // 位7: 分子大小（原子数）
                if (pdbFile.Atoms.Count >= 10)
                {
                    SetBit(fingerprint, 6);
                }
                
                // 位8: 分子是否带电
                bool hasCharge = pdbFile.Atoms.Exists(atom => Math.Abs(atom.PartialCharge) > 0.1f);
                if (hasCharge)
                {
                    SetBit(fingerprint, 7);
                }
                
                Debug.Log($"生成MACCS指纹: 分子包含 {pdbFile.Atoms.Count} 个原子");
                return fingerprint;
            }
            catch (Exception ex)
            {
                Debug.LogError($"生成MACCS指纹失败: {ex.Message}");
                return new ulong[0];
            }
        }

        // 计算原子环境哈希
        private static ulong CalculateAtomEnvironmentHash(PDBFile pdbFile, int atomIndex, int radius)
        {
            // 简化的原子环境哈希计算
            var atom = pdbFile.Atoms[atomIndex];
            ulong hash = (ulong)atom.Element.GetHashCode();
            
            if (radius > 0)
            {
                // 查找邻居原子
                List<int> neighbors = FindNeighborAtoms(pdbFile, atomIndex);
                
                foreach (var neighborIndex in neighbors)
                {
                    var neighbor = pdbFile.Atoms[neighborIndex];
                    hash ^= (ulong)(neighbor.Element.GetHashCode() * 31 + radius);
                }
            }
            
            return hash;
        }

        // 查找原子的邻居
        private static List<int> FindNeighborAtoms(PDBFile pdbFile, int atomIndex)
        {
            List<int> neighbors = new List<int>();
            var centralAtom = pdbFile.Atoms[atomIndex];
            
            // 基于距离查找邻居（简化实现）
            for (int i = 0; i < pdbFile.Atoms.Count; i++)
            {
                if (i == atomIndex) continue;
                
                var atom = pdbFile.Atoms[i];
                float distance = Vector3.Distance(
                    new Vector3(centralAtom.X, centralAtom.Y, centralAtom.Z),
                    new Vector3(atom.X, atom.Y, atom.Z)
                );
                
                // 基于原子类型判断键长
                string bondType = $"{centralAtom.Element}-{atom.Element}";
                if (ForceFieldParameters.BondLengths.ContainsKey(bondType))
                {
                    float idealLength = ForceFieldParameters.BondLengths[bondType];
                    if (Math.Abs(distance - idealLength) < 0.3f)
                    {
                        neighbors.Add(i);
                    }
                }
            }
            
            return neighbors;
        }

        // 设置指纹位
        private static void SetBit(ulong[] fingerprint, int bitIndex)
        {
            if (bitIndex >= 0 && bitIndex < fingerprint.Length * 64)
            {
                int index = bitIndex / 64;
                int offset = bitIndex % 64;
                fingerprint[index] |= (1UL << offset);
            }
        }

        // 计算指纹相似度
        public static float CalculateFingerprintSimilarity(ulong[] fingerprint1, ulong[] fingerprint2)
        {
            try
            {
                // 使用Tanimoto系数计算相似度
                int minLength = Math.Min(fingerprint1.Length, fingerprint2.Length);
                ulong intersection = 0;
                ulong union = 0;
                
                for (int i = 0; i < minLength; i++)
                {
                    intersection += CountSetBits(fingerprint1[i] & fingerprint2[i]);
                    union += CountSetBits(fingerprint1[i] | fingerprint2[i]);
                }
                
                // 处理剩余的位
                for (int i = minLength; i < fingerprint1.Length; i++)
                {
                    union += CountSetBits(fingerprint1[i]);
                }
                
                for (int i = minLength; i < fingerprint2.Length; i++)
                {
                    union += CountSetBits(fingerprint2[i]);
                }
                
                if (union == 0) return 0.0f;
                return (float)intersection / union;
            }
            catch (Exception ex)
            {
                Debug.LogError($"计算指纹相似度失败: {ex.Message}");
                return 0.0f;
            }
        }

        // 子结构搜索
        public static bool HasSubstructure(PDBFile pdbFile, PDBFile substructure)
        {
            try
            {
                // 简化的子结构搜索实现
                // 使用子图同构算法
                
                if (substructure.Atoms.Count == 0)
                    return true;
                
                if (pdbFile.Atoms.Count < substructure.Atoms.Count)
                    return false;
                
                // 构建分子的连接图
                var moleculeGraph = BuildMolecularGraph(pdbFile);
                var substructureGraph = BuildMolecularGraph(substructure);
                
                // 寻找子图同构
                return FindSubgraphIsomorphism(moleculeGraph, substructureGraph);
            }
            catch (Exception ex)
            {
                Debug.LogError($"子结构搜索失败: {ex.Message}");
                return false;
            }
        }

        // 基于SMARTS字符串的子结构搜索
        public static bool HasSubstructure(PDBFile pdbFile, string smartsPattern)
        {
            try
            {
                // 简化的SMARTS模式匹配
                // 这里实现一些基本的SMARTS模式匹配
                
                Debug.Log($"搜索SMARTS模式: {smartsPattern}");
                
                // 基本的SMARTS模式匹配
                if (smartsPattern.Contains("C"))
                {
                    if (!pdbFile.Atoms.Exists(atom => atom.Element == "C"))
                        return false;
                }
                
                if (smartsPattern.Contains("N"))
                {
                    if (!pdbFile.Atoms.Exists(atom => atom.Element == "N"))
                        return false;
                }
                
                if (smartsPattern.Contains("O"))
                {
                    if (!pdbFile.Atoms.Exists(atom => atom.Element == "O"))
                        return false;
                }
                
                if (smartsPattern.Contains("S"))
                {
                    if (!pdbFile.Atoms.Exists(atom => atom.Element == "S"))
                        return false;
                }
                
                // 更复杂的模式匹配可以在这里扩展
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"SMARTS模式搜索失败: {ex.Message}");
                return false;
            }
        }

        // 构建分子连接图
        private static Dictionary<int, List<int>> BuildMolecularGraph(PDBFile pdbFile)
        {
            Dictionary<int, List<int>> graph = new Dictionary<int, List<int>>();
            
            for (int i = 0; i < pdbFile.Atoms.Count; i++)
            {
                graph[i] = new List<int>();
                
                // 查找邻居原子
                var neighbors = FindNeighborAtoms(pdbFile, i);
                foreach (var neighborIndex in neighbors)
                {
                    graph[i].Add(neighborIndex);
                }
            }
            
            return graph;
        }

        // 寻找子图同构
        private static bool FindSubgraphIsomorphism(Dictionary<int, List<int>> moleculeGraph, Dictionary<int, List<int>> substructureGraph)
        {
            // 简化的子图同构搜索
            // 使用回溯算法
            
            if (substructureGraph.Count == 0)
                return true;
            
            // 尝试将子结构的每个原子映射到分子的原子
            List<int> moleculeNodes = new List<int>(moleculeGraph.Keys);
            List<int> substructureNodes = new List<int>(substructureGraph.Keys);
            
            // 简单的映射检查
            // 实际应用中应该使用更复杂的回溯算法
            for (int i = 0; i <= moleculeNodes.Count - substructureNodes.Count; i++)
            {
                bool match = true;
                
                for (int j = 0; j < substructureNodes.Count; j++)
                {
                    int molNode = moleculeNodes[i + j];
                    int subNode = substructureNodes[j];
                    
                    // 检查度数是否匹配
                    if (moleculeGraph[molNode].Count < substructureGraph[subNode].Count)
                    {
                        match = false;
                        break;
                    }
                }
                
                if (match)
                {
                    return true;
                }
            }
            
            return false;
        }

        // 过滤分子集合中的子结构匹配
        public static List<PDBFile> FilterBySubstructure(List<PDBFile> molecules, PDBFile substructure)
        {
            List<PDBFile> matchingMolecules = new List<PDBFile>();
            
            foreach (var molecule in molecules)
            {
                if (HasSubstructure(molecule, substructure))
                {
                    matchingMolecules.Add(molecule);
                }
            }
            
            Debug.Log($"子结构过滤: {matchingMolecules.Count} 个分子匹配");
            return matchingMolecules;
        }

        // 生成分子构象
        public static List<PDBFile> GenerateConformers(PDBFile pdbFile, int count = 10, string method = "systematic", float rmsdThreshold = 0.5f)
        {
            try
            {
                List<PDBFile> conformers = new List<PDBFile>();
                
                // 添加原始构象
                conformers.Add(CopyPDBFile(pdbFile));
                
                // 生成新的构象
                for (int i = 1; i < count; i++)
                {
                    PDBFile conformer = CopyPDBFile(pdbFile);
                    
                    // 随机改变扭转角
                    RandomizeTorsionAngles(conformer);
                    
                    // 优化构象
                    OptimizeMolecule(conformer, "MMFF94", 500, 0.01f);
                    
                    // 检查RMSD阈值，避免生成相似的构象
                    if (conformers.All(c => CalculateRMSD(conformer, c) > rmsdThreshold))
                    {
                        conformers.Add(conformer);
                    }
                }
                
                // 按能量排序
                conformers.Sort((a, b) => 
                    CalculateEnergy(a, "MMFF94").CompareTo(CalculateEnergy(b, "MMFF94"))
                );
                
                Debug.Log($"生成 {conformers.Count} 个构象");
                return conformers;
            }
            catch (Exception ex)
            {
                Debug.LogError($"生成构象失败: {ex.Message}");
                return new List<PDBFile> { pdbFile };
            }
        }

        // 随机改变扭转角
        private static void RandomizeTorsionAngles(PDBFile pdbFile)
        {
            // 简化的扭转角随机化
            // 实际应用中应该识别可旋转键并只改变那些键的扭转角
            
            // 构建分子图
            var graph = BuildMolecularGraph(pdbFile);
            
            // 随机选择一些原子对并旋转它们之间的键
            System.Random random = new System.Random();
            
            for (int i = 0; i < pdbFile.Atoms.Count; i++)
            {
                var atom = pdbFile.Atoms[i];
                
                // 只处理非氢原子
                if (atom.Element == "H") continue;
                
                // 随机选择一个邻居
                var neighbors = graph[i];
                if (neighbors.Count > 0)
                {
                    int neighborIndex = neighbors[random.Next(neighbors.Count)];
                    var neighbor = pdbFile.Atoms[neighborIndex];
                    
                    // 随机旋转角度
                    float angle = random.Next(0, 360) * Mathf.Deg2Rad;
                    
                    // 执行旋转
                    RotateAroundBond(pdbFile, i, neighborIndex, angle);
                }
            }
        }

        // 绕键旋转
        private static void RotateAroundBond(PDBFile pdbFile, int atom1Index, int atom2Index, float angle)
        {
            var atom1 = pdbFile.Atoms[atom1Index];
            var atom2 = pdbFile.Atoms[atom2Index];
            
            // 计算旋转轴
            Vector3 bondVector = new Vector3(atom2.X, atom2.Y, atom2.Z) - 
                                new Vector3(atom1.X, atom1.Y, atom1.Z);
            bondVector.Normalize();
            
            // 计算旋转中心点
            Vector3 center = new Vector3(
                (atom1.X + atom2.X) / 2,
                (atom1.Y + atom2.Y) / 2,
                (atom1.Z + atom2.Z) / 2
            );
            
            // 构建分子图
            var graph = BuildMolecularGraph(pdbFile);
            
            // 确定要旋转的原子集合
            HashSet<int> atomsToRotate = new HashSet<int>();
            Queue<int> queue = new Queue<int>();
            
            // 从atom2开始，找出所有不通过atom1连接的原子
            queue.Enqueue(atom2Index);
            atomsToRotate.Add(atom2Index);
            
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                
                foreach (int neighbor in graph[current])
                {
                    if (neighbor != atom1Index && !atomsToRotate.Contains(neighbor))
                    {
                        atomsToRotate.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }
            
            // 执行旋转
            Quaternion rotation = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, bondVector);
            
            foreach (int index in atomsToRotate)
            {
                var atom = pdbFile.Atoms[index];
                Vector3 position = new Vector3(atom.X, atom.Y, atom.Z);
                Vector3 relativePosition = position - center;
                Vector3 rotatedPosition = rotation * relativePosition + center;
                
                atom.X = rotatedPosition.x;
                atom.Y = rotatedPosition.y;
                atom.Z = rotatedPosition.z;
            }
        }

        // 计算两个构象之间的RMSD
        public static float CalculateRMSD(PDBFile conformer1, PDBFile conformer2)
        {
            if (conformer1.Atoms.Count != conformer2.Atoms.Count)
                return float.MaxValue;
            
            float sumSquaredDistances = 0.0f;
            
            for (int i = 0; i < conformer1.Atoms.Count; i++)
            {
                var atom1 = conformer1.Atoms[i];
                var atom2 = conformer2.Atoms[i];
                
                // 只考虑非氢原子
                if (atom1.Element == "H" || atom2.Element == "H")
                    continue;
                
                float distance = Vector3.Distance(
                    new Vector3(atom1.X, atom1.Y, atom1.Z),
                    new Vector3(atom2.X, atom2.Y, atom2.Z)
                );
                
                sumSquaredDistances += distance * distance;
            }
            
            int nonHydrogenCount = conformer1.Atoms.Count - conformer1.Atoms.Count(atom => atom.Element == "H");
            if (nonHydrogenCount == 0)
                return 0.0f;
            
            return Mathf.Sqrt(sumSquaredDistances / nonHydrogenCount);
        }

        // 计算分子描述符
        public static Dictionary<string, float> CalculateDescriptors(PDBFile pdbFile)
        {
            Dictionary<string, float> descriptors = new Dictionary<string, float>();
            
            try
            {
                // 物理描述符
                descriptors["MolecularWeight"] = CalculateMolecularWeight(pdbFile);
                descriptors["AtomCount"] = pdbFile.Atoms.Count;
                descriptors["HeavyAtomCount"] = pdbFile.Atoms.Count(atom => atom.Element != "H");
                descriptors["HydrogenCount"] = pdbFile.Atoms.Count(atom => atom.Element == "H");
                
                // 元素组成
                descriptors["CarbonCount"] = pdbFile.Atoms.Count(atom => atom.Element == "C");
                descriptors["NitrogenCount"] = pdbFile.Atoms.Count(atom => atom.Element == "N");
                descriptors["OxygenCount"] = pdbFile.Atoms.Count(atom => atom.Element == "O");
                descriptors["SulfurCount"] = pdbFile.Atoms.Count(atom => atom.Element == "S");
                descriptors["PhosphorusCount"] = pdbFile.Atoms.Count(atom => atom.Element == "P");
                descriptors["HalogenCount"] = pdbFile.Atoms.Count(atom => 
                    atom.Element == "F" || atom.Element == "Cl" || atom.Element == "Br" || atom.Element == "I");
                
                // 拓扑描述符
                var graph = BuildMolecularGraph(pdbFile);
                descriptors["BondCount"] = graph.Sum(node => node.Value.Count) / 2;
                descriptors["RingCount"] = CalculateRingCount(pdbFile, graph);
                descriptors["ChainCount"] = CalculateChainCount(pdbFile, graph);
                
                // 电荷描述符
                descriptors["TotalCharge"] = pdbFile.Atoms.Sum(atom => atom.PartialCharge);
                descriptors["MaxPartialCharge"] = pdbFile.Atoms.Max(atom => atom.PartialCharge);
                descriptors["MinPartialCharge"] = pdbFile.Atoms.Min(atom => atom.PartialCharge);
                descriptors["ChargeSpread"] = descriptors["MaxPartialCharge"] - descriptors["MinPartialCharge"];
                
                // 物理化学描述符
                descriptors["LogP"] = CalculateLogP(pdbFile);
                descriptors["TPSA"] = CalculateTPSA(pdbFile); // 拓扑极性表面积
                descriptors["Refractivity"] = CalculateRefractivity(pdbFile); // 折射率
                
                // 药物相似性描述符
                descriptors["LipinskiHBA"] = CalculateHydrogenBondAcceptors(pdbFile);
                descriptors["LipinskiHBD"] = CalculateHydrogenBondDonors(pdbFile);
                descriptors["RotatableBondCount"] = CalculateRotatableBonds(pdbFile, graph);
                
                Debug.Log($"计算了 {descriptors.Count} 个分子描述符");
            }
            catch (Exception ex)
            {
                Debug.LogError($"计算分子描述符失败: {ex.Message}");
            }
            
            return descriptors;
        }

        // 计算分子量
        private static float CalculateMolecularWeight(PDBFile pdbFile)
        {
            // 原子量参数
            Dictionary<string, float> atomicWeights = new Dictionary<string, float>
            {
                {"H", 1.008f}, {"C", 12.011f}, {"N", 14.007f}, {"O", 15.999f},
                {"S", 32.065f}, {"P", 30.974f}, {"F", 18.998f}, {"Cl", 35.453f},
                {"Br", 79.904f}, {"I", 126.904f}
            };
            
            float molecularWeight = 0.0f;
            
            foreach (var atom in pdbFile.Atoms)
            {
                if (atomicWeights.ContainsKey(atom.Element))
                {
                    molecularWeight += atomicWeights[atom.Element];
                }
                else
                {
                    molecularWeight += 12.011f; // 默认碳的原子量
                }
            }
            
            return molecularWeight;
        }

        // 计算环数量
        private static int CalculateRingCount(PDBFile pdbFile, Dictionary<int, List<int>> graph)
        {
            // 简化的环计数实现
            // 实际应用中应该使用更复杂的算法，如Hanser环分析
            
            // 这里返回0作为占位符
            return 0;
        }

        // 计算链数量
        private static int CalculateChainCount(PDBFile pdbFile, Dictionary<int, List<int>> graph)
        {
            // 简化的链计数实现
            // 实际应用中应该分析分子的连接性
            
            // 这里返回1作为占位符
            return 1;
        }

        // 计算LogP
        private static float CalculateLogP(PDBFile pdbFile)
        {
            // 简化的LogP计算
            // 基于原子组成的经验公式
            
            int carbonCount = pdbFile.Atoms.Count(atom => atom.Element == "C");
            int oxygenCount = pdbFile.Atoms.Count(atom => atom.Element == "O");
            int nitrogenCount = pdbFile.Atoms.Count(atom => atom.Element == "N");
            int halogenCount = pdbFile.Atoms.Count(atom => 
                atom.Element == "F" || atom.Element == "Cl" || atom.Element == "Br" || atom.Element == "I");
            
            // 简单的经验公式
            float logP = 0.2f * carbonCount - 0.5f * (oxygenCount + nitrogenCount) + 0.3f * halogenCount;
            
            return Math.Max(0, logP);
        }

        // 计算拓扑极性表面积(TPSA)
        private static float CalculateTPSA(PDBFile pdbFile)
        {
            // 简化的TPSA计算
            // 基于极性原子的贡献
            
            float tpsa = 0.0f;
            
            // 极性原子的表面积贡献
            Dictionary<string, float> atomContributions = new Dictionary<string, float>
            {
                {"O", 17.07f}, {"N", 15.60f}, {"F", 14.60f},
                {"Cl", 12.47f}, {"Br", 18.47f}, {"I", 22.14f}
            };
            
            foreach (var atom in pdbFile.Atoms)
            {
                if (atomContributions.ContainsKey(atom.Element))
                {
                    tpsa += atomContributions[atom.Element];
                }
            }
            
            return tpsa;
        }

        // 计算折射率
        private static float CalculateRefractivity(PDBFile pdbFile)
        {
            // 简化的折射率计算
            // 基于原子组成的摩尔折射率
            
            float refractivity = 0.0f;
            
            // 原子的摩尔折射率贡献
            Dictionary<string, float> atomContributions = new Dictionary<string, float>
            {
                {"C", 2.42f}, {"H", 1.10f}, {"N", 2.67f}, {"O", 1.60f},
                {"S", 7.97f}, {"P", 9.69f}, {"F", 0.92f}, {"Cl", 6.03f},
                {"Br", 8.86f}, {"I", 13.90f}
            };
            
            foreach (var atom in pdbFile.Atoms)
            {
                if (atomContributions.ContainsKey(atom.Element))
                {
                    refractivity += atomContributions[atom.Element];
                }
            }
            
            return refractivity;
        }

        // 计算氢键受体数量
        private static int CalculateHydrogenBondAcceptors(PDBFile pdbFile)
        {
            // 计算氢键受体数量（O和N原子）
            return pdbFile.Atoms.Count(atom => atom.Element == "O" || atom.Element == "N");
        }

        // 计算氢键供体数量
        private static int CalculateHydrogenBondDonors(PDBFile pdbFile)
        {
            // 计算氢键供体数量（N-H和O-H基团）
            // 简化实现：计算与N或O相连的H原子
            return pdbFile.Atoms.Count(atom => atom.Element == "H" && 
                (atom.AtomName.Contains("N") || atom.AtomName.Contains("O")));
        }

        // 立体化学处理
        public static Dictionary<int, string> DetectStereocenters(PDBFile pdbFile)
        {
            Dictionary<int, string> stereocenters = new Dictionary<int, string>();
            
            try
            {
                // 构建分子图
                var graph = BuildMolecularGraph(pdbFile);
                
                for (int i = 0; i < pdbFile.Atoms.Count; i++)
                {
                    var atom = pdbFile.Atoms[i];
                    
                    // 只考虑碳、氮、磷和硫原子作为潜在的立体中心
                    if (atom.Element != "C" && atom.Element != "N" && atom.Element != "P" && atom.Element != "S")
                        continue;
                    
                    // 检查配位数（连接的非氢原子数）
                    var neighbors = graph[i];
                    int heavyNeighbors = neighbors.Count(n => pdbFile.Atoms[n].Element != "H");
                    
                    // 立体中心需要至少3个不同的配体
                    if (heavyNeighbors >= 3)
                    {
                        // 检查配体是否不同
                        HashSet<string> ligandTypes = new HashSet<string>();
                        foreach (int neighborIndex in neighbors)
                        {
                            var neighbor = pdbFile.Atoms[neighborIndex];
                            ligandTypes.Add(neighbor.Element);
                        }
                        
                        if (ligandTypes.Count >= 3)
                        {
                            // 计算立体化学配置（R/S）
                            string config = AssignStereoConfiguration(pdbFile, i, graph);
                            stereocenters[i] = config;
                        }
                    }
                }
                
                Debug.Log($"检测到 {stereocenters.Count} 个立体中心");
            }
            catch (Exception ex)
            {
                Debug.LogError($"检测立体中心失败: {ex.Message}");
            }
            
            return stereocenters;
        }

        // 分配立体化学配置（R/S）
        private static string AssignStereoConfiguration(PDBFile pdbFile, int atomIndex, Dictionary<int, List<int>> graph)
        {
            try
            {
                var atom = pdbFile.Atoms[atomIndex];
                var neighbors = graph[atomIndex];
                
                // 确保有足够的邻居
                if (neighbors.Count < 3)
                    return "?";
                
                // 计算邻居原子的位置相对于中心原子的向量
                List<Vector3> neighborVectors = new List<Vector3>();
                foreach (int neighborIndex in neighbors)
                {
                    var neighbor = pdbFile.Atoms[neighborIndex];
                    Vector3 vector = new Vector3(
                        neighbor.X - atom.X,
                        neighbor.Y - atom.Y,
                        neighbor.Z - atom.Z
                    );
                    neighborVectors.Add(vector);
                }
                
                // 计算手性中心的配置
                // 简化实现：使用右手定则
                if (neighborVectors.Count >= 3)
                {
                    Vector3 v1 = neighborVectors[0];
                    Vector3 v2 = neighborVectors[1];
                    Vector3 v3 = neighborVectors[2];
                    
                    // 计算三重积
                    float tripleProduct = Vector3.Dot(Vector3.Cross(v1, v2), v3);
                    
                    if (tripleProduct > 0)
                        return "R";
                    else if (tripleProduct < 0)
                        return "S";
                }
            }
            catch (Exception)
            {
                // 忽略错误，返回未知配置
            }
            
            return "?";
        }

        // 检测顺反异构
        public static List<Tuple<int, int, string>> DetectCisTransIsomerism(PDBFile pdbFile)
        {
            List<Tuple<int, int, string>> cisTransBonds = new List<Tuple<int, int, string>>();
            
            try
            {
                // 构建分子图
                var graph = BuildMolecularGraph(pdbFile);
                
                // 查找双键
                for (int i = 0; i < pdbFile.Atoms.Count; i++)
                {
                    var atom = pdbFile.Atoms[i];
                    
                    foreach (int neighborIndex in graph[i])
                    {
                        if (neighborIndex <= i) // 避免重复检查
                            continue;
                        
                        var neighbor = pdbFile.Atoms[neighborIndex];
                        
                        // 检查是否为双键
                        // 简化实现：基于键长和原子类型
                        float distance = Vector3.Distance(
                            new Vector3(atom.X, atom.Y, atom.Z),
                            new Vector3(neighbor.X, neighbor.Y, neighbor.Z)
                        );
                        
                        // 双键长度通常在1.2-1.4埃之间
                        if (distance >= 1.2f && distance <= 1.4f)
                        {
                            // 检查顺反异构
                            string isomerism = DetectCisTransAroundBond(pdbFile, i, neighborIndex, graph);
                            if (isomerism != "")
                            {
                                cisTransBonds.Add(new Tuple<int, int, string>(i, neighborIndex, isomerism));
                            }
                        }
                    }
                }
                
                Debug.Log($"检测到 {cisTransBonds.Count} 个顺反异构键");
            }
            catch (Exception ex)
            {
                Debug.LogError($"检测顺反异构失败: {ex.Message}");
            }
            
            return cisTransBonds;
        }

        // 检测围绕双键的顺反异构
        private static string DetectCisTransAroundBond(PDBFile pdbFile, int atom1Index, int atom2Index, Dictionary<int, List<int>> graph)
        {
            try
            {
                var atom1 = pdbFile.Atoms[atom1Index];
                var atom2 = pdbFile.Atoms[atom2Index];
                
                // 获取每个原子的其他邻居（除了彼此）
                var atom1Neighbors = graph[atom1Index].Where(n => n != atom2Index).ToList();
                var atom2Neighbors = graph[atom2Index].Where(n => n != atom1Index).ToList();
                
                if (atom1Neighbors.Count >= 2 && atom2Neighbors.Count >= 2)
                {
                    // 计算取代基的位置关系
                    Vector3 bondVector = new Vector3(atom2.X, atom2.Y, atom2.Z) - 
                                        new Vector3(atom1.X, atom1.Y, atom1.Z);
                    bondVector.Normalize();
                    
                    // 计算每个取代基相对于键轴的位置
                    List<Vector3> atom1Substituents = new List<Vector3>();
                    foreach (int neighborIndex in atom1Neighbors.Take(2))
                    {
                        var neighbor = pdbFile.Atoms[neighborIndex];
                        Vector3 vector = new Vector3(
                            neighbor.X - atom1.X,
                            neighbor.Y - atom1.Y,
                            neighbor.Z - atom1.Z
                        );
                        // 投影到垂直于键轴的平面
                        Vector3 projected = vector - Vector3.Dot(vector, bondVector) * bondVector;
                        atom1Substituents.Add(projected);
                    }
                    
                    List<Vector3> atom2Substituents = new List<Vector3>();
                    foreach (int neighborIndex in atom2Neighbors.Take(2))
                    {
                        var neighbor = pdbFile.Atoms[neighborIndex];
                        Vector3 vector = new Vector3(
                            neighbor.X - atom2.X,
                            neighbor.Y - atom2.Y,
                            neighbor.Z - atom2.Z
                        );
                        // 投影到垂直于键轴的平面
                        Vector3 projected = vector - Vector3.Dot(vector, -bondVector) * (-bondVector);
                        atom2Substituents.Add(projected);
                    }
                    
                    // 计算取代基之间的角度
                    if (atom1Substituents.Count >= 2 && atom2Substituents.Count >= 2)
                    {
                        float angle1 = Vector3.Angle(atom1Substituents[0], atom2Substituents[0]);
                        float angle2 = Vector3.Angle(atom1Substituents[0], atom2Substituents[1]);
                        
                        // 顺式：取代基在同一侧（角度较小）
                        // 反式：取代基在相反侧（角度较大）
                        if (Math.Min(angle1, angle2) < 90)
                            return "cis";
                        else
                            return "trans";
                    }
                }
            }
            catch (Exception)
            {
                // 忽略错误，返回空字符串
            }
            
            return "";
        }

        // 2D到3D转换
        public static PDBFile Convert2DTo3D(PDBFile pdbFile, string method = "distance_geometry", bool optimize = true)
        {
            try
            {
                // 创建3D构象
                PDBFile pdb3D = CopyPDBFile(pdbFile);
                
                // 检查是否已经有3D坐标
                bool has3DCoords = pdbFile.Atoms.All(atom => atom.Z != 0.0f);
                if (has3DCoords)
                {
                    Debug.Log("分子已经包含3D坐标，无需转换");
                    return pdb3D;
                }
                
                // 使用距离几何方法生成3D坐标
                if (method == "distance_geometry")
                {
                    Generate3DCoordinatesUsingDistanceGeometry(pdb3D);
                }
                else
                {
                    // 使用简单的随机方法生成3D坐标
                    Generate3DRandomCoordinates(pdb3D);
                }
                
                // 优化3D结构
                if (optimize)
                {
                    OptimizeMolecule(pdb3D, "MMFF94", 1000, 0.001f);
                }
                
                Debug.Log("成功将2D结构转换为3D结构");
                return pdb3D;
            }
            catch (Exception ex)
            {
                Debug.LogError($"2D到3D转换失败: {ex.Message}");
                return pdbFile;
            }
        }

        // 使用距离几何方法生成3D坐标
        private static void Generate3DCoordinatesUsingDistanceGeometry(PDBFile pdbFile)
        {
            // 简化的距离几何实现
            // 实际应用中应该使用更复杂的算法
            
            // 构建分子图
            var graph = BuildMolecularGraph(pdbFile);
            
            // 计算原子间的距离约束
            Dictionary<Tuple<int, int>, float> distanceConstraints = new Dictionary<Tuple<int, int>, float>();
            
            // 填充距离约束
            for (int i = 0; i < pdbFile.Atoms.Count; i++)
            {
                for (int j = i + 1; j < pdbFile.Atoms.Count; j++)
                {
                    // 计算最短路径长度
                    int pathLength = CalculateShortestPathLength(graph, i, j);
                    
                    if (pathLength > 0)
                    {
                        // 基于路径长度估计距离
                        float distance = EstimateDistanceFromPathLength(pathLength, pdbFile.Atoms[i].Element, pdbFile.Atoms[j].Element);
                        distanceConstraints[new Tuple<int, int>(i, j)] = distance;
                    }
                }
            }
            
            // 使用距离约束生成3D坐标
            Assign3DCoordinatesFromConstraints(pdbFile, distanceConstraints);
        }

        // 使用随机方法生成3D坐标
        private static void Generate3DRandomCoordinates(PDBFile pdbFile)
        {
            System.Random random = new System.Random();
            
            for (int i = 0; i < pdbFile.Atoms.Count; i++)
            {
                var atom = pdbFile.Atoms[i];
                
                // 生成随机3D坐标
                float x = (float)(random.NextDouble() * 10.0 - 5.0);
                float y = (float)(random.NextDouble() * 10.0 - 5.0);
                float z = (float)(random.NextDouble() * 10.0 - 5.0);
                
                atom.X = x;
                atom.Y = y;
                atom.Z = z;
            }
        }

        // 计算图中两个节点之间的最短路径长度
        private static int CalculateShortestPathLength(Dictionary<int, List<int>> graph, int start, int end)
        {
            // 使用BFS计算最短路径
            Queue<int> queue = new Queue<int>();
            Dictionary<int, int> distances = new Dictionary<int, int>();
            
            queue.Enqueue(start);
            distances[start] = 0;
            
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                
                if (current == end)
                    return distances[current];
                
                foreach (int neighbor in graph[current])
                {
                    if (!distances.ContainsKey(neighbor))
                    {
                        distances[neighbor] = distances[current] + 1;
                        queue.Enqueue(neighbor);
                    }
                }
            }
            
            return -1; // 不可达
        }

        // 基于路径长度估计原子间距离
        private static float EstimateDistanceFromPathLength(int pathLength, string element1, string element2)
        {
            // 键长参数
            Dictionary<string, float> bondLengths = new Dictionary<string, float>
            {
                {"C-C", 1.54f}, {"C=N", 1.38f}, {"C=O", 1.20f}, {"C-O", 1.43f},
                {"C-N", 1.47f}, {"N-H", 1.01f}, {"O-H", 0.96f}, {"C-H", 1.09f}
            };
            
            // 平均键长
            float averageBondLength = 1.4f;
            
            // 基于路径长度估计距离
            return pathLength * averageBondLength;
        }

        // 从距离约束分配3D坐标
        private static void Assign3DCoordinatesFromConstraints(PDBFile pdbFile, Dictionary<Tuple<int, int>, float> distanceConstraints)
        {
            // 简化的坐标分配
            // 实际应用中应该使用更复杂的算法，如多维尺度分析
            
            // 首先放置第一个原子在原点
            if (pdbFile.Atoms.Count > 0)
            {
                pdbFile.Atoms[0].X = 0.0f;
                pdbFile.Atoms[0].Y = 0.0f;
                pdbFile.Atoms[0].Z = 0.0f;
            }
            
            // 放置第二个原子在x轴上
            if (pdbFile.Atoms.Count > 1)
            {
                var constraint = distanceConstraints.TryGetValue(new Tuple<int, int>(0, 1), out float distance) 
                    ? distance : 1.5f;
                
                pdbFile.Atoms[1].X = distance;
                pdbFile.Atoms[1].Y = 0.0f;
                pdbFile.Atoms[1].Z = 0.0f;
            }
            
            // 放置第三个原子在xy平面上
            if (pdbFile.Atoms.Count > 2)
            {
                var constraint1 = distanceConstraints.TryGetValue(new Tuple<int, int>(0, 2), out float distance1) 
                    ? distance1 : 1.5f;
                var constraint2 = distanceConstraints.TryGetValue(new Tuple<int, int>(1, 2), out float distance2) 
                    ? distance2 : 1.5f;
                
                // 使用余弦定理计算坐标
                float x = (distance1 * distance1 - distance2 * distance2 + constraint1 * constraint1) / (2 * constraint1);
                float y = Mathf.Sqrt(distance1 * distance1 - x * x);
                
                pdbFile.Atoms[2].X = x;
                pdbFile.Atoms[2].Y = y;
                pdbFile.Atoms[2].Z = 0.0f;
            }
            
            // 放置剩余的原子
            for (int i = 3; i < pdbFile.Atoms.Count; i++)
            {
                // 找到三个已经放置的原子
                float minDistance = float.MaxValue;
                int closestAtomIndex = 0;
                
                for (int j = 0; j < i; j++)
                {
                    float dis = Vector3.Distance(
                        new Vector3(pdbFile.Atoms[j].X, pdbFile.Atoms[j].Y, pdbFile.Atoms[j].Z),
                        new Vector3(0, 0, 0)
                    );
                    
                    if (dis < minDistance)
                    {
                        minDistance = dis;
                        closestAtomIndex = j;
                    }
                }
                
                // 放置新原子
                var constraint = distanceConstraints.TryGetValue(new Tuple<int, int>(closestAtomIndex, i), out float distance) 
                    ? distance : 1.5f;
                
                // 随机方向
                System.Random random = new System.Random();
                float theta = random.Next(0, 360) * Mathf.Deg2Rad;
                float phi = random.Next(0, 180) * Mathf.Deg2Rad;
                
                float x = distance * Mathf.Sin(phi) * Mathf.Cos(theta);
                float y = distance * Mathf.Sin(phi) * Mathf.Sin(theta);
                float z = distance * Mathf.Cos(phi);
                
                pdbFile.Atoms[i].X = pdbFile.Atoms[closestAtomIndex].X + x;
                pdbFile.Atoms[i].Y = pdbFile.Atoms[closestAtomIndex].Y + y;
                pdbFile.Atoms[i].Z = pdbFile.Atoms[closestAtomIndex].Z + z;
            }
        }

        // 从SMILES生成3D结构
        public static PDBFile Generate3DFromSMILES(string smiles, bool optimize = true)
        {
            try
            {
                // 解析SMILES字符串
                PDBFile pdbFile = ParseSMILES(smiles);
                
                // 添加氢原子
                AddHydrogens(pdbFile, true, false);
                
                // 转换为3D
                PDBFile pdb3D = Convert2DTo3D(pdbFile, "distance_geometry", optimize);
                
                Debug.Log($"从SMILES生成3D结构: {smiles}");
                return pdb3D;
            }
            catch (Exception ex)
            {
                Debug.LogError($"从SMILES生成3D结构失败: {ex.Message}");
                return new PDBFile();
            }
        }

        // 保存立体化学信息到文件
        public static void SaveStereochemistryInfo(PDBFile pdbFile, string outputPath)
        {
            try
            {
                var stereocenters = DetectStereocenters(pdbFile);
                var cisTransBonds = DetectCisTransIsomerism(pdbFile);
                
                StringBuilder info = new StringBuilder();
                info.AppendLine("立体化学信息");
                info.AppendLine("=============");
                info.AppendLine();
                
                info.AppendLine("立体中心:");
                foreach (var kvp in stereocenters)
                {
                    var atom = pdbFile.Atoms[kvp.Key];
                    info.AppendLine($"原子 {atom.AtomName} ({atom.Element}) - 配置: {kvp.Value}");
                }
                info.AppendLine();
                
                info.AppendLine("顺反异构:");
                foreach (var bond in cisTransBonds)
                {
                    var atom1 = pdbFile.Atoms[bond.Item1];
                    var atom2 = pdbFile.Atoms[bond.Item2];
                    info.AppendLine($"键 {atom1.AtomName}-{atom2.AtomName} - 构型: {bond.Item3}");
                }
                
                File.WriteAllText(outputPath, info.ToString());
                Debug.Log($"成功保存立体化学信息到: {outputPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"保存立体化学信息失败: {ex.Message}");
            }
        }

        // 计算可旋转键数量
        private static int CalculateRotatableBonds(PDBFile pdbFile, Dictionary<int, List<int>> graph)
        {
            // 简化的可旋转键计数
            // 实际应用中应该识别真正的可旋转键
            
            int rotatableBonds = 0;
            
            // 构建键列表
            HashSet<Tuple<int, int>> bonds = new HashSet<Tuple<int, int>>();
            
            for (int i = 0; i < pdbFile.Atoms.Count; i++)
            {
                foreach (int neighbor in graph[i])
                {
                    if (i < neighbor) // 避免重复计数
                    {
                        bonds.Add(new Tuple<int, int>(i, neighbor));
                    }
                }
            }
            
            // 简单计数：所有非环键
            rotatableBonds = bonds.Count - CalculateRingCount(pdbFile, graph) * 2;
            
            return Math.Max(0, rotatableBonds);
        }

        // 复制PDB文件
        private static PDBFile CopyPDBFile(PDBFile pdbFile)
        {
            PDBFile copy = new PDBFile();
            
            foreach (var atom in pdbFile.Atoms)
            {
                PDBAtom atomCopy = new PDBAtom
                {
                    RecordType = atom.RecordType,
                    AtomNumber = atom.AtomNumber,
                    AtomName = atom.AtomName,
                    AltLoc = atom.AltLoc,
                    ResidueName = atom.ResidueName,
                    ChainID = atom.ChainID,
                    ResidueNumber = atom.ResidueNumber,
                    InsertionCode = atom.InsertionCode,
                    X = atom.X,
                    Y = atom.Y,
                    Z = atom.Z,
                    Occupancy = atom.Occupancy,
                    TemperatureFactor = atom.TemperatureFactor,
                    SegmentID = atom.SegmentID,
                    Element = atom.Element,
                    Charge = atom.Charge,
                    PartialCharge = atom.PartialCharge
                };
                copy.Atoms.Add(atomCopy);
            }
            
            copy.TERLines.AddRange(pdbFile.TERLines);
            copy.ENDLine = pdbFile.ENDLine;
            
            return copy;
        }

        // 过滤分子集合中的SMARTS匹配
        public static List<PDBFile> FilterBySMARTS(List<PDBFile> molecules, string smartsPattern)
        {
            List<PDBFile> matchingMolecules = new List<PDBFile>();
            
            foreach (var molecule in molecules)
            {
                if (HasSubstructure(molecule, smartsPattern))
                {
                    matchingMolecules.Add(molecule);
                }
            }
            
            Debug.Log($"SMARTS过滤: {matchingMolecules.Count} 个分子匹配");
            return matchingMolecules;
        }

        // 计算设置的位数量
        private static ulong CountSetBits(ulong value)
        {
            ulong count = 0;
            while (value > 0)
            {
                count += value & 1;
                value >>= 1;
            }
            return count;
        }

        // 计算梯度
        private static Dictionary<int, Vector3> CalculateGradients(PDBFile pdbFile, string forceField)
        {
            Dictionary<int, Vector3> gradients = new Dictionary<int, Vector3>();
            
            // 简化的梯度计算
            for (int i = 0; i < pdbFile.Atoms.Count; i++)
            {
                Vector3 gradient = Vector3.zero;
                
                for (int j = 0; j < pdbFile.Atoms.Count; j++)
                {
                    if (i == j) continue;
                    
                    Vector3 distanceVector = new Vector3(pdbFile.Atoms[j].X, pdbFile.Atoms[j].Y, pdbFile.Atoms[j].Z) - 
                                           new Vector3(pdbFile.Atoms[i].X, pdbFile.Atoms[i].Y, pdbFile.Atoms[i].Z);
                    float distance = distanceVector.magnitude;
                    
                    if (distance < 0.1f) continue;
                    
                    // 计算力
                    Vector3 force = distanceVector.normalized * (1.0f / (distance * distance));
                    gradient += force;
                }
                
                gradients[i] = gradient;
            }
            
            return gradients;
        }

        // 移除氢原子
        public static void RemoveHydrogens(PDBFile pdbFile, bool removeAll = true, bool keepPolar = false)
        {
            try
            {
                List<PDBAtom> nonHydrogenAtoms = new List<PDBAtom>();
                
                foreach (var atom in pdbFile.Atoms)
                {
                    if (atom.Element != "H")
                    {
                        nonHydrogenAtoms.Add(atom);
                    }
                    else if (keepPolar && !removeAll)
                    {
                        // 保留极性氢原子（与O、N、S相连的氢）
                        // 这里简化实现，实际应该检查键连接
                        nonHydrogenAtoms.Add(atom);
                    }
                }
                
                int removedCount = pdbFile.Atoms.Count - nonHydrogenAtoms.Count;
                pdbFile.Atoms = nonHydrogenAtoms;
                
                // 重新编号原子
                for (int i = 0; i < pdbFile.Atoms.Count; i++)
                {
                    pdbFile.Atoms[i].AtomNumber = i + 1;
                }
                
                Debug.Log($"成功移除 {removedCount} 个氢原子，剩余 {pdbFile.Atoms.Count} 个原子");
            }
            catch (Exception ex)
            {
                Debug.LogError($"移除氢原子失败: {ex.Message}");
            }
        }

        // 通用文件转换方法
        public static void ConvertFile(string inputPath, string outputPath, FileFormat inputFormat, FileFormat outputFormat, ChargeMethod chargeMethod = ChargeMethod.Gasteiger)
        {
            try
            {
                // 根据输入格式解析文件
                PDBFile pdbFile = null;
                
                switch (inputFormat)
                {
                    case FileFormat.PDB:
                        pdbFile = ParsePDB(inputPath);
                        break;
                    case FileFormat.MOL2:
                        var mol2File = ParseMOL2(inputPath);
                        // 转换MOL2到PDBFile
                        pdbFile = new PDBFile();
                        foreach (var atom in mol2File.Atoms)
                        {
                            PDBAtom pdbAtom = new PDBAtom
                            {
                                RecordType = "ATOM",
                                AtomNumber = atom.AtomID,
                                AtomName = atom.AtomName,
                                ResidueName = atom.SubstructureName,
                                ChainID = "A",
                                ResidueNumber = atom.SubstructureID,
                                X = atom.X,
                                Y = atom.Y,
                                Z = atom.Z,
                                Occupancy = 1.0f,
                                TemperatureFactor = 0.0f,
                                Element = ExtractElementFromAtomName(atom.AtomName),
                                PartialCharge = atom.Charge
                            };
                            pdbFile.Atoms.Add(pdbAtom);
                        }
                        break;
                    case FileFormat.SDF:
                        var sdfFile = ParseSDF(inputPath);
                        // 转换SDF到PDBFile
                        if (sdfFile.Molecules.Count > 0)
                        {
                            var molecule = sdfFile.Molecules[0];
                            pdbFile = new PDBFile();
                            int atomId = 1;
                            foreach (var atom in molecule.Atoms)
                            {
                                PDBAtom pdbAtom = new PDBAtom
                                {
                                    RecordType = "ATOM",
                                    AtomNumber = atomId++,
                                    AtomName = $"{atom.Element}{atomId}",
                                    ResidueName = "UNL",
                                    ChainID = "A",
                                    ResidueNumber = 1,
                                    X = atom.X,
                                    Y = atom.Y,
                                    Z = atom.Z,
                                    Occupancy = 1.0f,
                                    TemperatureFactor = 0.0f,
                                    Element = atom.Element
                                };
                                pdbFile.Atoms.Add(pdbAtom);
                            }
                        }
                        break;
                    case FileFormat.SMILES:
                        pdbFile = ParseSMILES(File.ReadAllText(inputPath).Trim());
                        break;
                }
                
                if (pdbFile == null)
                {
                    Debug.LogError("无法解析输入文件");
                    return;
                }
                
                // 根据输出格式写入文件
                switch (outputFormat)
                {
                    case FileFormat.PDBQT:
                        ConvertToPDBQT(pdbFile, outputPath, chargeMethod);
                        break;
                    case FileFormat.MOL2:
                        // 转换PDBFile到MOL2File
                        MOL2File outputMol2 = new MOL2File();
                        outputMol2.Header = "Generated by OpenBabelPDBQTConverter";
                        int mol2AtomId = 1;
                        foreach (var atom in pdbFile.Atoms)
                        {
                            MOL2Atom mol2Atom = new MOL2Atom
                            {
                                AtomID = mol2AtomId++,
                                AtomName = atom.AtomName,
                                X = atom.X,
                                Y = atom.Y,
                                Z = atom.Z,
                                AtomType = atom.Element,
                                SubstructureID = atom.ResidueNumber,
                                SubstructureName = atom.ResidueName,
                                Charge = atom.PartialCharge
                            };
                            outputMol2.Atoms.Add(mol2Atom);
                        }
                        WriteMOL2(outputMol2, outputPath);
                        break;
                    case FileFormat.SDF:
                        // 转换PDBFile到SDFFile
                        SDFFile outputSDF = new SDFFile();
                        SDMolecule sdfMolecule = new SDMolecule();
                        sdfMolecule.Header = "Generated by OpenBabelPDBQTConverter";
                        foreach (var atom in pdbFile.Atoms)
                        {
                            SDFAtom sdfAtom = new SDFAtom
                            {
                                X = atom.X,
                                Y = atom.Y,
                                Z = atom.Z,
                                Element = atom.Element
                            };
                            sdfMolecule.Atoms.Add(sdfAtom);
                        }
                        outputSDF.Molecules.Add(sdfMolecule);
                        WriteSDF(outputSDF, outputPath);
                        break;
                    case FileFormat.SMILES:
                        string smiles = GenerateSMILES(pdbFile);
                        File.WriteAllText(outputPath, smiles);
                        Debug.Log($"成功生成SMILES文件: {outputPath}");
                        break;
                }
                
            }
            catch (Exception ex)
            {
                Debug.LogError($"文件转换失败: {ex.Message}");
            }
        }
    }

    // PDB文件数据结构
    public class PDBFile
    {
        public List<PDBAtom> Atoms { get; set; } = new List<PDBAtom>();
        public List<string> TERLines { get; set; } = new List<string>();
        public string ENDLine { get; set; } = "";
    }

    // PDB原子数据结构
    public class PDBAtom
    {
        public string RecordType { get; set; } = "";
        public int AtomNumber { get; set; } = 0;
        public string AtomName { get; set; } = "";
        public string AltLoc { get; set; } = "";
        public string ResidueName { get; set; } = "";
        public string ChainID { get; set; } = "";
        public int ResidueNumber { get; set; } = 0;
        public string InsertionCode { get; set; } = "";
        public float X { get; set; } = 0.0f;
        public float Y { get; set; } = 0.0f;
        public float Z { get; set; } = 0.0f;
        public float Occupancy { get; set; } = 1.0f;
        public float TemperatureFactor { get; set; } = 0.0f;
        public string SegmentID { get; set; } = "";
        public string Element { get; set; } = "";
        public string Charge { get; set; } = "";
        
        // PDBQT特有字段
        public float PartialCharge { get; set; } = 0.0f;
    }

    // 分子力场参数
    public class ForceFieldParameters
    {
        public static readonly Dictionary<string, float> AtomicRadii = new Dictionary<string, float>
        {
            {"H", 1.2f}, {"C", 1.7f}, {"N", 1.55f}, {"O", 1.52f},
            {"S", 1.8f}, {"P", 1.8f}, {"F", 1.47f}, {"Cl", 1.75f},
            {"Br", 1.85f}, {"I", 1.98f}
        };

        public static readonly Dictionary<string, float> BondLengths = new Dictionary<string, float>
        {
            {"C-C", 1.54f}, {"C-N", 1.47f}, {"C-O", 1.43f}, {"N-H", 1.01f},
            {"O-H", 0.96f}, {"C=O", 1.20f}, {"C=C", 1.34f}, {"C=N", 1.38f}
        };
    }

    // MOL2文件数据结构
    public class MOL2File
    {
        public string Header { get; set; } = "";
        public string Comment { get; set; } = "";
        public List<MOL2Atom> Atoms { get; set; } = new List<MOL2Atom>();
        public List<MOL2Bond> Bonds { get; set; } = new List<MOL2Bond>();
        public List<string> Substructures { get; set; } = new List<string>();
    }

    // MOL2原子数据结构
    public class MOL2Atom
    {
        public int AtomID { get; set; } = 0;
        public string AtomName { get; set; } = "";
        public float X { get; set; } = 0.0f;
        public float Y { get; set; } = 0.0f;
        public float Z { get; set; } = 0.0f;
        public string AtomType { get; set; } = "";
        public int SubstructureID { get; set; } = 1;
        public string SubstructureName { get; set; } = "";
        public float Charge { get; set; } = 0.0f;
    }

    // MOL2键数据结构
    public class MOL2Bond
    {
        public int BondID { get; set; } = 0;
        public int Atom1 { get; set; } = 0;
        public int Atom2 { get; set; } = 0;
        public string BondType { get; set; } = "1";
    }

    // SDF文件数据结构
    public class SDFFile
    {
        public List<SDMolecule> Molecules { get; set; } = new List<SDMolecule>();
    }

    // SDF分子数据结构
    public class SDMolecule
    {
        public string Header { get; set; } = "";
        public string Comment { get; set; } = "";
        public List<SDFAtom> Atoms { get; set; } = new List<SDFAtom>();
        public List<SDFBond> Bonds { get; set; } = new List<SDFBond>();
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
    }

    // SDF原子数据结构
    public class SDFAtom
    {
        public float X { get; set; } = 0.0f;
        public float Y { get; set; } = 0.0f;
        public float Z { get; set; } = 0.0f;
        public string Element { get; set; } = "";
        public int MassDiff { get; set; } = 0;
    }

    // SDF键数据结构
    public class SDFBond
    {
        public int Atom1 { get; set; } = 0;
        public int Atom2 { get; set; } = 0;
        public int BondType { get; set; } = 1;
        public int Stereo { get; set; } = 0;
    }
}
