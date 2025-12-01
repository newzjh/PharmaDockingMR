using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System.Linq;
using SdfManualParser.SdfManualParser;
using Cysharp.Threading.Tasks;

namespace SdfManualParser
{
    /// <summary>
    /// 原子实体类
    /// </summary>
    public class Atom
    {
        /// <summary>
        /// 元素符号（如C、H、O）
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// X坐标
        /// </summary>
        public double X { get; set; }

        /// <summary>
        /// Y坐标
        /// </summary>
        public double Y { get; set; }

        /// <summary>
        /// Z坐标
        /// </summary>
        public double Z { get; set; }

        /// <summary>
        /// 原子序号（1-based）
        /// </summary>
        public int Index { get; set; }
    }

    /// <summary>
    /// 化学键实体类
    /// </summary>
    public class Bond
    {
        /// <summary>
        /// 第一个成键原子序号（1-based）
        /// </summary>
        public int Atom1Index { get; set; }

        /// <summary>
        /// 第二个成键原子序号（1-based）
        /// </summary>
        public int Atom2Index { get; set; }

        /// <summary>
        /// 键类型：1=单键，2=双键，3=三键，4=芳香键
        /// </summary>
        public int BondType { get; set; }

        /// <summary>
        /// 键类型描述
        /// </summary>
        public string BondTypeDesc => BondType switch
        {
            1 => "单键",
            2 => "双键",
            3 => "三键",
            4 => "芳香键",
            _ => "未知键型"
        };
    }

    /// <summary>
    /// 分子实体类
    /// </summary>
    public class Molecule
    {
        /// <summary>
        /// 分子标题（SDF第一行）
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 注释行（SDF第二行）
        /// </summary>
        public string Comment { get; set; } = string.Empty;

        /// <summary>
        /// 原子列表
        /// </summary>
        public List<Atom> Atoms { get; set; } = new List<Atom>();

        /// <summary>
        /// 化学键列表
        /// </summary>
        public List<Bond> Bonds { get; set; } = new List<Bond>();

