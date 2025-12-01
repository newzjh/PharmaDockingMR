using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MoleculeLogic
{
    public class MoleculeDirectLoader
    {

        /// <summary>
        /// Called by the contructor to parse the portions of the PDB file related to atoms and
        /// secondary structures.
        /// </summary>
        /// <param name="pdbStream">The PDB stream.</param>
        public static void LoadFromStream(Molecule mol, Stream stream, string ext)
        {
            if (ext == ".pdb" || ext == ".ent" || ext == ".pdbqt" || ext == ".ret")
            {
                LoadFromPDB(mol, stream);
            }
            else if (ext == ".mol2" || ext == ".ml2" || ext == ".sy2")
            {
                LoadFromMol2(mol, stream);
            }
        }

        private static void LoadFromMol2(Molecule mol, Stream stream)
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
                if (sLine.Length <= 0 || sLine.StartsWith("#"))
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

                else if (curblockname == "atom")
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
                    Vector3 pos = new Vector3();
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

                    lcount++;
                    //sscanf(buffer, " %*s %1024s %lf %lf %lf %1024s %d %1024s %lf",atmid, &x, &y, &z, temp_type, &resnum, resname, &pcharge);
                }

                else if (curblockname == "bond")
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
                    int bindex = int.Parse(svalues[0]) - 1;
                    int index1 = int.Parse(svalues[1]) - 1;
                    int index2 = int.Parse(svalues[2]) - 1;
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
                    int subid = int.Parse(svalues[0]) - 1;
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

        private static void LoadFromPDB(Molecule mol, Stream stream)
        {
            int partIndex = 0;

            StreamReader pdbReader = new StreamReader(stream);

            string pdbLine = pdbReader.ReadLine();

            while (pdbLine != null)
            {
                if (pdbLine.StartsWith("ENDMDL")) break;

                else if (pdbLine.StartsWith("HELIX") || pdbLine.StartsWith("SHEET"))
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
                            }
                        }
                    }
                }

                else if (pdbLine.StartsWith("SHAREE"))
                {
                    int sourceAtomId = int.Parse(pdbLine.Substring(7, 4));
                    int targetAtomId = int.Parse(pdbLine.Substring(12, 4));
                    int shareElect = int.Parse(pdbLine.Substring(17, 4));
                    if (sourceAtomId > 0 && sourceAtomId <= mol.atoms.Count && targetAtomId > 0 && targetAtomId <= mol.atoms.Count)
                    {
                        Atom atom1 = mol.atoms[sourceAtomId - 1];
                        Atom atom2 = mol.atoms[targetAtomId - 1];
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
    }
}
