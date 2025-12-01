using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;

namespace idock
{

    //! Represents an atom of either receptor or ligand.
    public class atom
    {
        private const int n = 33; //!< Number of AutoDock4 atom types.
        //!< AutoDock4 atom type strings, e.g. H, HD, C, A.
        private static List<string> ad_strings = new List<string>()
{
	"H" , //  0
	"HD", //  1
	"C" , //  2
	"A" , //  3
	"N" , //  4
	"NA", //  5
	"OA", //  6
	"S" , //  7
	"SA", //  8
	"Se", //  9
	"P" , // 10
	"F" , // 11
	"Cl", // 12
	"Br", // 13
	"I" , // 14
	"Zn", // 15
	"Fe", // 16
	"Mg", // 17
	"Ca", // 18
	"Mn", // 19
	"Cu", // 20
	"Na", // 21
	"K" , // 22
	"Hg", // 23
	"Ni", // 24
	"Co", // 25
	"Cd", // 26
	"As", // 27
	"Sr", // 28
	"U" , // 29
	"Cs", // 30
	"Mo", // 31
	"Si", // 32
};

        //!< Covalent radii of AutoDock4 atom types.
        private static float[] ad_covalent_radii = new float[]
{
	0.407f, //  0 = H , 0.407 = 1.1 * 0.37
	0.407f, //  1 = HD, 0.407 = 1.1 * 0.37
	0.847f, //  2 = C , 0.847 = 1.1 * 0.77
	0.847f, //  3 = A , 0.847 = 1.1 * 0.77
	0.825f, //  4 = N , 0.825 = 1.1 * 0.75
	0.825f, //  5 = NA, 0.825 = 1.1 * 0.75
	0.803f, //  6 = OA, 0.803 = 1.1 * 0.73
	1.122f, //  7 = S , 1.122 = 1.1 * 1.02
	1.122f, //  8 = SA, 1.122 = 1.1 * 1.02
	1.276f, //  9 = Se, 1.276 = 1.1 * 1.16
	1.166f, // 10 = P , 1.166 = 1.1 * 1.06
	0.781f, // 11 = F , 0.781 = 1.1 * 0.71
	1.089f, // 12 = Cl, 1.089 = 1.1 * 0.99
	1.254f, // 13 = Br, 1.254 = 1.1 * 1.14
	1.463f, // 14 = I , 1.463 = 1.1 * 1.33
	1.441f, // 15 = Zn, 1.441 = 1.1 * 1.31
	1.375f, // 16 = Fe, 1.375 = 1.1 * 1.25
	1.430f, // 17 = Mg, 1.430 = 1.1 * 1.30
	1.914f, // 18 = Ca, 1.914 = 1.1 * 1.74
	1.529f, // 19 = Mn, 1.529 = 1.1 * 1.39
	1.518f, // 20 = Cu, 1.518 = 1.1 * 1.38
	1.694f, // 21 = Na, 1.694 = 1.1 * 1.54
	2.156f, // 22 = K , 2.156 = 1.1 * 1.96
	1.639f, // 23 = Hg, 1.639 = 1.1 * 1.49
	1.331f, // 24 = Ni, 1.331 = 1.1 * 1.21
	1.386f, // 25 = Co, 1.386 = 1.1 * 1.26
	1.628f, // 26 = Cd, 1.628 = 1.1 * 1.48
	1.309f, // 27 = As, 1.309 = 1.1 * 1.19
	2.112f, // 28 = Sr, 2.112 = 1.1 * 1.92
	2.156f, // 29 = U , 2.156 = 1.1 * 1.96
	2.475f, // 30 = Cs, 2.475 = 1.1 * 2.25
	1.595f, // 31 = Mo, 1.595 = 1.1 * 1.45
	1.221f, // 32 = Si, 1.221 = 1.1 * 1.11
};