        /// <summary>
        /// 分子属性字典（> <属性名> 对应的键值对）
        /// </summary>
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    namespace SdfManualParser
    {
        /// <summary>
        /// SDF文件手动解析工具类
        /// </summary>
        public static class SdfParser
        {
            /// <summary>
            /// 读取并解析SDF文件
            /// </summary>
            /// <param name="filePath">SDF文件路径</param>
            /// <returns>解析后的分子列表</returns>
            /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
            /// <exception cref="IOException">文件读取失败时抛出</exception>
            public static List<Molecule> ParseSdf(Stream s)
            {
                StreamReader sr = new StreamReader(s);


                // 读取文件内容，统一换行符为\n，避免跨平台问题
                //string content = File.ReadAllText(filePath, Encoding.UTF8)
                //                     .Replace("\r\n", "\n")
                //                     .Replace("\r", "\n");

                string content = sr.ReadToEnd().
                    Replace("\r\n", "\n")
                    .Replace("\r", "\n");

                // 按SDF分子块分隔符$$$$分割，过滤空块
                string[] molBlocks = content.Split(new[] { "$$$$" }, StringSplitOptions.RemoveEmptyEntries)
                                            .Select(block => block.Trim())
                                            .Where(block => !string.IsNullOrEmpty(block))
                                            .ToArray();

                var molecules = new List<Molecule>();
                foreach (var block in molBlocks)
                {
                    try
                    {
                        var molecule = ParseMoleculeBlock(block);
                        molecules.Add(molecule);
                    }
                    catch (Exception ex)
                    {
                        // 单个分子块解析失败不影响整体，记录异常并继续
                        Console.WriteLine($"解析分子块失败：{ex.Message}");
                    }
                }

                return molecules;
            }

            /// <summary>
            /// 解析单个分子块的内容
            /// </summary>
            /// <param name="block">分子块字符串</param>
            /// <returns>解析后的分子对象</returns>
            private static Molecule ParseMoleculeBlock(string block)
            {
                var molecule = new Molecule();
                // 按行分割，过滤空行和纯空格行
                var lines = block.Split('\n')
                                 .Select(line => line.Trim())
                                 .Where(line => !string.IsNullOrWhiteSpace(line))
                                 .ToList();

                lines.Insert(1, string.Empty);

                if (lines.Count < 3)
                {
                    throw new FormatException("分子块格式无效：行数不足3行");
                }

                // 解析标题行（第一行）和注释行（第二行）
                molecule.Title = lines[0];
                molecule.Comment = lines[1];

                // 解析计数行（第三行）：固定宽度格式，前3位原子数，后3位键数
                string countLine = lines[2];
                if (countLine.Length < 6)
                {
                    throw new FormatException("计数行格式无效：长度不足6位");
                }

                // 提取原子数和键数（处理前导空格）
                if (!int.TryParse(countLine.Substring(0, 3).Trim(), out int atomCount) || atomCount < 0)
                {
                    throw new FormatException($"原子数解析失败：{countLine.Substring(0, 3)}");
                }
                if (!int.TryParse(countLine.Substring(3, 3).Trim(), out int bondCount) || bondCount < 0)
                {
                    throw new FormatException($"键数解析失败：{countLine.Substring(3, 3)}");
                }

                // 解析原子块：从第4行开始，共atomCount行
                ParseAtomBlock(lines, 3, atomCount, molecule);

                // 解析键块：原子块后开始，共bondCount行
                int bondStartIndex = 3 + atomCount;
                ParseBondBlock(lines, bondStartIndex, bondCount, molecule);

                // 解析属性块：键块后开始，直到分子块结束
                int propStartIndex = bondStartIndex + bondCount;
                ParsePropertyBlock(lines, propStartIndex, molecule);

                return molecule;
            }

            /// <summary>
            /// 解析原子块
            /// SDF原子行格式（固定宽度）：
            /// 0-9: X坐标 | 10-19: Y坐标 | 20-29: Z坐标 | 31-33: 元素符号
            /// </summary>
            private static void ParseAtomBlock(List<string> lines, int startIndex, int atomCount, Molecule molecule)
            {
                if (lines.Count < startIndex + atomCount)
                {
                    throw new FormatException($"原子块行数不足：需要{atomCount}行，实际{lines.Count - startIndex}行");
                }

                for (int i = 0; i < atomCount; i++)
                {
                    string atomLine = lines[startIndex + i];
                    if (atomLine.Length < 34)
                    {
                        throw new FormatException($"原子行格式无效：{atomLine}");
                    }

                    string[] tokens = atomLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                    // 解析坐标（处理科学计数法，如1.234E+02）
                    if (!double.TryParse(tokens[0], out double x))
                    {
                        throw new FormatException($"X坐标解析失败：{tokens[0]}");
                    }
                    if (!double.TryParse(tokens[1], out double y))
                    {
                        throw new FormatException($"Y坐标解析失败：{tokens[1]}");
                    }
                    if (!double.TryParse(tokens[2], out double z))
                    {
                        throw new FormatException($"Z坐标解析失败：{tokens[2]}");
                    }

                    // 解析元素符号（去除前后空格）
                    string symbol = tokens[3];
                    if (string.IsNullOrEmpty(symbol))
                    {
                        symbol = "Unknown"; // 未知元素
                    }

                    molecule.Atoms.Add(new Atom
                    {
                        Index = i + 1, // 1-based序号
                        Symbol = symbol,
                        X = x,
                        Y = y,
                        Z = z
                    });
                }
            }

            /// <summary>
            /// 解析键块
            /// SDF键行格式（固定宽度）：
            /// 0-2: 原子1序号 | 3-5: 原子2序号 | 6-8: 键类型
            /// </summary>
            private static void ParseBondBlock(List<string> lines, int startIndex, int bondCount, Molecule molecule)
            {
                if (lines.Count < startIndex + bondCount)
                {
                    throw new FormatException($"键块行数不足：需要{bondCount}行，实际{lines.Count - startIndex}行");
                }

                for (int i = 0; i < bondCount; i++)
                {
                    string bondLine = lines[startIndex + i];
                    if (bondLine.Length < 9)
                    {
                        throw new FormatException($"键行格式无效：{bondLine}");
                    }

                    string[] tokens = bondLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                    // 解析成键原子序号和键类型
                    if (!int.TryParse(tokens[0], out int atom1))
                    {
                        throw new FormatException($"原子1序号解析失败：{tokens[0]}");
                    }
                    if (!int.TryParse(tokens[1], out int atom2))
                    {
                        throw new FormatException($"原子2序号解析失败：{tokens[1]}");
                    }
                    if (!int.TryParse(tokens[2], out int bondType))
                    {
                        throw new FormatException($"键类型解析失败：{tokens[2]}");
                    }

                    molecule.Bonds.Add(new Bond
                    {
                        Atom1Index = atom1,
                        Atom2Index = atom2,
                        BondType = bondType
                    });
                }
            }

            /// <summary>
            /// 解析属性块（处理单行/多行属性值）
            /// SDF属性格式：> <属性名> 换行 属性值（可能多行）
            /// </summary>
            private static void ParsePropertyBlock(List<string> lines, int startIndex, Molecule molecule)
            {
                if (lines.Count <= startIndex)
                {
                    return; // 无属性块
                }

                string currentPropName = string.Empty;
                var currentPropValue = new StringBuilder();

                for (int i = startIndex; i < lines.Count; i++)
                {
                    string line = lines[i];
                    // 检测属性名行：> <属性名>
                    if (line.StartsWith("> <") && line.EndsWith(">"))
                    {
                        // 保存上一个属性（如果存在）
                        if (!string.IsNullOrEmpty(currentPropName))
                        {
                            molecule.Properties[currentPropName] = currentPropValue.ToString().Trim();
                        }

                        // 提取新属性名
                        currentPropName = line.TrimStart("> <".ToCharArray()).TrimEnd('>').Trim();
                        currentPropValue.Clear();
                    }
                    else if (!string.IsNullOrEmpty(currentPropName))
                    {
                        // 追加属性值（处理多行值）
                        currentPropValue.AppendLine(line);
                    }
                }

                // 保存最后一个属性
                if (!string.IsNullOrEmpty(currentPropName))
                {
                    molecule.Properties[currentPropName] = currentPropValue.ToString().Trim();
                }
            }
        }
    }
}


namespace MoleculeLogic
{
    public class MoleculeProgressiveLoader
    {

