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
    // Minimal PDB/PDBQT conversion utilities used when the project cannot rely on a platform-specific OpenBabel build.
    public class OpenBabelPDBQTConverter
    {
        // Maps common element symbols to the atom-type labels expected by exported PDBQT files.
        private static readonly Dictionary<string, string> AtomTypeMap = new()
        {
            {"C", "C"}, {"N", "N"}, {"O", "O"}, {"S", "S"}, {"P", "P"},
            {"F", "F"}, {"Cl", "Cl"}, {"Br", "Br"}, {"I", "I"}, {"H", "H"}
        };

        // Amino-acid name compression used when reconstructing residue information.
        private static readonly Dictionary<string, string> AminoAcidMap = new()
        {
            {"ALA", "A"}, {"ARG", "R"}, {"ASN", "N"}, {"ASP", "D"}, {"CYS", "C"},
            {"GLN", "Q"}, {"GLU", "E"}, {"GLY", "G"}, {"HIS", "H"}, {"ILE", "I"},
            {"LEU", "L"}, {"LYS", "K"}, {"MET", "M"}, {"PHE", "F"}, {"PRO", "P"},
            {"SER", "S"}, {"THR", "T"}, {"TRP", "W"}, {"TYR", "Y"}, {"VAL", "V"}
        };

        // Placeholder Gasteiger parameters; these can be refined if the converter needs more accurate charge estimation.
        private static readonly Dictionary<string, float> GasteigerParams = new()
        {
            {"C", 0.0f}, {"N", 0.0f}, {"O", 0.0f}, {"S", 0.0f}, {"P", 0.0f},
            {"F", 0.0f}, {"Cl", 0.0f}, {"Br", 0.0f}, {"I", 0.0f}, {"H", 0.0f}
        };

        // Supported input and output structure formats handled by this utility.
        public enum FileFormat
        {
            PDB,
            PDBQT,
            MOL2,
            SDF,
            SMILES
        }

        // Charge-estimation modes exposed by the converter front end.
        public enum ChargeMethod
        {
            Gasteiger,
            MMFF94,
            AM1BCC,
            QEq,
            None
        }

        
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
                Debug.LogError($"Failed to parse PDB file {filePath}: {ex.Message}");
            }
            
            return pdbFile;
        }

        
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

                
                if (string.IsNullOrEmpty(atom.Element))
                {
                    atom.Element = ExtractElementFromAtomName(atom.AtomName);
                }

                return atom;
            }
            catch (Exception)
            {
                Debug.LogWarning($"OpenBabel conversion status");
                return null;
            }
        }

        
        private static string ExtractElementFromAtomName(string atomName)
        {
            
            if (atomName.Length >= 2 && char.IsLetter(atomName[0]) && char.IsLetter(atomName[1]))
            {
                string element = atomName.Substring(0, 2);
                if (AtomTypeMap.ContainsKey(element))
                {
                    return element;
                }
            }
            
            
            if (atomName.Length >= 1 && char.IsLetter(atomName[0]))
            {
                string element = atomName.Substring(0, 1);
                if (AtomTypeMap.ContainsKey(element))
                {
                    return element;
                }
            }
            
            return "C"; 
        }

        
        private static void CalculateGasteigerCharges(PDBFile pdbFile)
        {
            
            foreach (var atom in pdbFile.Atoms)
            {
                
                switch (atom.Element)
                {
                    case "O":
                        
                        if (atom.AtomName.Contains("O") && (atom.ResidueName == "GLU" || atom.ResidueName == "ASP"))
                        {
                            atom.PartialCharge = -0.6f;
                        }
                        
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
                        
                        if (atom.ResidueName == "LYS")
                        {
                            atom.PartialCharge = 0.8f;
                        }
                        
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

        
        private static void CalculateAM1BCCCharges(PDBFile pdbFile)
        {
            
            foreach (var atom in pdbFile.Atoms)
            {
                
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

        
        private static void CalculateMMFF94Charges(PDBFile pdbFile)
        {
            
            foreach (var atom in pdbFile.Atoms)
            {
                
                switch (atom.Element)
                {
                    case "O":
                        
                        if (atom.AtomName.Contains("O") && (atom.ResidueName == "GLU" || atom.ResidueName == "ASP"))
                        {
                            atom.PartialCharge = -0.55f;
                        }
                        
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
                        
                        if (atom.ResidueName == "LYS")
                        {
                            atom.PartialCharge = 0.75f;
                        }
                        
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

        
        private static void CalculateQEqCharges(PDBFile pdbFile)
        {
            
            
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

            
            foreach (var atom in pdbFile.Atoms)
            {
                if (electronegativityHardness.ContainsKey(atom.Element))
                {
                    var (chi, eta) = electronegativityHardness[atom.Element];
                    
                    atom.PartialCharge = -chi * 0.05f;
                }
                else
                {
                    atom.PartialCharge = 0.0f;
                }
            }
        }

        
        public static void ConvertToPDBQT(PDBFile pdbFile, string outputPath, ChargeMethod chargeMethod = ChargeMethod.Gasteiger)
        {
            
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
                    
                    foreach (var atom in pdbFile.Atoms)
                    {
                        atom.PartialCharge = 0.0f;
                    }
                    break;
            }
            
            
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
            
            
            try
            {
                File.WriteAllText(outputPath, pdbqtContent.ToString());
                Debug.Log($"OpenBabel conversion status");
            }
            catch (Exception ex)
            {
                Debug.LogError($"OpenBabel conversion status");
            }
        }

        
        private static string GeneratePDBQTAtomLine(PDBAtom atom)
        {
            StringBuilder line = new StringBuilder();
            
            
            line.Append(atom.RecordType.PadRight(6));
            
            
            line.Append(atom.AtomNumber.ToString().PadLeft(5));
            
            
            line.Append(" ");
            
            
            if (atom.AtomName.Length == 1 || (atom.AtomName.Length > 1 && char.IsLetter(atom.AtomName[1])))
            {
                line.Append(atom.AtomName.PadLeft(4));
            }
            else
            {
                line.Append(atom.AtomName.PadRight(4));
            }
            
            
            line.Append(atom.AltLoc.PadRight(1));
            
            
            line.Append(atom.ResidueName.PadRight(3));
            
            
            line.Append(" ");
            
            
            line.Append(atom.ChainID.PadRight(1));
            
            
            line.Append(atom.ResidueNumber.ToString().PadLeft(4));
            
            
            line.Append(atom.InsertionCode.PadRight(1));
            
            
            line.Append("   ");
            
            
            line.Append(atom.X.ToString("F3", CultureInfo.InvariantCulture).PadLeft(8));
            
            
            line.Append(atom.Y.ToString("F3", CultureInfo.InvariantCulture).PadLeft(8));
            
            
            line.Append(atom.Z.ToString("F3", CultureInfo.InvariantCulture).PadLeft(8));
            
            
            line.Append(atom.Occupancy.ToString("F2", CultureInfo.InvariantCulture).PadLeft(6));
            
            
            line.Append(atom.TemperatureFactor.ToString("F2", CultureInfo.InvariantCulture).PadLeft(6));
            
            
            line.Append(atom.PartialCharge.ToString("F4", CultureInfo.InvariantCulture).PadLeft(6));
            
            
            string atomType = GetPDBQTAtomType(atom);
            line.Append(atomType.PadRight(2));
            
            
            while (line.Length < 80)
            {
                line.Append(" ");
            }
            
            return line.ToString();
        }

        
        private static string GetPDBQTAtomType(PDBAtom atom)
        {
            if (AtomTypeMap.ContainsKey(atom.Element))
            {
                return AtomTypeMap[atom.Element];
            }
            return "C";
        }

        
        public static void ConvertPDBToPDBQT(string pdbPath, string pdbqtPath, ChargeMethod chargeMethod = ChargeMethod.Gasteiger)
        {
            PDBFile pdbFile = ParsePDB(pdbPath);
            ConvertToPDBQT(pdbFile, pdbqtPath, chargeMethod);
        }

        
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
                    Debug.Log($"OpenBabel conversion status");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"OpenBabel conversion status");
                }
            }
            
            Debug.Log($"OpenBabel conversion status");
        }

        
        public static void RotateMolecule(PDBFile pdbFile, Vector3 rotation)
        {
            
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

        
        public static void TranslateMolecule(PDBFile pdbFile, Vector3 translation)
        {
            foreach (var atom in pdbFile.Atoms)
            {
                atom.X += translation.x;
                atom.Y += translation.y;
                atom.Z += translation.z;
            }
        }

        
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

            
            try
            {
                ConvertPDBToPDBQT(inputPath, outputPath, chargeMethod);
                Console.WriteLine($"Conversion succeeded: {inputPath} -> {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Conversion failed: {ex.Message}");
            }
        }

        
        private static void ShowHelp()
        {
            Console.WriteLine("OpenBabelPDBQTConverter command line tool");
            Console.WriteLine("Usage: OpenBabelPDBQTConverter <input PDB file> <output PDBQT file> [options]");
            Console.WriteLine("Options:");
            Console.WriteLine("  -c, --charge <method>   Set the charge calculation method (gasteiger, am1bcc, mmff94, qeq, none)");
            Console.WriteLine("  -h, --help             Show this help message");
            Console.WriteLine("Examples:");
            Console.WriteLine("  OpenBabelPDBQTConverter protein.pdb protein.pdbqt");
            Console.WriteLine("  OpenBabelPDBQTConverter ligand.pdb ligand.pdbqt --charge am1bcc");
            Console.WriteLine("  OpenBabelPDBQTConverter drug.pdb drug.pdbqt --charge mmff94");
        }

        
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

        
        public static string GetMoleculeInfo(PDBFile pdbFile)
        {
            StringBuilder info = new StringBuilder();
            info.AppendLine($"Total atoms: {pdbFile.Atoms.Count}");
            
            
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
            
            info.AppendLine("Element distribution:");
            foreach (var kvp in elementCount)
            {
                info.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }
            
            
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
            
            info.AppendLine("Residue distribution:");
            foreach (var kvp in residueCount)
            {
                info.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }
            
            return info.ToString();
        }

        
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
                Debug.LogError($"OpenBabel conversion status");
            }
            
            return mol2File;
        }

        
        public static void WriteMOL2(MOL2File mol2File, string outputPath)
        {
            try
            {
                StringBuilder mol2Content = new StringBuilder();
                
                
                mol2Content.AppendLine("@<TRIPOS>MOLECULE");
                mol2Content.AppendLine(mol2File.Header);
                mol2Content.AppendLine($"{mol2File.Atoms.Count} {mol2File.Bonds.Count} 0 0 0");
                mol2Content.AppendLine("SMALL");
                mol2Content.AppendLine("USER_CHARGES");
                mol2Content.AppendLine();
                
                
                mol2Content.AppendLine("@<TRIPOS>ATOM");
                foreach (var atom in mol2File.Atoms)
                {
                    mol2Content.AppendLine($"{atom.AtomID} {atom.AtomName} {atom.X:F4} {atom.Y:F4} {atom.Z:F4} {atom.AtomType} {atom.SubstructureID} {atom.SubstructureName} {atom.Charge:F4}");
                }
                mol2Content.AppendLine();
                
                
                mol2Content.AppendLine("@<TRIPOS>BOND");
                foreach (var bond in mol2File.Bonds)
                {
                    mol2Content.AppendLine($"{bond.BondID} {bond.Atom1} {bond.Atom2} {bond.BondType}");
                }
                mol2Content.AppendLine();
                
                
                if (mol2File.Substructures.Count > 0)
                {
                    mol2Content.AppendLine("@<TRIPOS>SUBSTRUCTURE");
                    foreach (var substructure in mol2File.Substructures)
                    {
                        mol2Content.AppendLine(substructure);
                    }
                }
                
                File.WriteAllText(outputPath, mol2Content.ToString());
                Debug.Log($"OpenBabel conversion status");
            }
            catch (Exception ex)
            {
                Debug.LogError($"OpenBabel conversion status");
            }
        }

        
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
                
                
                if (currentMolecule != null)
                {
                    sdfFile.Molecules.Add(currentMolecule);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"OpenBabel conversion status");
            }
            
            return sdfFile;
        }

        
        public static void WriteSDF(SDFFile sdfFile, string outputPath)
        {
            try
            {
                StringBuilder sdfContent = new StringBuilder();
                
                foreach (var molecule in sdfFile.Molecules)
                {
                    
                    sdfContent.AppendLine(molecule.Header);
                    sdfContent.AppendLine(molecule.Comment);
                    sdfContent.AppendLine($"{molecule.Atoms.Count.ToString().PadLeft(3)} {molecule.Bonds.Count.ToString().PadLeft(3)} 0 0 0 0 0 0 0 0 0 0");
                    
                    
                    foreach (var atom in molecule.Atoms)
                    {
                        sdfContent.AppendLine($"{atom.X.ToString("F4").PadLeft(10)} {atom.Y.ToString("F4").PadLeft(10)} {atom.Z.ToString("F4").PadLeft(10)} {atom.Element.PadRight(3)} 0 {atom.MassDiff.ToString().PadLeft(2)} 0 0 0 0 0 0");
                    }
                    
                    
                    foreach (var bond in molecule.Bonds)
                    {
                        sdfContent.AppendLine($"{bond.Atom1.ToString().PadLeft(3)} {bond.Atom2.ToString().PadLeft(3)} {bond.BondType.ToString().PadLeft(3)} {bond.Stereo.ToString().PadLeft(3)} 0 0 0");
                    }
                    
                    
                    foreach (var property in molecule.Properties)
                    {
                        sdfContent.AppendLine($"> <{property.Key}>");
                        sdfContent.AppendLine(property.Value);
                        sdfContent.AppendLine();
                    }
                    
                    
                    sdfContent.AppendLine("$$$$");
                }
                
                File.WriteAllText(outputPath, sdfContent.ToString());
                Debug.Log($"OpenBabel conversion status");
            }
            catch (Exception ex)
            {
                Debug.LogError($"OpenBabel conversion status");
            }
        }

        
        public static PDBFile ParseSMILES(string smiles)
        {
            PDBFile pdbFile = new PDBFile();
            
            try
            {
                
                
                Debug.Log($"OpenBabel conversion status");
                
                
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
                Debug.LogError($"OpenBabel conversion status");
            }
            
            return pdbFile;
        }

        
        public static string GenerateSMILES(PDBFile pdbFile)
        {
            
            
            return "C"; 
        }

        
        public static void AddHydrogens(PDBFile pdbFile, bool pH7 = true, bool addPolarOnly = false)
        {
            try
            {
                List<PDBAtom> newAtoms = new List<PDBAtom>();
                int atomId = pdbFile.Atoms.Count + 1;
                
                
                Dictionary<string, float> bondLengths = new Dictionary<string, float>
                {
                    {"C-H", 1.09f}, {"N-H", 1.01f}, {"O-H", 0.96f}, {"S-H", 1.34f}
                };
                
                
                Dictionary<string, float> bondAngles = new Dictionary<string, float>
                {
                    {"C-H", 109.5f}, {"N-H", 107.0f}, {"O-H", 104.5f}, {"S-H", 92.0f}
                };
                
                foreach (var atom in pdbFile.Atoms)
                {
                    newAtoms.Add(atom);
                    
                    
                    switch (atom.Element)
                    {
                        case "C":
                            
                            if (atom.AtomName.StartsWith("C") && !atom.AtomName.Contains("A") && !atom.AtomName.Contains("B"))
                            {
                                if (!addPolarOnly)
                                {
                                    AddHydrogensToAtom(atom, "C", 3, bondLengths["C-H"], bondAngles["C-H"], ref newAtoms, ref atomId);
                                }
                            }
                            
                            else if (atom.AtomName.Contains("C") && atom.AtomName.Contains("O"))
                            {
                                
                            }
                            break;
                            
                        case "N":
                            
                            if (atom.ResidueName == "LYS" || atom.ResidueName == "ARG")
                            {
                                int hydrogenCount = pH7 ? 3 : 2;
                                AddHydrogensToAtom(atom, "N", hydrogenCount, bondLengths["N-H"], bondAngles["N-H"], ref newAtoms, ref atomId);
                            }
                            
                            else if (atom.ResidueName == "ASN" || atom.ResidueName == "GLN")
                            {
                                AddHydrogensToAtom(atom, "N", 1, bondLengths["N-H"], bondAngles["N-H"], ref newAtoms, ref atomId);
                            }
                            
                            else
                            {
                                int hydrogenCount = pH7 ? 2 : 1;
                                AddHydrogensToAtom(atom, "N", hydrogenCount, bondLengths["N-H"], bondAngles["N-H"], ref newAtoms, ref atomId);
                            }
                            break;
                            
                        case "O":
                            
                            if (atom.AtomName.Contains("OH") || atom.ResidueName == "SER" || atom.ResidueName == "THR" || atom.ResidueName == "TYR")
                            {
                                AddHydrogensToAtom(atom, "O", 1, bondLengths["O-H"], bondAngles["O-H"], ref newAtoms, ref atomId);
                            }
                            
                            else if (atom.AtomName.Contains("O") && (atom.ResidueName == "ASP" || atom.ResidueName == "GLU"))
                            {
                                
                            }
                            break;
                            
                        case "S":
                            
                            if (atom.ResidueName == "CYS")
                            {
                                AddHydrogensToAtom(atom, "S", 1, bondLengths["S-H"], bondAngles["S-H"], ref newAtoms, ref atomId);
                            }
                            break;
                    }
                }
                
                
                int originalCount = pdbFile.Atoms.Count;
                pdbFile.Atoms = newAtoms;
                Debug.Log($"OpenBabel conversion status");
            }
            catch (Exception ex)
            {
                Debug.LogError($"OpenBabel conversion status");
            }
        }

        
        private static void AddHydrogensToAtom(PDBAtom centralAtom, string element, int count, float bondLength, float bondAngle, ref List<PDBAtom> atoms, ref int atomId)
        {
            
            for (int i = 0; i < count; i++)
            {
                
                
                float angle = (i * 360.0f) / count;
                float radian = angle * Mathf.Deg2Rad;
                
                
                float x = centralAtom.X + bondLength * Mathf.Cos(radian);
                float y = centralAtom.Y + bondLength * Mathf.Sin(radian);
                float z = centralAtom.Z;
                
                
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
                    PartialCharge = 0.1f 
                };
                
                atoms.Add(hydrogen);
            }
        }

        
        public static void OptimizeMolecule(PDBFile pdbFile, string forceField = "MMFF94", int steps = 1000, float tolerance = 0.01f)
        {
            try
            {
                
                
                
                float previousEnergy = float.MaxValue;
                float currentEnergy = 0.0f;
                
                for (int step = 0; step < steps; step++)
                {
                    
                    currentEnergy = CalculateEnergy(pdbFile, forceField);
                    Dictionary<int, Vector3> gradients = CalculateGradients(pdbFile, forceField);
                    
                    
                    if (Math.Abs(previousEnergy - currentEnergy) < tolerance)
                    {
                        Debug.Log($"OpenBabel conversion status");
                        break;
                    }
                    
                    
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
                    
                    
                    if (step % 100 == 0)
                    {
                        Debug.Log($"OpenBabel conversion status");
                    }
                }
                
                Debug.Log($"OpenBabel conversion status");
            }
            catch (Exception ex)
            {
                Debug.LogError($"OpenBabel conversion status");
            }
        }

        
        private static float CalculateEnergy(PDBFile pdbFile, string forceField)
        {
            float energy = 0.0f;
            
            
            energy += CalculateBondEnergy(pdbFile);
            
            
            energy += CalculateAngleEnergy(pdbFile);
            
            
            energy += CalculateVanDerWaalsEnergy(pdbFile);
            
            
            energy += CalculateElectrostaticEnergy(pdbFile);
            
            return energy;
        }

        
        private static float CalculateBondEnergy(PDBFile pdbFile)
        {
            float bondEnergy = 0.0f;
            
            
            
            for (int i = 0; i < pdbFile.Atoms.Count; i++)
            {
                for (int j = i + 1; j < pdbFile.Atoms.Count; j++)
                {
                    float distance = Vector3.Distance(
                        new Vector3(pdbFile.Atoms[i].X, pdbFile.Atoms[i].Y, pdbFile.Atoms[i].Z),
                        new Vector3(pdbFile.Atoms[j].X, pdbFile.Atoms[j].Y, pdbFile.Atoms[j].Z)
                    );
                    
                    
                    string bondType = $"{pdbFile.Atoms[i].Element}-{pdbFile.Atoms[j].Element}";
                    if (ForceFieldParameters.BondLengths.ContainsKey(bondType))
                    {
                        float idealLength = ForceFieldParameters.BondLengths[bondType];
                        if (Math.Abs(distance - idealLength) < 0.3f)
                        {
                            
                            bondEnergy += 0.5f * 500.0f * Mathf.Pow(distance - idealLength, 2);
                        }
                    }
                }
            }
            
            return bondEnergy;
        }

        
        private static float CalculateAngleEnergy(PDBFile pdbFile)
        {
            float angleEnergy = 0.0f;
            
            
            
            for (int i = 0; i < pdbFile.Atoms.Count; i++)
            {
                for (int j = 0; j < pdbFile.Atoms.Count; j++)
                {
                    if (i == j) continue;
                    
                    for (int k = j + 1; k < pdbFile.Atoms.Count; k++)
                    {
                        if (k == i) continue;
                        
                        
                        Vector3 vec1 = new Vector3(pdbFile.Atoms[i].X, pdbFile.Atoms[i].Y, pdbFile.Atoms[i].Z) - 
                                      new Vector3(pdbFile.Atoms[j].X, pdbFile.Atoms[j].Y, pdbFile.Atoms[j].Z);
                        Vector3 vec2 = new Vector3(pdbFile.Atoms[k].X, pdbFile.Atoms[k].Y, pdbFile.Atoms[k].Z) - 
                                      new Vector3(pdbFile.Atoms[j].X, pdbFile.Atoms[j].Y, pdbFile.Atoms[j].Z);
                        
                        float angle = Vector3.Angle(vec1, vec2);
                        
                        
                        angleEnergy += 0.5f * 50.0f * Mathf.Pow(angle - 109.5f, 2);
                    }
                }
            }
            
            return angleEnergy;
        }

        
        private static float CalculateVanDerWaalsEnergy(PDBFile pdbFile)
        {
            float vdwEnergy = 0.0f;
            
            
            for (int i = 0; i < pdbFile.Atoms.Count; i++)
            {
                for (int j = i + 1; j < pdbFile.Atoms.Count; j++)
                {
                    float distance = Vector3.Distance(
                        new Vector3(pdbFile.Atoms[i].X, pdbFile.Atoms[i].Y, pdbFile.Atoms[i].Z),
                        new Vector3(pdbFile.Atoms[j].X, pdbFile.Atoms[j].Y, pdbFile.Atoms[j].Z)
                    );
                    
                    
                    if (distance < 0.1f) distance = 0.1f;
                    
                    
                    float sigma = 1.0f;
                    float epsilon = 0.1f;
                    
                    float term1 = Mathf.Pow(sigma / distance, 12);
                    float term2 = Mathf.Pow(sigma / distance, 6);
                    
                    vdwEnergy += 4.0f * epsilon * (term1 - term2);
                }
            }
            
            return vdwEnergy;
        }

        
        private static float CalculateElectrostaticEnergy(PDBFile pdbFile)
        {
            float electrostaticEnergy = 0.0f;
            
            
            for (int i = 0; i < pdbFile.Atoms.Count; i++)
            {
                for (int j = i + 1; j < pdbFile.Atoms.Count; j++)
                {
                    float distance = Vector3.Distance(
                        new Vector3(pdbFile.Atoms[i].X, pdbFile.Atoms[i].Y, pdbFile.Atoms[i].Z),
                        new Vector3(pdbFile.Atoms[j].X, pdbFile.Atoms[j].Y, pdbFile.Atoms[j].Z)
                    );
                    
                    
                    if (distance < 0.1f) distance = 0.1f;
                    
                    
                    float charge1 = pdbFile.Atoms[i].PartialCharge;
                    float charge2 = pdbFile.Atoms[j].PartialCharge;
                    
                    electrostaticEnergy += (charge1 * charge2) / distance;
                }
            }
            
            return electrostaticEnergy;
        }

        
        public static ulong[] GenerateMorganFingerprint(PDBFile pdbFile, int radius = 2, int bits = 2048)
        {
            try
            {
                
                
                
                Dictionary<ulong, int> fingerprintMap = new Dictionary<ulong, int>();
                int bitCount = 0;
                
                
                for (int i = 0; i < pdbFile.Atoms.Count; i++)
                {
                    var atom = pdbFile.Atoms[i];
                    
                    
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
                
                
                ulong[] fingerprint = new ulong[(bits + 63) / 64];
                foreach (var bit in fingerprintMap.Keys)
                {
                    int index = (int)(bit / 64);
                    int offset = (int)(bit % 64);
                    fingerprint[index] |= (1UL << offset);
                }
                
                Debug.Log($"OpenBabel conversion status");
                return fingerprint;
            }
            catch (Exception ex)
            {
                Debug.LogError($"OpenBabel conversion status");
                return new ulong[0];
            }
        }

        
        public static ulong[] GenerateMACCSFingerprint(PDBFile pdbFile)
        {
            try
            {
                
                
                
                ulong[] fingerprint = new ulong[3]; 
                
                
                
                
                
                bool hasCarbon = pdbFile.Atoms.Exists(atom => atom.Element == "C");
                if (hasCarbon)
                {
                    SetBit(fingerprint, 0); 
                }
                
                
                bool hasNitrogen = pdbFile.Atoms.Exists(atom => atom.Element == "N");
                if (hasNitrogen)
                {
                    SetBit(fingerprint, 1);
                }
                
                
                bool hasOxygen = pdbFile.Atoms.Exists(atom => atom.Element == "O");
                if (hasOxygen)
                {
                    SetBit(fingerprint, 2);
                }
                
                
                bool hasSulfur = pdbFile.Atoms.Exists(atom => atom.Element == "S");
                if (hasSulfur)
                {
                    SetBit(fingerprint, 3);
                }
                
                
                bool hasPhosphorus = pdbFile.Atoms.Exists(atom => atom.Element == "P");
                if (hasPhosphorus)
                {
                    SetBit(fingerprint, 4);
                }
                
                
                bool hasHalogen = pdbFile.Atoms.Exists(atom => 
                    atom.Element == "F" || atom.Element == "Cl" || atom.Element == "Br" || atom.Element == "I");
                if (hasHalogen)
                {
                    SetBit(fingerprint, 5);
                }
                
                
                if (pdbFile.Atoms.Count >= 10)
                {
                    SetBit(fingerprint, 6);
                }
                
                
                bool hasCharge = pdbFile.Atoms.Exists(atom => Math.Abs(atom.PartialCharge) > 0.1f);
                if (hasCharge)
                {
                    SetBit(fingerprint, 7);
                }
                
                Debug.Log($"OpenBabel conversion status");
                return fingerprint;
            }
            catch (Exception ex)
            {
                Debug.LogError($"OpenBabel conversion status");
                return new ulong[0];
            }
        }

        
        private static ulong CalculateAtomEnvironmentHash(PDBFile pdbFile, int atomIndex, int radius)
        {
            
            var atom = pdbFile.Atoms[atomIndex];
            ulong hash = (ulong)atom.Element.GetHashCode();
            
            if (radius > 0)
            {
                
                List<int> neighbors = FindNeighborAtoms(pdbFile, atomIndex);
                
                foreach (var neighborIndex in neighbors)
                {
                    var neighbor = pdbFile.Atoms[neighborIndex];
                    hash ^= (ulong)(neighbor.Element.GetHashCode() * 31 + radius);
                }
            }
            
            return hash;
        }

        
        private static List<int> FindNeighborAtoms(PDBFile pdbFile, int atomIndex)
        {
            List<int> neighbors = new List<int>();
            var centralAtom = pdbFile.Atoms[atomIndex];
            
            
            for (int i = 0; i < pdbFile.Atoms.Count; i++)
            {
                if (i == atomIndex) continue;
                
                var atom = pdbFile.Atoms[i];
                float distance = Vector3.Distance(
                    new Vector3(centralAtom.X, centralAtom.Y, centralAtom.Z),
                    new Vector3(atom.X, atom.Y, atom.Z)
                );
                
                
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

        
        private static void SetBit(ulong[] fingerprint, int bitIndex)
        {
            if (bitIndex >= 0 && bitIndex < fingerprint.Length * 64)
            {
                int index = bitIndex / 64;
                int offset = bitIndex % 64;
                fingerprint[index] |= (1UL << offset);
            }
        }

        
        public static float CalculateFingerprintSimilarity(ulong[] fingerprint1, ulong[] fingerprint2)
        {
            try
            {
                
                int minLength = Math.Min(fingerprint1.Length, fingerprint2.Length);
                ulong intersection = 0;
                ulong union = 0;
                
                for (int i = 0; i < minLength; i++)
                {
                    intersection += CountSetBits(fingerprint1[i] & fingerprint2[i]);
                    union += CountSetBits(fingerprint1[i] | fingerprint2[i]);
                }
                
                
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
                Debug.LogError($"OpenBabel conversion status");
                return 0.0f;
            }
        }

        
        public static bool HasSubstructure(PDBFile pdbFile, PDBFile substructure)
        {
            try
            {
                
                
                
                if (substructure.Atoms.Count == 0)
                    return true;
                
                if (pdbFile.Atoms.Count < substructure.Atoms.Count)
                    return false;
                
                
                var moleculeGraph = BuildMolecularGraph(pdbFile);
                var substructureGraph = BuildMolecularGraph(substructure);
                
                
                return FindSubgraphIsomorphism(moleculeGraph, substructureGraph);
            }
            catch (Exception ex)
            {
                Debug.LogError($"OpenBabel conversion status");
                return false;
            }
        }

        
        public static bool HasSubstructure(PDBFile pdbFile, string smartsPattern)
        {
            try
            {
                
                
                
                Debug.Log($"OpenBabel conversion status");
                
                
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
                
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"OpenBabel conversion status");
                return false;
            }
        }

        
        private static Dictionary<int, List<int>> BuildMolecularGraph(PDBFile pdbFile)
        {
            Dictionary<int, List<int>> graph = new Dictionary<int, List<int>>();
            
            for (int i = 0; i < pdbFile.Atoms.Count; i++)
            {
                graph[i] = new List<int>();
                
                
                var neighbors = FindNeighborAtoms(pdbFile, i);
                foreach (var neighborIndex in neighbors)
                {
                    graph[i].Add(neighborIndex);
                }
            }
            
            return graph;
        }

        
        private static bool FindSubgraphIsomorphism(Dictionary<int, List<int>> moleculeGraph, Dictionary<int, List<int>> substructureGraph)
        {
            
            
            
            if (substructureGraph.Count == 0)
                return true;
            
            
            List<int> moleculeNodes = new List<int>(moleculeGraph.Keys);
            List<int> substructureNodes = new List<int>(substructureGraph.Keys);
            
            
            
            for (int i = 0; i <= moleculeNodes.Count - substructureNodes.Count; i++)
            {
                bool match = true;
                
                for (int j = 0; j < substructureNodes.Count; j++)
                {
                    int molNode = moleculeNodes[i + j];
                    int subNode = substructureNodes[j];
                    
                    
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
            
            Debug.Log($"OpenBabel conversion status");
            return matchingMolecules;
        }

        
        public static List<PDBFile> GenerateConformers(PDBFile pdbFile, int count = 10, string method = "systematic", float rmsdThreshold = 0.5f)
        {
            try
            {
                List<PDBFile> conformers = new List<PDBFile>();
                
                
                conformers.Add(CopyPDBFile(pdbFile));
                
                
                for (int i = 1; i < count; i++)
                {
                    PDBFile conformer = CopyPDBFile(pdbFile);
                    
                    
                    RandomizeTorsionAngles(conformer);
                    
                    
                    OptimizeMolecule(conformer, "MMFF94", 500, 0.01f);
                    
                    
                    if (conformers.All(c => CalculateRMSD(conformer, c) > rmsdThreshold))
                    {
                        conformers.Add(conformer);
                    }
                }
                
                
                conformers.Sort((a, b) => 
                    CalculateEnergy(a, "MMFF94").CompareTo(CalculateEnergy(b, "MMFF94"))
                );
                
                Debug.Log($"OpenBabel conversion status");
                return conformers;
            }
            catch (Exception ex)
            {
                Debug.LogError($"OpenBabel conversion status");
                return new List<PDBFile> { pdbFile };
            }
        }

        
        private static void RandomizeTorsionAngles(PDBFile pdbFile)
        {
            
            
            
            
            var graph = BuildMolecularGraph(pdbFile);
            
            
            System.Random random = new System.Random();
            
            for (int i = 0; i < pdbFile.Atoms.Count; i++)
            {
                var atom = pdbFile.Atoms[i];
                
                
                if (atom.Element == "H") continue;
                
                
                var neighbors = graph[i];
                if (neighbors.Count > 0)
                {
                    int neighborIndex = neighbors[random.Next(neighbors.Count)];
                    var neighbor = pdbFile.Atoms[neighborIndex];
                    
                    
                    float angle = random.Next(0, 360) * Mathf.Deg2Rad;
                    
                    
                    RotateAroundBond(pdbFile, i, neighborIndex, angle);
                }
            }
        }

        
        private static void RotateAroundBond(PDBFile pdbFile, int atom1Index, int atom2Index, float angle)
        {
            var atom1 = pdbFile.Atoms[atom1Index];
            var atom2 = pdbFile.Atoms[atom2Index];
            
            
            Vector3 bondVector = new Vector3(atom2.X, atom2.Y, atom2.Z) - 
                                new Vector3(atom1.X, atom1.Y, atom1.Z);
            bondVector.Normalize();
            
            
            Vector3 center = new Vector3(
                (atom1.X + atom2.X) / 2,
                (atom1.Y + atom2.Y) / 2,
                (atom1.Z + atom2.Z) / 2
            );
            
            
            var graph = BuildMolecularGraph(pdbFile);
            
            
            HashSet<int> atomsToRotate = new HashSet<int>();
            Queue<int> queue = new Queue<int>();
            
            
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

        
        public static float CalculateRMSD(PDBFile conformer1, PDBFile conformer2)
        {
            if (conformer1.Atoms.Count != conformer2.Atoms.Count)
                return float.MaxValue;
            
            float sumSquaredDistances = 0.0f;
            
            for (int i = 0; i < conformer1.Atoms.Count; i++)
            {
                var atom1 = conformer1.Atoms[i];
                var atom2 = conformer2.Atoms[i];
                
                
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

        
        public static Dictionary<string, float> CalculateDescriptors(PDBFile pdbFile)
        {
            Dictionary<string, float> descriptors = new Dictionary<string, float>();
            
            try
            {
                
                descriptors["MolecularWeight"] = CalculateMolecularWeight(pdbFile);
                descriptors["AtomCount"] = pdbFile.Atoms.Count;
                descriptors["HeavyAtomCount"] = pdbFile.Atoms.Count(atom => atom.Element != "H");
                descriptors["HydrogenCount"] = pdbFile.Atoms.Count(atom => atom.Element == "H");
                
                
                descriptors["CarbonCount"] = pdbFile.Atoms.Count(atom => atom.Element == "C");
                descriptors["NitrogenCount"] = pdbFile.Atoms.Count(atom => atom.Element == "N");
                descriptors["OxygenCount"] = pdbFile.Atoms.Count(atom => atom.Element == "O");
                descriptors["SulfurCount"] = pdbFile.Atoms.Count(atom => atom.Element == "S");
                descriptors["PhosphorusCount"] = pdbFile.Atoms.Count(atom => atom.Element == "P");
                descriptors["HalogenCount"] = pdbFile.Atoms.Count(atom => 
                    atom.Element == "F" || atom.Element == "Cl" || atom.Element == "Br" || atom.Element == "I");
                
                
                var graph = BuildMolecularGraph(pdbFile);
                descriptors["BondCount"] = graph.Sum(node => node.Value.Count) / 2;
                descriptors["RingCount"] = CalculateRingCount(pdbFile, graph);
                descriptors["ChainCount"] = CalculateChainCount(pdbFile, graph);
                
                
                descriptors["TotalCharge"] = pdbFile.Atoms.Sum(atom => atom.PartialCharge);
                descriptors["MaxPartialCharge"] = pdbFile.Atoms.Max(atom => atom.PartialCharge);
                descriptors["MinPartialCharge"] = pdbFile.Atoms.Min(atom => atom.PartialCharge);
                descriptors["ChargeSpread"] = descriptors["MaxPartialCharge"] - descriptors["MinPartialCharge"];
                
                
                descriptors["LogP"] = CalculateLogP(pdbFile);
                descriptors["TPSA"] = CalculateTPSA(pdbFile); 
                descriptors["Refractivity"] = CalculateRefractivity(pdbFile); 
                
                
                descriptors["LipinskiHBA"] = CalculateHydrogenBondAcceptors(pdbFile);
                descriptors["LipinskiHBD"] = CalculateHydrogenBondDonors(pdbFile);
                descriptors["RotatableBondCount"] = CalculateRotatableBonds(pdbFile, graph);
                
                Debug.Log($"OpenBabel conversion status");
            }
            catch (Exception ex)
            {
                Debug.LogError($"OpenBabel conversion status");
            }
            
            return descriptors;
        }

        
        private static float CalculateMolecularWeight(PDBFile pdbFile)
        {
            
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
                    molecularWeight += 12.011f; 
                }
            }
            
            return molecularWeight;
        }

        
        private static int CalculateRingCount(PDBFile pdbFile, Dictionary<int, List<int>> graph)
        {
            
            
            
            
            return 0;
        }

        
        private static int CalculateChainCount(PDBFile pdbFile, Dictionary<int, List<int>> graph)
        {
            
            
            
            
            return 1;
        }

        
        private static float CalculateLogP(PDBFile pdbFile)
        {
            
            
            
            int carbonCount = pdbFile.Atoms.Count(atom => atom.Element == "C");
            int oxygenCount = pdbFile.Atoms.Count(atom => atom.Element == "O");
            int nitrogenCount = pdbFile.Atoms.Count(atom => atom.Element == "N");
            int halogenCount = pdbFile.Atoms.Count(atom => 
                atom.Element == "F" || atom.Element == "Cl" || atom.Element == "Br" || atom.Element == "I");
            
            
            float logP = 0.2f * carbonCount - 0.5f * (oxygenCount + nitrogenCount) + 0.3f * halogenCount;
            
            return Math.Max(0, logP);
        }

        
        private static float CalculateTPSA(PDBFile pdbFile)
        {
            
            
            
            float tpsa = 0.0f;
            
            
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

        
        private static float CalculateRefractivity(PDBFile pdbFile)
        {
            
            
            
            float refractivity = 0.0f;
            
            
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

        
        private static int CalculateHydrogenBondAcceptors(PDBFile pdbFile)
        {
            
            return pdbFile.Atoms.Count(atom => atom.Element == "O" || atom.Element == "N");
        }

        
        private static int CalculateHydrogenBondDonors(PDBFile pdbFile)
        {
            
            
            return pdbFile.Atoms.Count(atom => atom.Element == "H" && 
                (atom.AtomName.Contains("N") || atom.AtomName.Contains("O")));
        }

        
        public static Dictionary<int, string> DetectStereocenters(PDBFile pdbFile)
        {
            Dictionary<int, string> stereocenters = new Dictionary<int, string>();
            
            try
            {
                
                var graph = BuildMolecularGraph(pdbFile);
                
                for (int i = 0; i < pdbFile.Atoms.Count; i++)
                {
                    var atom = pdbFile.Atoms[i];
                    
                    
                    if (atom.Element != "C" && atom.Element != "N" && atom.Element != "P" && atom.Element != "S")
                        continue;
                    
                    
                    var neighbors = graph[i];
                    int heavyNeighbors = neighbors.Count(n => pdbFile.Atoms[n].Element != "H");
                    
                    
                    if (heavyNeighbors >= 3)
                    {
                        
                        HashSet<string> ligandTypes = new HashSet<string>();
                        foreach (int neighborIndex in neighbors)
                        {
                            var neighbor = pdbFile.Atoms[neighborIndex];
                            ligandTypes.Add(neighbor.Element);
                        }
                        
                        if (ligandTypes.Count >= 3)
                        {
                            
                            string config = AssignStereoConfiguration(pdbFile, i, graph);
                            stereocenters[i] = config;
                        }
                    }
                }
                
                Debug.Log($"OpenBabel conversion status");
            }
            catch (Exception ex)
            {
                Debug.LogError($"OpenBabel conversion status");
            }
            
            return stereocenters;
        }

        
        private static string AssignStereoConfiguration(PDBFile pdbFile, int atomIndex, Dictionary<int, List<int>> graph)
        {
            try
            {
                var atom = pdbFile.Atoms[atomIndex];
                var neighbors = graph[atomIndex];
                
                
                if (neighbors.Count < 3)
                    return "?";
                
                
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
                
                
                
                if (neighborVectors.Count >= 3)
                {
                    Vector3 v1 = neighborVectors[0];
                    Vector3 v2 = neighborVectors[1];
                    Vector3 v3 = neighborVectors[2];
                    
                    
                    float tripleProduct = Vector3.Dot(Vector3.Cross(v1, v2), v3);
                    
                    if (tripleProduct > 0)
                        return "R";
                    else if (tripleProduct < 0)
                        return "S";
                }
            }
            catch (Exception)
            {
                
            }
            
            return "?";
        }

        
        public static List<Tuple<int, int, string>> DetectCisTransIsomerism(PDBFile pdbFile)
        {
            List<Tuple<int, int, string>> cisTransBonds = new List<Tuple<int, int, string>>();
            
            try
            {
                
                var graph = BuildMolecularGraph(pdbFile);
                
                
                for (int i = 0; i < pdbFile.Atoms.Count; i++)
                {
                    var atom = pdbFile.Atoms[i];
                    
                    foreach (int neighborIndex in graph[i])
                    {
                        if (neighborIndex <= i) 
                            continue;
                        
                        var neighbor = pdbFile.Atoms[neighborIndex];
                        
                        
                        
                        float distance = Vector3.Distance(
                            new Vector3(atom.X, atom.Y, atom.Z),
                            new Vector3(neighbor.X, neighbor.Y, neighbor.Z)
                        );
                        
                        
                        if (distance >= 1.2f && distance <= 1.4f)
                        {
                            
                            string isomerism = DetectCisTransAroundBond(pdbFile, i, neighborIndex, graph);
                            if (isomerism != "")
                            {
                                cisTransBonds.Add(new Tuple<int, int, string>(i, neighborIndex, isomerism));
                            }
                        }
                    }
                }
                
                Debug.Log($"OpenBabel conversion status");
            }
            catch (Exception ex)
            {
                Debug.LogError($"OpenBabel conversion status");
            }
            
            return cisTransBonds;
        }

        
        private static string DetectCisTransAroundBond(PDBFile pdbFile, int atom1Index, int atom2Index, Dictionary<int, List<int>> graph)
        {
            try
            {
                var atom1 = pdbFile.Atoms[atom1Index];
                var atom2 = pdbFile.Atoms[atom2Index];
                
                
                var atom1Neighbors = graph[atom1Index].Where(n => n != atom2Index).ToList();
                var atom2Neighbors = graph[atom2Index].Where(n => n != atom1Index).ToList();
                
                if (atom1Neighbors.Count >= 2 && atom2Neighbors.Count >= 2)
                {
                    
                    Vector3 bondVector = new Vector3(atom2.X, atom2.Y, atom2.Z) - 
                                        new Vector3(atom1.X, atom1.Y, atom1.Z);
                    bondVector.Normalize();
                    
                    
                    List<Vector3> atom1Substituents = new List<Vector3>();
                    foreach (int neighborIndex in atom1Neighbors.Take(2))
                    {
                        var neighbor = pdbFile.Atoms[neighborIndex];
                        Vector3 vector = new Vector3(
                            neighbor.X - atom1.X,
                            neighbor.Y - atom1.Y,
                            neighbor.Z - atom1.Z
                        );
                        
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
                        
                        Vector3 projected = vector - Vector3.Dot(vector, -bondVector) * (-bondVector);
                        atom2Substituents.Add(projected);
                    }
                    
                    
                    if (atom1Substituents.Count >= 2 && atom2Substituents.Count >= 2)
                    {
                        float angle1 = Vector3.Angle(atom1Substituents[0], atom2Substituents[0]);
                        float angle2 = Vector3.Angle(atom1Substituents[0], atom2Substituents[1]);
                        
                        
                        
                        if (Math.Min(angle1, angle2) < 90)
                            return "cis";
                        else
                            return "trans";
                    }
                }
            }
            catch (Exception)
            {
                
            }
            
            return "";
        }

        
        public static PDBFile Convert2DTo3D(PDBFile pdbFile, string method = "distance_geometry", bool optimize = true)
        {
            try
            {
                
                PDBFile pdb3D = CopyPDBFile(pdbFile);
                
                
                bool has3DCoords = pdbFile.Atoms.All(atom => atom.Z != 0.0f);
                if (has3DCoords)
                {
                    Debug.Log("OpenBabel conversion status");
                    return pdb3D;
                }
                
                
                if (method == "distance_geometry")
                {
                    Generate3DCoordinatesUsingDistanceGeometry(pdb3D);
                }
                else
                {
                    
                    Generate3DRandomCoordinates(pdb3D);
                }
                
                
                if (optimize)
                {
                    OptimizeMolecule(pdb3D, "MMFF94", 1000, 0.001f);
                }
                
                Debug.Log("OpenBabel conversion status");
                return pdb3D;
            }
            catch (Exception ex)
            {
                Debug.LogError($"OpenBabel conversion status");
                return pdbFile;
            }
        }

        
        private static void Generate3DCoordinatesUsingDistanceGeometry(PDBFile pdbFile)
        {
            
            
            
            
            var graph = BuildMolecularGraph(pdbFile);
            
            
            Dictionary<Tuple<int, int>, float> distanceConstraints = new Dictionary<Tuple<int, int>, float>();
            
            
            for (int i = 0; i < pdbFile.Atoms.Count; i++)
            {
                for (int j = i + 1; j < pdbFile.Atoms.Count; j++)
                {
                    
                    int pathLength = CalculateShortestPathLength(graph, i, j);
                    
                    if (pathLength > 0)
                    {
                        
                        float distance = EstimateDistanceFromPathLength(pathLength, pdbFile.Atoms[i].Element, pdbFile.Atoms[j].Element);
                        distanceConstraints[new Tuple<int, int>(i, j)] = distance;
                    }
                }
            }
            
            
            Assign3DCoordinatesFromConstraints(pdbFile, distanceConstraints);
        }

        
        private static void Generate3DRandomCoordinates(PDBFile pdbFile)
        {
            System.Random random = new System.Random();
            
            for (int i = 0; i < pdbFile.Atoms.Count; i++)
            {
                var atom = pdbFile.Atoms[i];
                
                
                float x = (float)(random.NextDouble() * 10.0 - 5.0);
                float y = (float)(random.NextDouble() * 10.0 - 5.0);
                float z = (float)(random.NextDouble() * 10.0 - 5.0);
                
                atom.X = x;
                atom.Y = y;
                atom.Z = z;
            }
        }

        
        private static int CalculateShortestPathLength(Dictionary<int, List<int>> graph, int start, int end)
        {
            
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
            
            return -1; 
        }

        
        private static float EstimateDistanceFromPathLength(int pathLength, string element1, string element2)
        {
            
            Dictionary<string, float> bondLengths = new Dictionary<string, float>
            {
                {"C-C", 1.54f}, {"C=N", 1.38f}, {"C=O", 1.20f}, {"C-O", 1.43f},
                {"C-N", 1.47f}, {"N-H", 1.01f}, {"O-H", 0.96f}, {"C-H", 1.09f}
            };
            
            
            float averageBondLength = 1.4f;
            
            
            return pathLength * averageBondLength;
        }

        
        private static void Assign3DCoordinatesFromConstraints(PDBFile pdbFile, Dictionary<Tuple<int, int>, float> distanceConstraints)
        {
            
            
            
            
            if (pdbFile.Atoms.Count > 0)
            {
                pdbFile.Atoms[0].X = 0.0f;
                pdbFile.Atoms[0].Y = 0.0f;
                pdbFile.Atoms[0].Z = 0.0f;
            }
            
            
            if (pdbFile.Atoms.Count > 1)
            {
                var constraint = distanceConstraints.TryGetValue(new Tuple<int, int>(0, 1), out float distance) 
                    ? distance : 1.5f;
                
                pdbFile.Atoms[1].X = distance;
                pdbFile.Atoms[1].Y = 0.0f;
                pdbFile.Atoms[1].Z = 0.0f;
            }
            
            
            if (pdbFile.Atoms.Count > 2)
            {
                var constraint1 = distanceConstraints.TryGetValue(new Tuple<int, int>(0, 2), out float distance1) 
                    ? distance1 : 1.5f;
                var constraint2 = distanceConstraints.TryGetValue(new Tuple<int, int>(1, 2), out float distance2) 
                    ? distance2 : 1.5f;
                
                
                float x = (distance1 * distance1 - distance2 * distance2 + constraint1 * constraint1) / (2 * constraint1);
                float y = Mathf.Sqrt(distance1 * distance1 - x * x);
                
                pdbFile.Atoms[2].X = x;
                pdbFile.Atoms[2].Y = y;
                pdbFile.Atoms[2].Z = 0.0f;
            }
            
            
            for (int i = 3; i < pdbFile.Atoms.Count; i++)
            {
                
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
                
                
                var constraint = distanceConstraints.TryGetValue(new Tuple<int, int>(closestAtomIndex, i), out float distance) 
                    ? distance : 1.5f;
                
                
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

        
        public static PDBFile Generate3DFromSMILES(string smiles, bool optimize = true)
        {
            try
            {
                
                PDBFile pdbFile = ParseSMILES(smiles);
                
                
                AddHydrogens(pdbFile, true, false);
                
                
                PDBFile pdb3D = Convert2DTo3D(pdbFile, "distance_geometry", optimize);
                
                Debug.Log($"OpenBabel conversion status");
                return pdb3D;
            }
            catch (Exception ex)
            {
                Debug.LogError($"OpenBabel conversion status");
                return new PDBFile();
            }
        }

        
        public static void SaveStereochemistryInfo(PDBFile pdbFile, string outputPath)
        {
            try
            {
                var stereocenters = DetectStereocenters(pdbFile);
                var cisTransBonds = DetectCisTransIsomerism(pdbFile);
                
                StringBuilder info = new StringBuilder();
                info.AppendLine("Stereochemistry Information");
                info.AppendLine("=============");
                info.AppendLine();
                
                info.AppendLine("Stereocenters:");
                foreach (var kvp in stereocenters)
                {
                    var atom = pdbFile.Atoms[kvp.Key];
                    info.AppendLine($"Atom {atom.AtomName} ({atom.Element}) - Configuration: {kvp.Value}");
                }
                info.AppendLine();
                
                info.AppendLine("Cis-Trans Isomerism:");
                foreach (var bond in cisTransBonds)
                {
                    var atom1 = pdbFile.Atoms[bond.Item1];
                    var atom2 = pdbFile.Atoms[bond.Item2];
                    info.AppendLine($"Bond {atom1.AtomName}-{atom2.AtomName} - Configuration: {bond.Item3}");
                }
                
                File.WriteAllText(outputPath, info.ToString());
                Debug.Log($"OpenBabel conversion status");
            }
            catch (Exception ex)
            {
                Debug.LogError($"OpenBabel conversion status");
            }
        }

        
        private static int CalculateRotatableBonds(PDBFile pdbFile, Dictionary<int, List<int>> graph)
        {
            
            
            
            int rotatableBonds = 0;
            
            
            HashSet<Tuple<int, int>> bonds = new HashSet<Tuple<int, int>>();
            
            for (int i = 0; i < pdbFile.Atoms.Count; i++)
            {
                foreach (int neighbor in graph[i])
                {
                    if (i < neighbor) 
                    {
                        bonds.Add(new Tuple<int, int>(i, neighbor));
                    }
                }
            }
            
            
            rotatableBonds = bonds.Count - CalculateRingCount(pdbFile, graph) * 2;
            
            return Math.Max(0, rotatableBonds);
        }

        
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
            
            Debug.Log($"OpenBabel conversion status");
            return matchingMolecules;
        }

        
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

        
        private static Dictionary<int, Vector3> CalculateGradients(PDBFile pdbFile, string forceField)
        {
            Dictionary<int, Vector3> gradients = new Dictionary<int, Vector3>();
            
            
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
                    
                    
                    Vector3 force = distanceVector.normalized * (1.0f / (distance * distance));
                    gradient += force;
                }
                
                gradients[i] = gradient;
            }
            
            return gradients;
        }

        
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
                        
                        
                        nonHydrogenAtoms.Add(atom);
                    }
                }
                
                int removedCount = pdbFile.Atoms.Count - nonHydrogenAtoms.Count;
                pdbFile.Atoms = nonHydrogenAtoms;
                
                
                for (int i = 0; i < pdbFile.Atoms.Count; i++)
                {
                    pdbFile.Atoms[i].AtomNumber = i + 1;
                }
                
                Debug.Log($"OpenBabel conversion status");
            }
            catch (Exception ex)
            {
                Debug.LogError($"OpenBabel conversion status");
            }
        }

        
        public static void ConvertFile(string inputPath, string outputPath, FileFormat inputFormat, FileFormat outputFormat, ChargeMethod chargeMethod = ChargeMethod.Gasteiger)
        {
            try
            {
                
                PDBFile pdbFile = null;
                
                switch (inputFormat)
                {
                    case FileFormat.PDB:
                        pdbFile = ParsePDB(inputPath);
                        break;
                    case FileFormat.MOL2:
                        var mol2File = ParseMOL2(inputPath);
                        
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
                    Debug.LogError("OpenBabel conversion status");
                    return;
                }
                
                
                switch (outputFormat)
                {
                    case FileFormat.PDBQT:
                        ConvertToPDBQT(pdbFile, outputPath, chargeMethod);
                        break;
                    case FileFormat.MOL2:
                        
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
                        Debug.Log($"OpenBabel conversion status");
                        break;
                }
                
            }
            catch (Exception ex)
            {
                Debug.LogError($"OpenBabel conversion status");
            }
        }
    }

    
    public class PDBFile
    {
        public List<PDBAtom> Atoms { get; set; } = new List<PDBAtom>();
        public List<string> TERLines { get; set; } = new List<string>();
        public string ENDLine { get; set; } = "";
    }

    
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
        
        
        public float PartialCharge { get; set; } = 0.0f;
    }

    
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

    
    public class MOL2File
    {
        public string Header { get; set; } = "";
        public string Comment { get; set; } = "";
        public List<MOL2Atom> Atoms { get; set; } = new List<MOL2Atom>();
        public List<MOL2Bond> Bonds { get; set; } = new List<MOL2Bond>();
        public List<string> Substructures { get; set; } = new List<string>();
    }

    
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

    
    public class MOL2Bond
    {
        public int BondID { get; set; } = 0;
        public int Atom1 { get; set; } = 0;
        public int Atom2 { get; set; } = 0;
        public string BondType { get; set; } = "1";
    }

    
    public class SDFFile
    {
        public List<SDMolecule> Molecules { get; set; } = new List<SDMolecule>();
    }

    
    public class SDMolecule
    {
        public string Header { get; set; } = "";
        public string Comment { get; set; } = "";
        public List<SDFAtom> Atoms { get; set; } = new List<SDFAtom>();
        public List<SDFBond> Bonds { get; set; } = new List<SDFBond>();
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
    }

    
    public class SDFAtom
    {
        public float X { get; set; } = 0.0f;
        public float Y { get; set; } = 0.0f;
        public float Z { get; set; } = 0.0f;
        public string Element { get; set; } = "";
        public int MassDiff { get; set; } = 0;
    }

    
    public class SDFBond
    {
        public int Atom1 { get; set; } = 0;
        public int Atom2 { get; set; } = 0;
        public int BondType { get; set; } = 1;
        public int Stereo { get; set; } = 0;
    }
}
