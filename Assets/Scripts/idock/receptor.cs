using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;

namespace idock
{
    //! Represents a receptor.
    public class receptor
    {
        public List<string> lines = new List<string>();
        public Vec3 center; //!< Box center.
        public Vec3 size; //!< 3D sizes of box.
        public Vec3 corner0; //!< Box boundary corner with smallest values of all the 3 dimensions.
        public Vec3 corner1; //!< Box boundary corner with largest values of all the 3 dimensions.
        public float granularity; //!< 1D size of grids.
        public float granularity_inverse; //!< 1 / granularity.
        public int[] num_probes=new int[3]; //!< Number of probes.
        public int num_probes_product; //!< Product of num_probes[0,1,2].
        public List<atom> atoms=new List<atom>(); //!< Heavy atoms.
        public List<List<int>> p_offset=new List<List<int>>(); //!< Auxiliary precalculated constants to accelerate grid map creation.
        public List<List<float>> maps=new List<List<float>>(); //!< Grid maps.

        private static float sqrt(float v)
        {
            //return Mathf.Sqrt(v);
            return (float)Math.Sqrt(v);
        }

        //! Constructs a receptor by parsing a receptor file in PDBQT format.
        public receptor(string path,  Vec3 center,  Vec3 size, float granularity)
        {
            //center(center), size(size), corner0(center - 0.5 * size), corner1(corner0 + size), 
            //granularity(granularity), granularity_inverse(1 / granularity),
            //num_probes({{static_cast<size_t>(size[0] * granularity_inverse) + 2, static_cast<size_t>(size[1] * granularity_inverse) + 2, static_cast<size_t>(size[2] * granularity_inverse) + 2}}),
            //num_probes_product(num_probes[0] * num_probes[1] * num_probes[2]),
            //p_offset(scoring_function::n), maps(scoring_function::n)

            this.center = center;
            this.size = size;
            this.corner0 = center - 0.5f * size;
            this.corner1 = corner0 + size;
            this.granularity = granularity;
            this.granularity_inverse = 1.0f / granularity;
            this.num_probes[0]=(int)(size[0] * granularity_inverse) + 2;
            this.num_probes[1]=(int)(size[1] * granularity_inverse) + 2;
            this.num_probes[2]=(int)(size[2] * granularity_inverse) + 2;
            this.num_probes_product = num_probes[0] * num_probes[1] * num_probes[2];
            this.p_offset.Capacity = scoring_function.n ;//= new List<List<int>>(scoring_function.n);
            this.maps.Capacity = scoring_function.n;//=new List<List<float>>(scoring_function.n);
            for (int i = 0; i < scoring_function.n; i++)
            { 
                List<int> offset=new List<int>();
                p_offset.Add(offset);
                List<float> map = new List<float>();
                maps.Add(map);
            }
            //granularity(granularity), granularity_inverse(1.0f / granularity),
            //num_probes_product(num_probes[0] * num_probes[1] * num_probes[2]),
            //map_bytes(sizeof(float) * num_probes_product), p_offset(scoring_function::n), maps(scoring_function::n)

            // Parse the receptor line by line.
            atoms.Capacity = 5000; // A receptor typically consists of <= 2,000 atoms within bound.

            string residue = "XXXX"; // Current residue sequence located at 1-based [23, 26], used to track residue change, initialized to a dummy value.
            int residue_start = 0; // The starting atom of the current residue.

            string[] inputlines = File.ReadAllLines(path);
            foreach (string line in inputlines)
            {
                if (line == null)
                    continue;

                lines.Add(line);

                int cmdlen = 6;
                if (cmdlen > line.Length)
                    cmdlen = line.Length;
                string record = line.Substring(0, cmdlen);
                record = record.Trim();

                if (record == "ATOM" || record == "HETATM")
                {
                    // If this line is the start of a new residue, mark the starting index within the atoms vector.
                    if ((line[25] != residue[3]) || (line[24] != residue[2]) || (line[23] != residue[1]) || (line[22] != residue[0]))
                    {
                        //residue[3] = line[25];
                        //residue[2] = line[24];
                        //residue[1] = line[23];
                        //residue[0] = line[22];
                        //residue_start = atoms.size();

                        if (residue.Length - 4 > 0)
                            residue = residue.Substring(4, residue.Length - 4);
                        residue = line[25] + residue;
                        residue = line[24] + residue;
                        residue = line[23] + residue;
                        residue = line[22] + residue;
                        residue_start = atoms.Count;
                    }

                    // Parse the ATOM/HETATM line.
                    atom a = new atom(line);

                    // Skip unsupported atom types.
                    if (a.ad_unsupported())
                    {
                        a.ad = 2;
                        a.xs = 0;
                        a.rf = 0;
                    }

                    //if (a.ad_unsupported())
                        //continue;

                    // Skip non-polar hydrogens.
                    if (a.is_nonpolar_hydrogen()) continue;

                    // For a polar hydrogen, the bonded hetero atom must be a hydrogen bond donor.
                    if (a.is_polar_hydrogen())
                    {
                        for (int i = atoms.Count; i > residue_start; )
                        {
                            atom b = atoms[--i];
                            //if (b.is_hetero() && b.has_covalent_bond(a))
                            if (b.is_hetero() && b.is_neighbor(a))
                            {
                                b.donorize();
                                break;
                            }
                        }
                        continue;
                    }

                    // For a hetero atom, its connected carbon atoms are no longer hydrophobic.
                    if (a.is_hetero())
                    {
                        for (int i = atoms.Count; i > residue_start; )
                        {
                            atom b = atoms[--i];
                            //if (!b.is_hetero() && b.has_covalent_bond(a))
                            if (!b.is_hetero() && b.is_neighbor(a))
                            {
                                b.dehydrophobicize();
                            }
                        }
                    }
                    // For a carbon atom, it is no longer hydrophobic when connected to a hetero atom.
                    else
                    {
                        for (int i = atoms.Count; i > residue_start; )
                        {
                            atom b = atoms[--i];
                            //if (b.is_hetero() && b.has_covalent_bond(a))
                            if (b.is_hetero() && b.is_neighbor(a))
                            {
                                a.dehydrophobicize();
                                break;
                            }
                        }
                    }

                    // Save the atom if and only if its distance to its projection point on the box is within cutoff.
                    float r2 = 0;
                    for (int i = 0; i < 3; ++i)
                    {
                        if (a.coord[i] < corner0[i])
                        {
                            float d = a.coord[i] - corner0[i];
                            r2 += d * d;
                        }
                        else if (a.coord[i] > corner1[i])
                        {
                            float d = a.coord[i] - corner1[i];
                            r2 += d * d;
                        }
                    }
                    if (r2 < scoring_function.cutoff_sqr)
                    {
                        atoms.Add(a);
                    }
                }
                else if (record == "TER")
                {
                    residue = "XXXX";
                }
            }
        }