        /// <summary>
        /// Called by the contructor to parse the portions of the PDB file related to atoms and
        /// secondary structures.
        /// </summary>
        /// <param name="pdbStream">The PDB stream.</param>
        public static async UniTask LoadFromStream(Molecule mol, Stream stream, string ext)
        {
            if (ext == ".pdb" || ext == ".ent" || ext==".pdbqt" || ext==".ret")
            {
                await LoadFromPDB(mol, stream);
            }
            else if (ext == ".mol2" || ext == ".ml2" || ext == ".sy2")
            {
                await LoadFromMol2(mol, stream);
            }
            else if (ext == ".sdf")
            {
                await LoadFromSDF(mol, stream);
            }
        }

        private static async UniTask LoadFromMol2(Molecule mol, Stream stream)
        {
            StreamReader pdbReader = new StreamReader(stream);

            int natoms, nbonds;
            Dictionary<string, object> properties = new Dictionary<string, object>();

            int lcount = 0;
            bool hasPartialCharges = true;
            string curblockname = "";
            Dictionary<string, string> residuetochain = new Dictionary<string, string>();

            while (true)
            {
                string sLine = pdbReader.ReadLine();
                if (sLine == null)
                {
                    break;
                }

                sLine = sLine.TrimStart();
                if (sLine.Length<=0 || sLine.StartsWith("#"))
                {
                    continue;
                }

                if (sLine.StartsWith("@<TRIPOS>MOLECULE"))
                {
                    curblockname = "molecule";
                    lcount = 0;
                    continue;
                }
                else if (sLine.StartsWith("@<TRIPOS>ATOM"))
                {
                    curblockname = "atom";
                    lcount = 0;
                    continue;
                }
                else if (sLine.StartsWith("@<TRIPOS>BOND"))
                {
                    curblockname = "bond";
                    lcount = 0;
                    continue;
                }
                else if (sLine.StartsWith("@<TRIPOS>SUBSTRUCTURE"))
                {
                    curblockname = "substructure";
                    lcount = 0;
                    continue;
                }
                else if (sLine.StartsWith("@<"))
                {
                    curblockname = "other";
                    lcount = 0;
                    continue;
                }

                if (curblockname == "molecule")
                {
                    if (lcount == 0)
                    {
                        mol.name = sLine;
                    }
                    else if (lcount == 1)
                    {
                        string[] svalues = sLine.Split(' ');
                        int.TryParse(svalues[0], out natoms);
                        int.TryParse(svalues[1], out nbonds);
                    }
                    else if (lcount == 2)
                    {

                    }
                    else if (lcount == 3) // charge descriptions
                    {
                        properties["PartialCharges"] = sLine;
                        if (sLine.StartsWith("NO_CHARGES"))
                            hasPartialCharges = false;
                    }
                    else if (lcount == 4) //energy (?)
                    {
                        properties["Energy"] = sLine;
                    }
                    else if (lcount == 5) //comment
                    {
                        properties["comment"] = sLine;
                    }
                    lcount++;
                }

                else if (curblockname=="atom")
                {
                    List<string> tvalues = new List<string>(sLine.Split(' '));
                    List<string> svalues = new List<string>();
                    for (int i = 0; i < tvalues.Count; i++)
                    {
                        if (tvalues[i].Length > 0) svalues.Add(tvalues[i]);
                    }
                    if (svalues.Count < 8)
                    {
                        continue;
                    }
                    int aindex = -1;
                    int.TryParse(svalues[0], out aindex); 
                    aindex--;
                    string atomName = svalues[1];
                    Vector3 pos=new Vector3();
                    float.TryParse(svalues[2], out pos.x);
                    float.TryParse(svalues[3], out pos.y);
                    float.TryParse(svalues[4], out pos.z);
                    string stype = svalues[5];
                    int residueSequenceNumber = -1;
                    int.TryParse(svalues[6], out residueSequenceNumber);
                    residueSequenceNumber--;
                    string residueName = svalues[7];

                    float temperatureFactor = 0.0f;
                    string chainIdentifier = "";
                    bool hetType = false;

                    Atom a = Atom.CreateAtom(mol, atomName, hetType, residueName,
                        residueSequenceNumber, chainIdentifier, pos, temperatureFactor,
                        mol.atoms.Count, mol.atomgroup.transform);
                    mol.atoms.Add(a);

                    if (mol.atoms.Count > 0 && mol.atoms.Count % 100 == 0) 
                        await UniTask.NextFrame();

                    lcount++;
                    //sscanf(buffer, " %*s %1024s %lf %lf %lf %1024s %d %1024s %lf",atmid, &x, &y, &z, temp_type, &resnum, resname, &pcharge);
                }

                else if (curblockname=="bond")
                {
                    List<string> tvalues = new List<string>(sLine.Split(' '));
                    List<string> svalues = new List<string>();
                    for (int i = 0; i < tvalues.Count; i++)
                    {
                        if (tvalues[i].Length > 0) svalues.Add(tvalues[i]);
                    }
                    if (svalues.Count < 4)
                    {
                        continue;
                    }
                    int bindex = int.Parse(svalues[0])-1;
                    int index1 = int.Parse(svalues[1])-1;
                    int index2 = int.Parse(svalues[2])-1;
                    string stype = svalues[3];
                    int order = 1;
                    if (stype == "ar" || stype == "AR" || stype == "Ar")
                        order = 5;
                    else if (stype == "AM" || stype == "am" || stype == "Am")
                        order = 1;
                    else
                        int.TryParse(stype, out order);

                    if (index1 >= 0 && index1 < mol.atoms.Count && index2 >= 0 && index2 < mol.atoms.Count)
                    {
                        Atom atom1 = mol.atoms[index1];
                        Atom atom2 = mol.atoms[index2];
                        float distance = (atom1.position - atom2.position).magnitude;
                        if (distance * distance < 3.6f && atom1.partIndex == atom2.partIndex &&
                            !(atom1.bonds.ContainsKey(atom2) || atom2.bonds.ContainsKey(atom1)))
                        {
                            Bond b = Bond.CreateBond(mol, atom1, atom2, order, mol.bonds.Count, mol.bondgroup.transform);
                            mol.bonds.Add(b);

                            if (mol.bonds.Count > 0 && mol.bonds.Count % 100 == 0) 
                                await UniTask.NextFrame();
                        }
                    }
                }
                else if (curblockname == "substructure")
                {
                    List<string> tvalues = new List<string>(sLine.Split(' '));
                    List<string> svalues = new List<string>();
                    for (int i = 0; i < tvalues.Count; i++)
                    {
                        if (tvalues[i].Length > 0) svalues.Add(tvalues[i]);
                    }
                    if (svalues.Count < 6)
                    {
                        continue;
                    }
                    int subid = int.Parse(svalues[0])-1;
                    string subname = svalues[1];
                    int rootatomid = int.Parse(svalues[2]) - 1;
                    string subtype = svalues[3];
                    string dicttype = svalues[4];
                    string chainid = svalues[5];

                    if (subtype.ToLower() == "residue")
                    {
                        residuetochain[subname] = chainid;
                    }
                }
                else if (curblockname == "other")
                {
                    //Debug.Log("unknownblock:"+curblockname);
                }
            }

            for (int i = 0; i < mol.atoms.Count; i++)
            {
                Atom atom = mol.atoms[i];
                if (residuetochain.ContainsKey(atom.residueName))
                {
                    string chainid = residuetochain[atom.residueName];
                    atom.chainIdentifier = chainid;
                }
            }

            mol.InitDefaultScheme();
        }