        //!< AutoDock4 to XScore atom type conversion.
        private static int[] ad_to_xs = new int[n]
{
	 n, //  0 = H  -> dummy
	 n, //  1 = HD -> dummy
	 0, //  2 = C  -> C_H   =  0, Carbon, hydrophobic, not bonded to a hetero atom.
	 0, //  3 = A  -> C_H   =  0, Carbon, hydrophobic, not bonded to a hetero atom.
	 2, //  4 = N  -> N_P   =  2, Nitrogen, neither hydrogen bond donor nor acceptor.
	 4, //  5 = NA -> N_A   =  4, Nitrogen, hydrogen bond acceptor.
	 6, //  6 = OA -> O_A   =  6, Oxygen, hydrogen bond acceptor.
	 8, //  7 = S  -> S_P   =  8, Sulfur or Selenium.
	 8, //  8 = SA -> S_P   =  8, Sulfur or Selenium.
	 8, //  9 = Se -> S_P   =  8, Sulfur or Selenium.
	 9, // 10 = P  -> P_P   =  9, Phosphorus.
	10, // 11 = F  -> F_H   = 10, Fluorine, hydrophobic.
	11, // 12 = Cl -> Cl_H  = 11, Chlorine, hydrophobic.
	12, // 13 = Br -> Br_H  = 12, Bromine, hydrophobic.
	13, // 14 = I  -> I_H   = 13, Iodine, hydrophobic.
	14, // 15 = Zn -> Met_D = 14, Metal, hydrogen bond donor.
	14, // 16 = Fe -> Met_D = 14, Metal, hydrogen bond donor.
	14, // 17 = Mg -> Met_D = 14, Metal, hydrogen bond donor.
	14, // 18 = Ca -> Met_D = 14, Metal, hydrogen bond donor.
	14, // 19 = Mn -> Met_D = 14, Metal, hydrogen bond donor.
	14, // 20 = Cu -> Met_D = 14, Metal, hydrogen bond donor.
	14, // 21 = Na -> Met_D = 14, Metal, hydrogen bond donor.
	14, // 22 = K  -> Met_D = 14, Metal, hydrogen bond donor.
	14, // 23 = Hg -> Met_D = 14, Metal, hydrogen bond donor.
	14, // 24 = Ni -> Met_D = 14, Metal, hydrogen bond donor.
	14, // 25 = Co -> Met_D = 14, Metal, hydrogen bond donor.
	14, // 26 = Cd -> Met_D = 14, Metal, hydrogen bond donor.
	14, // 27 = As -> Met_D = 14, Metal, hydrogen bond donor.
	14, // 28 = Sr -> Met_D = 14, Metal, hydrogen bond donor.
	14, // 29 = U  -> Met_D = 14, Metal, hydrogen bond donor.
	14, // 30 = Cs -> Met_D = 14, Metal, hydrogen bond donor.
	14, // 31 = Mo -> Met_D = 14, Metal, hydrogen bond donor.
	14, // 32 = Si -> Met_D = 14, Metal, hydrogen bond donor.
};

        //!< AutoDock4 to RF-Score atom type conversion.
        private static int[] ad_to_rf = new int[n]
{
	n, //  0 = H  -> dummy
	n, //  1 = HD -> dummy
	0, //  2 = C  -> C  = 0
	0, //  3 = A  -> C  = 0
	1, //  4 = N  -> N  = 1
	1, //  5 = NA -> N  = 1
	2, //  6 = OA -> O  = 2
	3, //  7 = S  -> S  = 3
	3, //  8 = SA -> S  = 3
	n, //  9 = Se -> dummy
	4, // 10 = P  -> P  = 4
	5, // 11 = F  -> F  = 5
	6, // 12 = Cl -> Cl = 6
	7, // 13 = Br -> Br = 7
	8, // 14 = I  -> I  = 8
	n, // 15 = Zn -> dummy
	n, // 16 = Fe -> dummy
	n, // 17 = Mg -> dummy
	n, // 18 = Ca -> dummy
	n, // 19 = Mn -> dummy
	n, // 20 = Cu -> dummy
	n, // 21 = Na -> dummy
	n, // 22 = K  -> dummy
	n, // 23 = Hg -> dummy
	n, // 24 = Ni -> dummy
	n, // 25 = Co -> dummy
	n, // 26 = Cd -> dummy
	n, // 27 = As -> dummy
	n, // 28 = Sr -> dummy
	n, // 29 = U  -> dummy
	n, // 30 = Cs -> dummy
	n, // 31 = Mo -> dummy
	n, // 32 = Si -> dummy
};

        public int serial; //!< Atom serial.
        public string name; //!< Atom name, 4 characters wide.
        public Vec3 coord; //!< Coordinate.
        public int ad; //!< AutoDock4 atom type.
        public int xs; //!< XScore atom type.
        public int rf; //!< RF-Score atom type.
        public List<atom> hydrogens; //!< Hydrogens connected to the current atom.

        private bool isspace(char c)
        {
            return c == ' ';
        }