        public void calcBound()
        {
            if (atoms.Count < 1)
                return;

            Vec3 vmin = atoms[0].coord;
            Vec3 vmax = atoms[0].coord;
            for (int i = 1; i < atoms.Count; i++)
            {
                Vec3 vcur = atoms[i].coord;
                if (vcur.x < vmin.x)
                    vmin.x = vcur.x;
                if (vcur.y < vmin.y)
                    vmin.y = vcur.y;
                if (vcur.z < vmin.z)
                    vmin.z = vcur.z;
                if (vcur.x > vmax.x)
                    vmax.x = vcur.x;
                if (vcur.y > vmax.y)
                    vmax.y = vcur.y;
                if (vcur.z > vmax.z)
                    vmax.z = vcur.z;
            }

            this.center = (vmin + vmax) * 0.5f; 
            this.corner0 = center - 0.5f * size;
            this.corner1 = corner0 + size;
        }

        public bool within(Vec3 coord) 
        {
	        return corner0[0] <= coord[0] && coord[0] < corner1[0]
	            && corner0[1] <= coord[1] && coord[1] < corner1[1]
	            && corner0[2] <= coord[2] && coord[2] < corner1[2];
        }

        public int[] index(Vec3 coord) 
        {
	        return new int[]
	        {
		        (int)((coord[0] - corner0[0]) * granularity_inverse),
		        (int)((coord[1] - corner0[1]) * granularity_inverse),
		        (int)((coord[2] - corner0[2]) * granularity_inverse),
	        };
        }