        private static async UniTask LoadFromPDB(Molecule mol, Stream stream)
        {
            int partIndex = 0;

            StreamReader pdbReader = new StreamReader(stream);
            
            string pdbLine = pdbReader.ReadLine();

            while (pdbLine != null)
            {

                if (pdbLine.StartsWith("HELIX") || pdbLine.StartsWith("SHEET"))
                {
                    Structure s = Structure.CreateStructure(pdbLine);
                    mol.structures.Add(s);
                }

                else if (pdbLine.StartsWith("ATOM") || pdbLine.StartsWith("HETATM"))
                {
                    string atomName = pdbLine.Substring(12, 4).Trim();
                    char sb = atomName[0];
                    if (sb >= '0' && sb <= '9')
                    {
                        atomName = atomName.Substring(1, atomName.Length - 1) + "_" + sb;
                    }

                    string residueName = pdbLine.Substring(17, 3).Trim();

                    bool hetType = pdbLine.StartsWith("HETATM");
                    string residueSequenceNumberStr = pdbLine.Substring(22, 4);
                    int residueSequenceNumber = 0;// Convert.ToInt32(residueSequenceNumberStr);
                    int.TryParse(residueSequenceNumberStr, out residueSequenceNumber);

                    string chainIdentifier = pdbLine.Substring(21, 1);
                    if (residueName == "HOH") chainIdentifier = "";
                    else if (chainIdentifier == " ") chainIdentifier = "1";

                    Vector3 pos;
                    pos.x = float.Parse(pdbLine.Substring(30, 8));
                    pos.y = float.Parse(pdbLine.Substring(38, 8));
                    pos.z = float.Parse(pdbLine.Substring(46, 8));

                    float temperatureFactor = 0.0f;
                    if (pdbLine.Length >= 66)
                        temperatureFactor = float.Parse(pdbLine.Substring(60, 6));

                    Atom a = Atom.CreateAtom(mol, atomName, hetType, residueName,
                        residueSequenceNumber, chainIdentifier, pos, temperatureFactor,
                        mol.atoms.Count, mol.atomgroup.transform);
                    a.partIndex = partIndex;
                    mol.atoms.Add(a);

                    if (mol.atoms.Count > 0 && mol.atoms.Count % 100 == 0)
                        await UniTask.NextFrame();
                }

                else if (pdbLine.StartsWith("CONECT"))
                {
                    string[] splitedStringTemp = pdbLine.Split(' '); //0 is Connect, 1 is the atom, 2,3..... is the bounded atoms
                    List<string> splitedString = new List<string>();
                    for (int j = 0; j < splitedStringTemp.Length; j++)
                    {
                        if (splitedStringTemp[j] != "")
                            splitedString.Add(splitedStringTemp[j]);
                    }
                    for (int j = 2; j < splitedString.Count; j++)
                    {
                        int index1 = -1;
                        if (int.TryParse(splitedString[1], out index1))
                        {
                            index1 -= 1;
                        }
                        else
                        {
                            break;
                        }
                        int index2 = -1;
                        if (int.TryParse(splitedString[j], out index2))
                        {
                            index2 -= 1;
                        }
                        else
                        {
                            break;
                        }
                        if (index1 >= 0 && index1 < mol.atoms.Count && index2 >= 0 && index2 < mol.atoms.Count)
                        {
                            Atom atom1 = mol.atoms[index1];
                            Atom atom2 = mol.atoms[index2];
                            float distance = (atom1.position - atom2.position).magnitude;
                            if (distance * distance < 3.6f && atom1.partIndex == atom2.partIndex &&
                                !(atom1.bonds.ContainsKey(atom2) || atom2.bonds.ContainsKey(atom1)))
                            {
                                Bond b = Bond.CreateBond(mol, atom1, atom2, -1, mol.bonds.Count, mol.bondgroup.transform);
                                mol.bonds.Add(b);

                                if (mol.bonds.Count > 0 && mol.bonds.Count % 100 == 0)
                                    await UniTask.NextFrame();
                            }
                        }
                    }
                }

                else if (pdbLine.StartsWith("SHAREE"))
                {
                    int sourceAtomId = int.Parse(pdbLine.Substring(7, 4));
                    int targetAtomId = int.Parse(pdbLine.Substring(12, 4));
                    int shareElect=int.Parse(pdbLine.Substring(17, 4));
                    if (sourceAtomId>0 && sourceAtomId<=mol.atoms.Count && targetAtomId>0 && targetAtomId<=mol.atoms.Count)
                    {
                        Atom atom1 = mol.atoms[sourceAtomId-1];
                        Atom atom2 = mol.atoms[targetAtomId-1];
                        if (atom1.shareE_targetAtom == null || atom1.shareE_num == null)
                        {
                            atom1.shareE_targetAtom = new List<Atom>();
                            atom1.shareE_num = new List<int>();
                        }
                        atom1.shareE_targetAtom.Add(atom2);
                        atom1.shareE_num.Add(shareElect);
                    }
                }

                else if (pdbLine.StartsWith("MODEL"))
                {
                    mol.type = MolType.Conformation;
                    partIndex++;
                }
                else if (pdbLine.StartsWith("BRANCH"))
                {
                    if (mol.type == MolType.Receptor)
                        mol.type = MolType.Ligand;
                }

                pdbLine = pdbReader.ReadLine();
            }

            mol.partCunnt = partIndex + 1;
            mol.InitDefaultScheme();

        }