        //! Constructs an atom from an ATOM/HETATM line in PDBQT format.
        public atom(string line)
        {
            serial = int.Parse(line.Substring(6, 5));
            //serial(stoul(line.substr(6, 5))),
            name = line.Substring(12, 4);
            //name(line.substr(12, 4)),
            float x = float.Parse(line.Substring(30, 8));
            float y = float.Parse(line.Substring(38, 8));
            float z = float.Parse(line.Substring(46, 8));
            coord = new Vec3(x, y, z);
            //coord({ stof(line.substr(30, 8)), stof(line.substr(38, 8)), stof(line.substr(46, 8)) }),
            string searchstr = line.Substring(77, isspace(line[78]) ? 1 : 2);
            for (int k = 0; k < ad_strings.Count; k++)
            {
                if (ad_strings[k] == searchstr)
                    ad = k;
            }
            //ad(find(ad_strings.cbegin(), ad_strings.cend(), searchstr) - ad_strings.cbegin()),
            xs = ad_to_xs[ad];
            //xs(ad_to_xs[ad]),
            rf = ad_to_rf[ad];
            //rf(ad_to_rf[ad])    
        }

        //! Returns true if the AutoDock4 atom type is not supported.
        public bool ad_unsupported()
        {
            return ad >= n;
        }

        //! Returns true if the XScore atom type is not supported.
        public bool xs_unsupported()
        {
            return xs >= n;
        }

        //! Returns true if the RF-Score atom type is not supported.
        public bool rf_unsupported()
        {
            return rf >= n;
        }

        //! Returns true if the atom is a nonpolar hydrogen atom, which is connected to a carbon atom.
        public bool is_nonpolar_hydrogen()
        {
            return ad == 0;
        }

        //! Returns true if the atom is a polar hydrogen atom, which is connected to a heavy atom other than carbon.
        public bool is_polar_hydrogen()
        {
            return ad == 1;
        }

        //! Returns true if the atom is a hydrogen atom, either polar or nonpolar.
        public bool is_hydrogen()
        {
            return ad <= 1;
        }

        //! Returns true if the atom is a hetero atom, i.e. non-carbon heavy atom.
        public bool is_hetero()
        {
            return ad >= 4;
        }

        //! Revises the XScore atom type of a nitrogen or an oxygen to its corresponding variant of hydrogen bond donor.
        public void donorize()
        {
            switch (xs)
            {
                case 2: xs = 3; break; // Nitrogen, hydrogen bond donor.
                case 4: xs = 5; break; // Nitrogen, both hydrogen bond donor and acceptor.
                case 6: xs = 7; break; // Oxygen, both hydrogen bond donor and acceptor.
            }
        }

        //! Revises the XScore atom type of a carbon to its corresponding non-hydrophobic variant.
        public void dehydrophobicize()
        {
            //assert(xs <= 1);
            xs = 1; // Carbon, bonded to a hetero atom.
        }


        //! Returns the covalent radius of the current AutoDock4 atom type.
        public float covalent_radius()
        {
            return ad_covalent_radii[ad];
        }

        //! Returns true if the current atom is covalently bonded to a given atom, i.e. their distance is within a certain threshold which depends on their covalent radii.
        public bool has_covalent_bond(atom a)
        {
            float s = covalent_radius() + a.covalent_radius();
            return IMath.distance_sqr(coord, a.coord) < s * s;
        }

        //! Returns true if the current atom is covalently bonded to a given atom.
        public bool is_neighbor(atom a)
        {
	        //assert(this != &a);
	        float r = covalent_radius() + a.covalent_radius();
	        return  IMath.distance_sqr(coord, a.coord) < r * r;
        }

        private string setw(int n)
        {
            string s = "";
            for(int i=0;i<n;i++)
                s+=' ';
            return s;
        }

        //! Outputs an ATOM line in PDBQT format.
        //void output(boost::filesystem::ofstream& ofs, const array<float, 3>& coord) const;
        public void output(StreamWriter ofs, Vec3 coord) 
        {
            string s = "ATOM  " + setw(5) + serial.ToString() + ' ' + name + setw(14) + "" + 
                setw(8) + coord[0] + setw(8) + coord[1] + setw(8) + coord[2] + setw(23) + "" +
                ad_strings[ad] + (ad_strings[ad].Length== 1 ? " " : "") + '\n';
            ofs.WriteLine(ofs);
        }
    };
}