        public int index(int[] idx) 
        {
	        return num_probes[0] * (num_probes[1] * idx[2] + idx[1]) + idx[0];
        }

        //! Precalculates auxiliary constants to accelerate grid map creation.
        public void precalculate( ref List<int> xs)
        {
            int nxs = xs.Count;
            for (int t0 = 0; t0 < scoring_function.n; ++t0)
            {
                List<int> p = p_offset[t0];
                if (nxs>0 && p.Capacity<nxs)
                    p.Capacity = nxs;
                for (int i = 0; i < nxs; ++i)
                {
                    int t1 = xs[i];
                    //p[i] = scoring_function.nr * IMath.mp(t0, t1);
                    p.Add ( IMath.mp(t0, t1));
                }
            }
        }

        //! Populates grid maps for certain atom types along X and Y dimensions for a given Z dimension value.
        public void populate(ref List<int> xs, int z, scoring_function sf)
        {
            int n = xs.Count;
            float z_coord = corner0[2] + granularity * z;
            int z_offset = num_probes[0] * num_probes[1] * z;

            foreach (atom a in atoms)
            {
                //assert(!a.is_hydrogen());
                float dz = z_coord - a.coord[2];
                float dz_sqr = dz * dz;
                float dydx_sqr_ub = scoring_function.cutoff_sqr - dz_sqr;
                if (dydx_sqr_ub <= 0) continue;
                float dydx_ub = sqrt(dydx_sqr_ub);
                float y_lb = a.coord[1] - dydx_ub;
                float y_ub = a.coord[1] + dydx_ub;
                int y_beg = y_lb > corner0[1] ? (y_lb < corner1[1] ? (int)((y_lb - corner0[1]) * granularity_inverse) : num_probes[1]) : 0;
                int y_end = y_ub > corner0[1] ? (y_ub < corner1[1] ? (int)((y_ub - corner0[1]) * granularity_inverse) + 1 : num_probes[1]) : 0;
                List<int> p = p_offset[a.xs];
                int zy_offset = z_offset + num_probes[0] * y_beg;
                float dy = corner0[1] + granularity * y_beg - a.coord[1];
                for (int y = y_beg; y < y_end; ++y, zy_offset += num_probes[0], dy += granularity)
                {
                    float dy_sqr = dy * dy;
                    float dx_sqr_ub = dydx_sqr_ub - dy_sqr;
                    if (dx_sqr_ub <= 0) continue;
                    float dx_ub = sqrt(dx_sqr_ub);
                    float x_lb = a.coord[0] - dx_ub;
                    float x_ub = a.coord[0] + dx_ub;
                    int x_beg = x_lb > corner0[0] ? (x_lb < corner1[0] ? (int)((x_lb - corner0[0]) * granularity_inverse) : num_probes[0]) : 0;
                    int x_end = x_ub > corner0[0] ? (x_ub < corner1[0] ? (int)((x_ub - corner0[0]) * granularity_inverse) + 1 : num_probes[0]) : 0;
                    float dzdy_sqr = dz_sqr + dy_sqr;
                    int zyx_offset = zy_offset + x_beg;
                    float dx = corner0[0] + granularity * x_beg - a.coord[0];
                    for (int x = x_beg; x < x_end; ++x, ++zyx_offset, dx += granularity)
                    {
                        float dx_sqr = dx * dx;
                        float r2 = dzdy_sqr + dx_sqr;
                        if (r2 >= scoring_function.cutoff_sqr) continue;
                        int r_offset = (int)(scoring_function.ns * r2);
                        for (int i = 0; i < n; ++i)
                        {
                            maps[xs[i]][zyx_offset] += sf.ee[p[i]][r_offset]; //sf.e[p[i] + r_offset];
                        }
                    }
                }
            }
        }

        public void write_models(ref List<string> outputlines)
        {
            outputlines.AddRange(lines);
        }
    }

}

