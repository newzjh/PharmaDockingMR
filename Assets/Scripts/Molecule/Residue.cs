//=============================================================================
// This file is part of The Scripps Research Institute's C-ME Application built
// by InterKnowlogy.  
//
// Copyright (C) 2006, 2007 Scripps Research Institute / InterKnowlogy, LLC.
// All rights reserved.
//
// For information about this application contact Tim Huckaby at
// TimHuck@InterKnowlogy.com or (760) 930-0075 x201.
//
// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND,
// EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.
//=============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoleculeLogic
{
    /// <summary>
    /// Represents a residue in a molecule. 
    /// </summary>
    /// <remarks>
    /// Sometimes referred to as an amino acid. Generates WPF content to display the 
    /// residue in cartoon mode as well as in the identifier strip at the
    /// top of the screen. Aggregates all constituent atoms.
    /// </remarks>
    public class Residue : Representable
    {
        /// <summary>
        /// 
        /// </summary>
        public enum SelectionType { None, Partial, Full };

        private Molecule molecule;
        private string residueName;
        private string chainIdentifier;
        private int residueSequenceNumber;
        private string residueIdentifier;
        private Color residueColor;
        private Color structureColor;
        private Color color;
        public List<Atom> atoms = new List<Atom>();
        private bool isSheet;
        private bool isHelix;
        private bool isStructureStart;
        private bool isStructureEnd;
        private Residue previousResidue;
        private Residue nextResidue;
        private Vector3 cAlphaPosition;
        private Vector3 carbonylOxygenPosition;
        private Chain chain;
        private ColorScheme colorScheme;
        private SelectionType selection;
        private SelectionType showAsSelection;
        private bool updatingSelectionProperty;
        private Cartoon cartoon;
        public int index;

        private GameObject textobj;
        private int showtext = -2;

        private static Dictionary<string, string> shortmap = new Dictionary<string, string>()
{
    { "gly", "G"},
    { "ala", "A"},
    { "val", "V"},
    { "leu", "L"},
    { "ile", "I"},
    { "pro", "P"},
    { "phe", "F"},
    { "tyr", "Y"},
    { "trp", "W"},
    { "ser", "S"},
    { "thr", "T"},
    { "cys", "C"},
    { "met", "M"},
    { "asn", "N"},
    { "gln", "Q"},
    { "asp", "D"},
    { "glu", "E"},
    { "lys", "K"},
    { "arg", "R"},
    { "his", "H"},

};

        /// <summary>
        /// Creates a new <see cref="Residue" /> object.
        /// </summary>
        /// <param name="molecule">The molecule this residue belongs to.</param>
        /// <param name="atom">An atom in the residue. This is needed to obtain residue properties
        /// since there is no corresponding PDB file record.</param>
        public static Residue CreateResidue(Molecule molecule, int residueSequenceNumber, string residueName, string chainIdentifier, int index, Transform parent)
        {
            GameObject residueobj = new GameObject();
            residueobj.name = residueName;
            residueobj.transform.parent = parent;// molecule.transform;
            residueobj.transform.localScale = Vector3.one;
            residueobj.transform.localEulerAngles = Vector3.zero;
            residueobj.transform.localPosition = Vector3.zero;
            Residue residue = residueobj.AddComponent<Residue>();

            residue.molecule = molecule;
            residue.index = index;
            residue.residueName = residueName;
            residue.chainIdentifier = chainIdentifier;
            residue.residueSequenceNumber = residueSequenceNumber;
            residue.residueIdentifier = Residue.GetResidueIdentifier(residue.residueName);
            residue.residueColor = Residue.GetResidueColor(residue.residueName);
            residue.structureColor = residue.residueIdentifier != "O" ? Color.gray : Color.red;
            residue.colorScheme = ColorScheme.Structure;
            residue.UpdateColorView();

            return residue;
        }

        public override void Represent(MoleculeScheme scheme, MolType type, bool dirty, int level)
        {

            if (this.Ribbon != null)
            {
                if (cartoon == null)
                    cartoon = this.gameObject.AddComponent<Cartoon>();
                cartoon.residue = this;
                cartoon.Represent(scheme,dirty);
            }

            if (this.showtext != scheme.showtext)
            {
                DestroyImmediate(textobj);

                if (scheme.showtext != 0 && atoms.Count > 0)
                {
                    textobj = new GameObject("text");
                    textobj.transform.parent = this.transform;
                    textobj.transform.localPosition = atoms[0].position;
                    textobj.transform.localScale = new Vector3(-0.5f, 0.5f, 0.5f);
                    textobj.transform.eulerAngles = Vector3.zero;

                    TextMesh tm = textobj.AddComponent<TextMesh>();
                    tm.fontSize = 24;
                    tm.anchor = TextAnchor.MiddleCenter;
                    tm.fontStyle = FontStyle.Bold;
                    tm.anchor = TextAnchor.UpperCenter;
                    string displayname;
                    string shortname = this.residueName.ToLower();
                    if (shortmap.ContainsKey(shortname))
                        displayname = shortmap[shortname];
                    else
                        displayname = this.residueName;
                    tm.text = displayname + this.residueSequenceNumber;
                    tm.color = this.color;
                    MeshRenderer textmr = textobj.GetComponent<MeshRenderer>();
                    if (Application.isPlaying)
                    {
                        textmr.material.color = this.color;
                    }
                }

                this.showtext = scheme.showtext;
            }
        }



        /// <summary>
        /// The multi-character abbreviation for the residue. For chain-based amino acids, this is
        /// a three letter code.
        /// </summary>
        public string ResidueName { get { return this.residueName; } }

        /// <summary>
        /// Alphanumeric chain identifier for the chain residue belongs to.
        /// </summary>
        public string ChainIdentifier { get { return this.chainIdentifier; } }

        /// <summary>
        /// Index number for this amino acid.
        /// </summary>
        public int ResidueSequenceNumber { get { return this.residueSequenceNumber; } }

        /// <summary>
        /// Shortened abbreviation for the residue. For chain-based amino acids, this is a single
        /// letter.
        /// </summary>
        public string ResidueIdentifier { get { return this.residueIdentifier; } }

        /// <summary>
        /// The color used for this residue when using the residue-based coloring method.
        /// </summary>
        public Color ResidueColor { get { return this.residueColor; } }

        /// <summary>
        /// The constituent atoms.
        /// </summary>
        public List<Atom> Atoms { get { return this.atoms; } }



        /// <summary>
        /// Gets and sets a boolean value indicating if this residue is part of a sheet.
        /// </summary>
        public bool IsSheet
        {
            get { return this.isSheet; }
            set { this.isSheet = value; }
        }

        /// <summary>
        /// Gets and sets a boolean value indicating if this residue is part of a helix.
        /// </summary>
        public bool IsHelix
        {
            get { return this.isHelix; }
            set { this.isHelix = value; }
        }

        /// <summary>
        /// Gets and sets a boolean value indicating if this residue is the first residue in a
        /// secondary structure.
        /// </summary>
        public bool IsStructureStart
        {
            get { return this.isStructureStart; }
            set { this.isStructureStart = value; }
        }

        /// <summary>
        /// Gets and sets a boolean value indicating if this residue is the last residue in a
        /// secondary structure.
        /// </summary>
        public bool IsStructureEnd
        {
            get { return this.isStructureEnd; }
            set { this.isStructureEnd = value; }
        }

        /// <summary>
        /// Reference to the previous residue in the current chain.
        /// </summary>
        public Residue PreviousResidue
        {
            get { return this.previousResidue; }
            set { this.previousResidue = value; }
        }

        /// <summary>
        /// Reference to the next residue in the current chain.
        /// </summary>
        public Residue NextResidue
        {
            get { return this.nextResidue; }
            set { this.nextResidue = value; }
        }

        /// <summary>
        /// If residue belongs to a standard protein amino acid this will contain the 3D location
        /// of the alpha carbon atom.
        /// </summary>
        public Vector3 CAlphaPosition
        {
            get { return this.cAlphaPosition; }
            set { this.cAlphaPosition = value; }
        }

        /// <summary>
        /// If residue belongs to a standard protein amino acid this will contain the 3D location
        /// of the carbonyl oxygen atom.
        /// </summary>
        public Vector3 CarbonylOxygenPosition
        {
            get { return this.carbonylOxygenPosition; }
            set { this.carbonylOxygenPosition = value; }
        }

        /// <summary>
        /// The chain this residue belongs to.
        /// </summary>
        public Chain Chain
        {
            get { return this.chain; }
            set { this.chain = value; }
        }

        /// <summary>
        /// Reference to the <see cref="Ribbon" /> object that calculates spline paths for this
        /// residue.
        /// </summary>
        public Ribbon Ribbon;

        /// <summary>
        /// The color to use for this residue when using the structure-based coloring method.
        /// </summary>
        public Color StructureColor
        {
            get { return this.structureColor; }
            set
            {
                this.structureColor = value;
                this.UpdateColorView();
            }
        }

        /// <summary>
        /// Currently used coloring method.
        /// </summary>
        public ColorScheme ColorScheme
        {
            get
            {
                return this.colorScheme;
            }
            set
            {
                if (this.colorScheme != value)
                {
                    this.colorScheme = value;
                    this.UpdateColorView();
                }
            }
        }


 
        /// <summary>
        /// Determines if a particular residue name refers to an amino acid.
        /// </summary>
        /// <param name="residueName">A multi-character residue abbreviation.</param>
        /// <returns>True if and only if the residue name refers to an amino acid.</returns>
        public static bool IsAminoName(string residueName)
        {
            if (residueName == "ALA") return true;
            else if (residueName == "ARG") return true;
            else if (residueName == "ASP") return true;
            else if (residueName == "CYS") return true;
            else if (residueName == "GLN") return true;
            else if (residueName == "GLU") return true;
            else if (residueName == "GLY") return true;
            else if (residueName == "HIS") return true;
            else if (residueName == "ILE") return true;
            else if (residueName == "LEU") return true;
            else if (residueName == "LYS") return true;
            else if (residueName == "MET") return true;
            else if (residueName == "PHE") return true;
            else if (residueName == "PRO") return true;
            else if (residueName == "SER") return true;
            else if (residueName == "THR") return true;
            else if (residueName == "TRP") return true;
            else if (residueName == "TYR") return true;
            else if (residueName == "VAL") return true;
            else if (residueName == "ASN") return true;
            else return false;
        }

        /// <summary>
        /// Static method to obtain the single character abbreviation of an amino acid.
        /// </summary>
        /// <param name="residueName">A multi-character residue abbreviation.</param>
        /// <returns>A single character abbreviation if one is available, othewise return the input
        /// abbreviation.</returns>
        private static string GetResidueIdentifier(string residueName)
        {
            if (residueName == "HOH") return "O";
            else if (residueName == "ALA") return "A";
            else if (residueName == "ARG") return "R";
            else if (residueName == "ASP") return "D";
            else if (residueName == "CYS") return "C";
            else if (residueName == "GLN") return "Q";
            else if (residueName == "GLU") return "E";
            else if (residueName == "GLY") return "G";
            else if (residueName == "HIS") return "H";
            else if (residueName == "ILE") return "I";
            else if (residueName == "LEU") return "L";
            else if (residueName == "LYS") return "K";
            else if (residueName == "MET") return "M";
            else if (residueName == "PHE") return "F";
            else if (residueName == "PRO") return "P";
            else if (residueName == "SER") return "S";
            else if (residueName == "THR") return "T";
            else if (residueName == "TRP") return "W";
            else if (residueName == "TYR") return "Y";
            else if (residueName == "VAL") return "V";
            else if (residueName == "ASN") return "N";
            else return residueName;
        }

        /// <summary>
        /// Selects a color based on the residue type.
        /// </summary>
        /// <param name="residueName">A multi-character residue abbreviation.</param>
        /// <returns>A color for the residue.</returns>
        private static Color GetResidueColor(string residueName)
        {
            if (residueName == "HOH") return Color.red;
            else if (residueName == "ALA") return Colors.FromRgb(199, 199, 199);
            else if (residueName == "ARG") return Colors.FromRgb(229, 10, 10);
            else if (residueName == "CYS") return Colors.FromRgb(229, 229, 0);
            else if (residueName == "GLN") return Colors.FromRgb(0, 229, 229);
            else if (residueName == "GLU") return Colors.FromRgb(229, 10, 10);
            else if (residueName == "GLY") return Colors.FromRgb(234, 234, 234);
            else if (residueName == "HIS") return Colors.FromRgb(130, 130, 209);
            else if (residueName == "ILE") return Colors.FromRgb(15, 130, 15);
            else if (residueName == "LEU") return Colors.FromRgb(15, 130, 15);
            else if (residueName == "LYS") return Colors.FromRgb(20, 90, 255);
            else if (residueName == "MET") return Colors.FromRgb(229, 229, 0);
            else if (residueName == "PHE") return Colors.FromRgb(50, 50, 169);
            else if (residueName == "PRO") return Colors.FromRgb(219, 149, 130);
            else if (residueName == "SER") return Colors.FromRgb(249, 149, 0);
            else if (residueName == "THR") return Colors.FromRgb(249, 149, 0);
            else if (residueName == "TRP") return Colors.FromRgb(179, 90, 179);
            else if (residueName == "TYR") return Colors.FromRgb(50, 50, 169);
            else if (residueName == "VAL") return Colors.FromRgb(15, 130, 15);
            else if (residueName == "ASN") return Colors.FromRgb(0, 229, 229);
            else return Color.green;
        }


        /// <summary>
        /// Selects the material color for this residue based on the coloring method.
        /// </summary>
        private void UpdateColorView()
        {
            if (this.colorScheme == ColorScheme.Structure)
                this.color = this.structureColor;
            else if (this.colorScheme == ColorScheme.Atom && this.residueIdentifier == "O")
                this.color = Color.red;
            else if (this.colorScheme == ColorScheme.Atom)
                this.color = Color.gray;
            else if (this.colorScheme == ColorScheme.Residue)
                this.color = this.residueColor;
            else if (this.colorScheme == ColorScheme.Chain && this.chain != null)
                this.color = this.chain.ChainColor;
            else if (this.colorScheme == ColorScheme.Temperature)
                this.color = Atom.GetAverageTemperateColor(this.atoms);
            else
                this.color = Color.gray;
            
            this.UpdateView();
        }

        /// <summary>
        /// Updates the material color for this atom based on the coloring method and the current
        /// hover state.
        /// </summary>
        private void UpdateView()
        {
            Color actualColor = this.color;

            //if (false)
            //{
            //    byte r = (byte)(color.r + (255 - color.r) / 2);
            //    byte g = (byte)(color.g + (255 - color.g) / 2);
            //    byte b = (byte)(color.b + (255 - color.b) / 2);

            //    if (r == g && g == b) r = g = b = 255;

            //    actualColor = Colors.FromRgb(r, g, b);
            //}

            //foreach (ResidueStripItem residueStripItem in this.residueStripItems)
            //{
            //    residueStripItem.Label.Foreground = foreground;
            //    residueStripItem.Label.Background = background;
            //}

            if (this.cartoon != null)
                this.cartoon.cartooncolor = actualColor;
        }
    }
}