        private static async UniTask LoadFromSDF(Molecule mol, Stream stream)
        {
            int partIndex = 0;

            var molecules = SdfParser.ParseSdf(stream);

            Console.WriteLine($"✅ 成功解析SDF文件，共读取 {molecules.Count} 个分子\n");

            // 遍历输出每个分子的信息
            for (int i = 0; i < molecules.Count; i++)
            {
                var srcmol = molecules[i];
                Console.WriteLine($"========== 分子 {i + 1} ==========");
                Console.WriteLine($"标题：{srcmol.Title}");
                Console.WriteLine($"注释：{srcmol.Comment}");
                Console.WriteLine($"原子数：{srcmol.Atoms.Count} | 键数：{srcmol.Bonds.Count}");

                // 输出原子信息
                Console.WriteLine("\n【原子信息】");
                foreach (var atom in srcmol.Atoms)
                {
                    Console.WriteLine($"原子{atom.Index}：{atom.Symbol} | 坐标({atom.X:F3}, {atom.Y:F3}, {atom.Z:F3})");
                    var pos = new Vector3((float)atom.X, (float)atom.Y, (float)atom.Z);
                    Atom a = Atom.CreateAtom(mol, atom.Symbol, false, "<0>",
                                   0, "d", pos, 0,
                                   mol.atoms.Count, mol.atomgroup.transform);
                    a.partIndex = partIndex;
                    mol.atoms.Add(a);

                    if (mol.atoms.Count > 0 && mol.atoms.Count % 100 == 0)
                        await UniTask.NextFrame();
                }

                // 输出键信息
                Console.WriteLine("\n【化学键信息】");
                foreach (var bond in srcmol.Bonds)
                {
                    Console.WriteLine($"键：{bond.Atom1Index}-{bond.Atom2Index} | 类型：{bond.BondTypeDesc}");
                    Atom atom1 = mol.atoms[bond.Atom1Index-1];
                    Atom atom2 = mol.atoms[bond.Atom2Index-1];
                    float distance = (atom1.position - atom2.position).magnitude;
                    if (distance * distance < 3.6f && atom1.partIndex == atom2.partIndex &&
                        !(atom1.bonds.ContainsKey(atom2) || atom2.bonds.ContainsKey(atom1)))
                    {
                        Bond b = Bond.CreateBond(mol, atom1, atom2, -1, mol.bonds.Count, mol.bondgroup.transform);
                        mol.bonds.Add(b);

                        if (mol.bonds.Count > 0 && mol.bonds.Count % 100 == 0)
                            await UniTask.NextFrame();
                    }
                }

                // 输出属性信息
                if (srcmol.Properties.Count > 0)
                {
                    Console.WriteLine("\n【分子属性】");
                    foreach (var prop in srcmol.Properties)
                    {
                        Console.WriteLine($"{prop.Key}：{prop.Value}");
                    }
                }

                Console.WriteLine("\n----------------------------------------\n");
            }

            mol.partCunnt = partIndex + 1;
            mol.InitDefaultScheme();
            mol.type = MolType.Ligand;

        }
    }
}
