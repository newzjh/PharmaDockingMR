using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;

namespace idock
{
    //! Represents a ligand conformation.
    public class conformation 
    {
        public Vec3 position; //!< Ligand origin coordinate.
        public Quat4 orientation; //!< Ligand orientation.
        public float[] torsions; //!< Ligand torsions.

        //! Constructs an initial conformation.
        public conformation(int num_active_torsions)
        {
            position = Vec3.zero;
            orientation = new Quat4(1.0f, 0.0f, 0.0f, 0.0f);
            torsions = new float[num_active_torsions];
        }

        public conformation Clone()
        {
            conformation newc = new conformation(this.torsions.Length);
            newc.position = this.position;
            newc.orientation = this.orientation;
            newc.torsions = new float[this.torsions.Length];
            Array.Copy(this.torsions, newc.torsions, this.torsions.Length);
            return newc;
        }
    };

    //! Represents a transition from one conformation to another.
    public class change
    {
        float[] items;

        //! Constructs a zero change.
        public change(int num_active_torsions)
        {
            //: vector<double>(6 + num_active_torsions, 0) {}
            items = new float[6 + num_active_torsions];
        }

        public float this[int index]
        {
            get
            {
                return items[index];
            }
            set
            {
                items[index] = value;
            }
        }

        public change Clone()
        {
            change newg = new change(this.items.Length);
            Array.Copy(this.items, newg.items, this.items.Length);
            return newg;
        }
    };

    public class ligand
    {
        public List<string> lines = new List<string>();
        public List<frame> frames=new List<frame>(); //!< ROOT and BRANCH frames.
        public List<atom> heavy_atoms=new List<atom>(); //!< Heavy atoms. Coordinates are relative to frame origin, which is the first atom by default.
        public List<atom> hydrogens=new List<atom>(); //!< Hydrogen atoms. Coordinates are relative to frame origin, which is the first atom by default.
        public bool[] xs = new bool[scoring_function.n]; //!< Presence of XScore atom types.
        public int num_heavy_atoms; //!< Number of heavy atoms.
        public int num_hydrogens; //!< Number of hydrogens.
        public int num_frames; //!< Number of frames.
        public int num_torsions; //!< Number of torsions.
        public int num_active_torsions; //!< Number of active torsions.
        public float flexibility_penalty_factor; //!< A value in (0, 1] to penalize ligand flexibility.

        private static void assert(bool b)
        {
            Kernel.assert(b);
        }

        //! Represents a pair of interacting atoms that are separated by 3 consecutive covalent bonds.
        public class interacting_pair
        {
            public int i0; //!< Index of atom 0.
            public int i1; //!< Index of atom 1.
            public int p_offset; //!< Type pair index to the scoring function. It can be precalculated to save the creating time of grid maps.

            //! Constructs a pair of non 1-4 interacting atoms.
            public interacting_pair(int i0, int i1, int p_offset)
            {
                this.i0 = i0;
                this.i1 = i1;
                this.p_offset = p_offset;
            }
        };

        private List<interacting_pair> interacting_pairs = new List<interacting_pair>(); //!< Non 1-4 interacting pairs.

        public ligand(string path, ref Vec3 origin)
        {
            //: xs{}, num_active_torsions(0)

            // Initialize necessary variables for constructing a ligand.
            frames.Capacity = 30;; // A ligand typically consists of <= 30 frames.
            frames.Add(new frame(0, 0, 1, 0, 0, 0)); // ROOT is also treated as a frame. The parent and rotorX of ROOT frame are dummy.
            heavy_atoms.Capacity =100;; // A ligand typically consists of <= 100 heavy atoms.
            hydrogens.Capacity=50; // A ligand typically consists of <= 50 hydrogens.

            // Initialize helper variables for parsing.
            List<List<int>> bonds = new List<List<int>>(); //!< Covalent bonds.
            bonds.Capacity = 100; // A ligand typically consists of <= 100 heavy atoms.
            int current = 0; // Index of current frame, initialized to ROOT frame.
            frame f = frames[0]; // Pointer to the current frame.
            f.rotorYidx = 0; // Assume the rotorY of ROOT frame is the first atom.

            // Start parsing.
            string[] inputlines = File.ReadAllLines(path);
            foreach (string line in inputlines)
            {
                if (line == null) 
                    continue;

                int cmdlen = 6;
                if (cmdlen > line.Length) 
                    cmdlen = line.Length;
                string record = line.Substring(0, cmdlen);
                record = record.Trim();
                if (record == "ATOM" || record == "HETATM")
                {
                    // Whenever an ATOM/HETATM line shows up, the current frame must be the last one.
                    assert(current == frames.Count - 1);
                    assert(f == frames[frames.Count - 1]);

                    // This line will be dumped to the output ligand file.
                    lines.Add(line);

                    // Parse the line.
                    atom a = new atom(line);

                    // Harmonize a unsupported atom type to carbon.
                    if (a.ad_unsupported())
                        a.ad = 2;

                    if (a.is_hydrogen()) // Current atom is a hydrogen.
                    {
                        // For a polar hydrogen, the bonded hetero atom must be a hydrogen bond donor.
                        if (a.is_polar_hydrogen())
                        {
                            for (int i = heavy_atoms.Count-1; i >= f.habegin; i-- )
                            {
                                atom b = heavy_atoms[i];
                                if (!b.is_hetero()) continue; // Only a hetero atom can be a hydrogen bond donor.
                                if (a.is_neighbor(b))
                                {
                                    b.donorize();
                                    break;
                                }
                            }
                        }

                        // Save the hydrogen.
                        hydrogens.Add(a);
                    }
                    else // Current atom is a heavy atom.
                    {
                        // Find bonds between the current atom and the other atoms of the same frame.
                        assert(bonds.Count == heavy_atoms.Count);
                        bonds.Add(new List<int>(4));
                        for (int i = heavy_atoms.Count-1; i >= f.habegin; i--)
                        {
                            atom b = heavy_atoms[i];
                            if (a.is_neighbor(b))
                            {
                                bonds[heavy_atoms.Count].Add(i);
                                bonds[i].Add(heavy_atoms.Count);

                                // If carbon atom b is bonded to hetero atom a, b is no longer a hydrophobic atom.
                                if (a.is_hetero() && !b.is_hetero())
                                {
                                    b.dehydrophobicize();
                                }
                                // If carbon atom a is bonded to hetero atom b, a is no longer a hydrophobic atom.
                                else if (!a.is_hetero() && b.is_hetero())
                                {
                                    a.dehydrophobicize();
                                }
                            }
                        }

                        // Set rotorYidx if the serial number of current atom is rotorYsrn.
                        if (current != 0 && (a.serial == f.rotorYsrn)) // current > 0, i.e. BRANCH frame.
                        {
                            f.rotorYidx = heavy_atoms.Count;
                        }

                        // Save the heavy atom.
                        heavy_atoms.Add(a);
                    }
                }
                else if (record == "BRANCH")
                {
                    // This line will be dumped to the output ligand file.
                    lines.Add(line);

                    // Parse "BRANCH   X   Y". X and Y are right-justified and 4 characters wide.
                    int rotorXsrn = int.Parse(line.Substring(6, 4));
                    int rotorYsrn = int.Parse(line.Substring(10, 4));

                    // Find the corresponding heavy atom with x as its atom serial number in the current frame.
                    for (int i = f.habegin; true; ++i)
                    {
                        if (heavy_atoms[i].serial == rotorXsrn)
                        {
                            // Insert a new frame whose parent is the current frame.
                            frames.Add(new frame(current, rotorXsrn, rotorYsrn, i, heavy_atoms.Count, hydrogens.Count));
                            break;
                        }
                    }

                    // Now the current frame is the newly inserted BRANCH frame.
                    current = frames.Count - 1;

                    // Update the pointer to the current frame.
                    f = frames[current];

                    // The ending index of atoms of previous frame is the starting index of atoms of current frame.
                    frames[current - 1].haend = f.habegin;
                    frames[current - 1].hyend = f.hybegin;
                }
                else if (record == "ENDBRA")
                {
                    // A frame may be empty, e.g. "BRANCH   4   9" is immediately followed by "ENDBRANCH   4   9".
                    // This emptiness is likely to be caused by invalid input structure, especially when all the atoms are located in the same plane.
                    if (f.habegin == heavy_atoms.Count)
                    {
                        frames.RemoveAt(frames.Count - 1);
                        //frames.pop_back();
                        lines.RemoveAt(lines.Count - 1);
                        //lines.pop_back();
                    }
                    else
                    {
                        // This line will be dumped to the output ligand file.
                        lines.Add(line);

                        // If the current frame consists of rotor Y and a few hydrogens only, e.g. -OH and -NH2,
                        // the torsion of this frame will have no effect on scoring and is thus redundant.
                        if (current + 1 == frames.Count && f.habegin + 1 == heavy_atoms.Count)
                        {
                            f.active = false;
                        }
                        else
                        {
                            ++num_active_torsions;
                        }

                        // Set up bonds between rotorX and rotorY.
                        bonds[f.rotorYidx].Add(f.rotorXidx);
                        bonds[f.rotorXidx].Add(f.rotorYidx);

                        // Dehydrophobicize rotorX and rotorY if necessary.
                        atom rotorY = heavy_atoms[f.rotorYidx];
                        atom rotorX = heavy_atoms[f.rotorXidx];
                        if (rotorY.is_hetero() && !rotorX.is_hetero()) rotorX.dehydrophobicize();
                        if (rotorX.is_hetero() && !rotorY.is_hetero()) rotorY.dehydrophobicize();

                        // Calculate parent_rotorY_to_current_rotorY and parent_rotorX_to_current_rotorY.
                        frame p = frames[f.parent];
                        f.parent_rotorY_to_current_rotorY = rotorY.coord - heavy_atoms[p.rotorYidx].coord;
                        f.parent_rotorX_to_current_rotorY = IMath.normalize(rotorY.coord - rotorX.coord);
                    }

                    // Now the parent of the following frame is the parent of current frame.
                    current = f.parent;

                    // Update the pointer to the current frame.
                    f = frames[current];
                }
                else if (record == "ROOT" || record == "ENDROO" || record == "TORSDO")
                {
                    // This line will be dumped to the output ligand file.
                    lines.Add(line);
                }
            }
            assert(current == 0); // current should remain its original value if "BRANCH" and "ENDBRANCH" properly match each other.
            assert(f == frames[0]); // The frame pointer should remain its original value if "BRANCH" and "ENDBRANCH" properly match each other.

            // Determine num_heavy_atoms and num_hydrogens.
            num_heavy_atoms = heavy_atoms.Count;
            num_hydrogens = hydrogens.Count;
            frames[frames.Count - 1].haend = num_heavy_atoms;
            frames[frames.Count - 1].hyend = num_hydrogens;

            // Determine num_frames, num_torsions, flexibility_penalty_factor.
            num_frames = frames.Count;
            assert(num_frames >= 1);
            num_torsions = num_frames - 1;
            assert(num_torsions + 1 == num_frames);
            assert(num_torsions >= num_active_torsions);
            int totalcount = num_heavy_atoms + num_hydrogens + (num_torsions << 1) + 3;
            assert(totalcount == lines.Count); // ATOM/HETATM lines + BRANCH/ENDBRANCH lines + ROOT/ENDROOT/TORSDOF lines == lines.size()
            flexibility_penalty_factor = 1.0f / (1.0f + 0.05846f * (num_active_torsions + 0.5f * (num_torsions - num_active_torsions)));
            assert(flexibility_penalty_factor <= 1);

            // Detect the presence of XScore atom types.
            foreach (atom a in heavy_atoms)
            {
                xs[a.xs] = true;
            }

            // Save the coordinate of the first heavy atom to origin.
            origin = heavy_atoms[frames[0].rotorYidx].coord;

            // Update heavy_atoms[].coord and hydrogens[].coord relative to frame origin.
            for (int k = 0; k < num_frames; ++k)
            {
                frame ff = frames[k];
                Vec3 origincoord = heavy_atoms[ff.rotorYidx].coord;
                for (int i = ff.habegin; i < ff.haend; ++i)
                {
                    heavy_atoms[i].coord -= origincoord;
                }
                for (int i = ff.hybegin; i < ff.hyend; ++i)
                {
                    hydrogens[i].coord -= origincoord;
                }
            }

            // Find intra-ligand interacting pairs that are not 1-4.
            interacting_pairs.Capacity = num_heavy_atoms * num_heavy_atoms;
            List<int> neighbors = new List<int>(10); // An atom typically consists of <= 10 neighbors.
            for (int k1 = 0; k1 < num_frames; ++k1)
            {
                frame f1 = frames[k1];
                for (int i = f1.habegin; i < f1.haend; ++i)
                {
                    // Find neighbor atoms within 3 consecutive covalent bonds.
                    List<int> i0_bonds = bonds[i];
                    int num_i0_bonds = i0_bonds.Count;
                    for (int i0 = 0; i0 < num_i0_bonds; ++i0)
                    {
                        int b1 = i0_bonds[i0];
                        //if (find(neighbors.begin(), neighbors.end(), b1) == neighbors.end())
                        if (!neighbors.Contains(b1))
                        {
                            neighbors.Add(b1);
                        }
                        List<int> i1_bonds = bonds[b1];
                        int num_i1_bonds = i1_bonds.Count;
                        for (int i1 = 0; i1 < num_i1_bonds; ++i1)
                        {
                            int b2 = i1_bonds[i1];
                            //if (find(neighbors.begin(), neighbors.end(), b2) == neighbors.end())
                            if (!neighbors.Contains(b2))
                            {
                                neighbors.Add(b2);
                            }
                            List<int> i2_bonds = bonds[b2];
                            int num_i2_bonds = i2_bonds.Count;
                            for (int i2 = 0; i2 < num_i2_bonds; ++i2)
                            {
                                int b3 = i2_bonds[i2];
                                //if (find(neighbors.begin(), neighbors.end(), b3) == neighbors.end())
                                if (!neighbors.Contains(b3))
                                {
                                    neighbors.Add(b3);
                                }
                            }
                        }
                    }

                    // Determine if interacting pairs can be possibly formed.
                    for (int k2 = k1 + 1; k2 < num_frames; ++k2)
                    {
                        frame f2 = frames[k2];
                        frame f3 = frames[f2.parent];
                        for (int j = f2.habegin; j < f2.haend; ++j)
                        {
                            if (k1 == f2.parent && (i == f2.rotorXidx || j == f2.rotorYidx)) continue;
                            if (k1 > 0 && f1.parent == f2.parent && i == f1.rotorYidx && j == f2.rotorYidx) continue;
                            if (f2.parent > 0 && k1 == f3.parent && i == f3.rotorXidx && j == f2.rotorYidx) continue;
                            //if (find(neighbors.cbegin(), neighbors.cend(), j) != neighbors.cend()) continue;
                            if (neighbors.Contains(j)) continue;
                            interacting_pairs.Add(new interacting_pair(i, j, IMath.mp(heavy_atoms[i].xs, heavy_atoms[j].xs)));
                        }
                    }

                    // Clear the current neighbor set for the next atom.
                    neighbors.Clear();
                }
            }
        }

        public bool evaluate(ref conformation conf, ref scoring_function sf, ref receptor rec,
            float e_upper_bound, ref float e, ref float f, ref change g)
        {
            if (!rec.within(conf.position))
                return false;

            // Initialize frame-wide conformational variables.
            List<Vec3> orig = new List<Vec3>(num_frames); //!< Origin coordinate, which is rotorY.
            List<Vec3> axes = new List<Vec3>(num_frames); //!< List pointing from rotor Y to rotor X.
            List<Quat4> oriq = new List<Quat4>(num_frames); //!< Orientation in the form of Quat4.
            List<float[]> orim = new List<float[]>(num_frames); //!< Orientation in the form of 3x3 matrix.
            List<Vec3> forc = new List<Vec3>(num_frames); //!< Aggregated derivatives of heavy atoms.
            List<Vec3> torq = new List<Vec3>(num_frames); //!< Torque of the force.
            for (int i = 0; i < num_frames; i++)
            {
                orig.Add(Vec3.zero);
                axes.Add(Vec3.zero);
                oriq.Add(new Quat4(0.0f,0.0f,0.0f,0.0f));
                orim.Add(new float[9]);
                forc.Add(Vec3.zero);
                torq.Add(Vec3.zero);
            }

            // Initialize atom-wide conformational variables.
            List<Vec3> coor = new List<Vec3>(num_heavy_atoms); //!< Heavy atom coordinates.
            List<Vec3> deri = new List<Vec3>(num_heavy_atoms); //!< Heavy atom derivatives.
            for (int i = 0; i < num_heavy_atoms; i++)
            {
                coor.Add(Vec3.zero);
                deri.Add(Vec3.zero);
            }

            // Apply position and orientation to ROOT frame.
            frame root = frames[0];
            orig[0] = conf.position;
            oriq[0] = conf.orientation;
            orim[0] = IMath.qtn4_to_mat3(conf.orientation);
            for (int i = root.habegin; i < root.haend; ++i)
            {
                coor[i] = orig[0] + IMath.mul(orim[0], heavy_atoms[i].coord);
                if (!rec.within(coor[i]))
                    return false;
            }

            // Apply torsions to BRANCH frames.
            for (int k = 1, t = 0; k < num_frames; ++k)
            {
                frame ff = frames[k];

                // Update origin.
                orig[k] = orig[ff.parent] + IMath.mul(orim[ff.parent], ff.parent_rotorY_to_current_rotorY);
                if (!rec.within(orig[k]))
                    return false;

                // If the current BRANCH frame does not have an active torsion, skip it.
                if (!ff.active)
                {
                    assert(ff.habegin + 1 == ff.haend);
                    assert(ff.habegin == ff.rotorYidx);
                    coor[ff.rotorYidx] = orig[k];
                    continue;
                }

                // Update orientation.
                assert(IMath.normalized(ff.parent_rotorX_to_current_rotorY));
                axes[k] = IMath.mul(orim[ff.parent], ff.parent_rotorX_to_current_rotorY);
                assert(IMath.normalized(axes[k]));
                oriq[k] = IMath.mul(IMath.vec4_to_qtn4(axes[k], conf.torsions[t++]) , oriq[ff.parent]);
                assert(IMath.normalized(oriq[k]));
                orim[k] = IMath.qtn4_to_mat3(oriq[k]);

                // Update coordinates.
                for (int i = ff.habegin; i < ff.haend; ++i)
                {
                    coor[i] = orig[k] + IMath.mul(orim[k], heavy_atoms[i].coord);
                    if (!rec.within(coor[i]))
                        return false;
                }
            }

            // Check steric clash between atoms of different frames except for (rotorX, rotorY) pair.
            //for (int k1 = num_frames - 1; k1 > 0; --k1)
            //{
            //	 frame& f1 = frames[k1];
            //	for (int i1 = f1.habegin; i1 < f1.haend; ++i1)
            //	{
            //		for (int k2 = 0; k2 < k1; ++k2)
            //		{
            //			 frame& f2 = frames[k2];
            //			for (int i2 = f2.habegin; i2 < f2.haend; ++i2)
            //			{
            //				if ((distance_sqr(coor[i1], coor[i2]) < sqr(heavy_atoms[i1].covalent_radius() + heavy_atoms[i2].covalent_radius())) && (!((k2 == f1.parent) && (i1 == f1.rotorYidx) && (i2 == f1.rotorXidx))))
            //					return false;
            //			}
            //		}
            //	}
            //}

            e = 0;
            for (int i = 0; i < num_heavy_atoms; ++i)
            {
                // Retrieve the grid map in need.
                List<float> map = rec.maps[heavy_atoms[i].xs];
                assert(map.Count != 0);

                // Find the index and fraction of the current coor.
                int[] index = rec.index(coor[i]);

                // Assert the validity of index.
                assert(index[0] + 1 < rec.num_probes[0]);
                assert(index[1] + 1 < rec.num_probes[1]);
                assert(index[2] + 1 < rec.num_probes[2]);

                // (x0, y0, z0) is the beginning corner of the partition.
                int x0 = index[0];
                int y0 = index[1];
                int z0 = index[2];
                float e000 = map[rec.index(new int[] { x0, y0, z0 })];

                // The derivative of probe atoms can be precalculated at the cost of massive memory storage.
                float e100 = map[rec.index(new int[] { x0 + 1, y0, z0 })];
                float e010 = map[rec.index(new int[] { x0, y0 + 1, z0 })];
                float e001 = map[rec.index(new int[] { x0, y0, z0 + 1 })];
                float fx = (e100 - e000) * rec.granularity_inverse;
                float fy = (e010 - e000) * rec.granularity_inverse;
                float fz = (e001 - e000) * rec.granularity_inverse;
                deri[i] = new Vec3(fx, fy, fz);

                e += e000; // Aggregate the energy.
            }

            // Save inter-molecular free energy into f.
            f = e;

            // Calculate intra-ligand free energy.
            int num_interacting_pairs = interacting_pairs.Count;
            for (int i = 0; i < num_interacting_pairs; ++i)
            {
                interacting_pair p = interacting_pairs[i];
                Vec3 r = coor[p.i1] - coor[p.i0];
                float r2 = IMath.norm_sqr(r);
                if (r2 < scoring_function.cutoff_sqr)
                {
                    int nsr2 = (int)(scoring_function.ns * r2);
                    e += sf.ee[p.p_offset][nsr2];
                    Vec3 d = sf.dd[p.p_offset][nsr2] * r;
                    deri[p.i0] -= d;
                    deri[p.i1] += d;
                }
            }

            // If the free energy is no better than the upper bound, refuse this conformation.
            if (e >= e_upper_bound)
                return false;

            // Calculate and aggregate the force and torque of BRANCH frames to their parent frame.
            for (int k = num_frames - 1, t = 6 + num_active_torsions; k > 0; --k)
            {
                frame ff = frames[k];

                for (int i = ff.habegin; i < ff.haend; ++i)
                {
                    // The deri with respect to the position, orientation, and torsions
                    // would be the negative total force acting on the ligand,
                    // the negative total torque, and the negative torque projections, respectively,
                    // where the projections refer to the torque applied to the branch moved by the torsion,
                    // projected on its rotation axis.
                    forc[k] += deri[i];
                    torq[k] += IMath.cross(coor[i] - orig[k], deri[i]);
                }

                // Aggregate the force and torque of current frame to its parent frame.
                forc[ff.parent] += forc[k];
                torq[ff.parent] += torq[k] + IMath.cross(orig[k] - orig[ff.parent], forc[k]);

                // If the current BRANCH frame does not have an active torsion, skip it.
                if (!ff.active) continue;

                // Save the torsion.
                g[--t] = IMath.dot(torq[k], axes[k]); // dot product
            }

            // Calculate and aggregate the force and torque of ROOT frame.
            for (int i = root.habegin; i < root.haend; ++i)
            {
                forc[0] += deri[i];
                torq[0] += IMath.cross(coor[i] - orig[0], deri[i]);
            }

            // Save the aggregated force and torque to g.
            g[0] = forc[0][0];
            g[1] = forc[0][1];
            g[2] = forc[0][2];
            g[3] = torq[0][0];
            g[4] = torq[0][1];
            g[5] = torq[0][2];

            return true;
        }

        public result compose_result(float e, float f, ref conformation conf, ref receptor rec)
        {
            List<Vec3> orig = new List<Vec3>();
            List<Quat4> oriq = new List<Quat4>();
            List<float[]> orim = new List<float[]>();
            if (num_frames > 0) orig.Capacity = num_frames;
            if (num_frames > 0) oriq.Capacity = num_frames;
            if (num_frames > 0) orim.Capacity = num_frames;
            for (int i = 0; i < num_frames; i++)
            {
                orig.Add(Vec3.zero);
                oriq.Add(new Quat4(0.0f,0.0f,0.0f,0.0f));
                orim.Add(new float[9]);
            }

            assert(num_heavy_atoms == this.heavy_atoms.Count);
            assert(num_hydrogens == this.hydrogens.Count);

            List<Vec3> heavy_atoms = new List<Vec3>();
            if (num_heavy_atoms > 0) heavy_atoms.Capacity = num_heavy_atoms;
            for (int i = 0; i < num_heavy_atoms; i++)
            {
                heavy_atoms.Add(Vec3.zero);
            }
            List<Vec3> hydrogens = new List<Vec3>();
            if (num_hydrogens>0) hydrogens.Capacity = num_hydrogens;
            for (int i = 0; i < num_hydrogens; i++)
            {
                hydrogens.Add(Vec3.zero);
            }

            orig[0] = conf.position;
            oriq[0] = conf.orientation;
            orim[0] = IMath.qtn4_to_mat3(conf.orientation);


            // Calculate the coor of both heavy atoms and hydrogens of ROOT frame.
            frame root = frames[0];
            for (int i = root.habegin; i < root.haend; ++i)
            {
                heavy_atoms[i] = orig[0] + IMath.mul(orim[0], this.heavy_atoms[i].coord);
            }
            for (int i = root.hybegin; i < root.hyend; ++i)
            {
                hydrogens[i] = orig[0] + IMath.mul(orim[0], this.hydrogens[i].coord);
            }

            // Calculate the coor of both heavy atoms and hydrogens of BRANCH frames.
            for (int k = 1, t = 0; k < num_frames; ++k)
            {
                frame ff = frames[k];

                // Update origin.
                orig[k] = orig[ff.parent] + IMath.mul(orim[ff.parent], ff.parent_rotorY_to_current_rotorY);

                // Update orientation.
                oriq[k] = IMath.mul( IMath.vec4_to_qtn4(IMath.mul(orim[ff.parent], ff.parent_rotorX_to_current_rotorY), ff.active ? conf.torsions[t++] : 0) , oriq[ff.parent]);
                orim[k] = IMath.qtn4_to_mat3(oriq[k]);

                // Update coor.
                for (int i = ff.habegin; i < ff.haend; ++i)
                {
                    heavy_atoms[i] = orig[k] + IMath.mul(orim[k], this.heavy_atoms[i].coord);
                }
                for (int i = ff.hybegin; i < ff.hyend; ++i)
                {
                    hydrogens[i] = orig[k] + IMath.mul(orim[k], this.hydrogens[i].coord);
                }
            }

            for (int i = 0; i < heavy_atoms.Count; i++)
            {
                float dis = IMath.distance(heavy_atoms[i], rec.center);
                assert(dis < 100.0f);
            }
            for (int i = 0; i < hydrogens.Count; i++)
            {
                float dis = IMath.distance(hydrogens[i], rec.center);
                assert(dis < 100.0f);
            }

            result r = new result(e, f, heavy_atoms, hydrogens);

            assert(r.heavy_atoms.Count == this.heavy_atoms.Count);
            assert(r.hydrogens.Count == this.hydrogens.Count);

            return r;
        }

        public float calculate_rf_score(ref result r, ref receptor rec, ref forest f)
        {
            float[] x = new float[tree.nv];
            for (int i = 0; i < num_heavy_atoms; ++i)
            {
                atom la = heavy_atoms[i];
                foreach (atom ra in rec.atoms)
                {
                    float ds = IMath.distance_sqr(r.heavy_atoms[i], ra.coord);
                    if (ds >= 144) continue; // RF-Score cutoff 12A
                    if (!la.rf_unsupported() && !ra.rf_unsupported())
                    {
                        ++x[(la.rf << 2) + ra.rf];
                    }
                    if (ds >= 64) continue; // Vina score cutoff 8A
                    if (!la.xs_unsupported() && !ra.xs_unsupported())
                    {
                        scoring_function.score(x, 36, la.xs, ra.xs, ds);
                    }
                }
            }
            x[x.Length - 1] = flexibility_penalty_factor;
            List<float> xx = new List<float>(x);
            float ret = f.PredictsY(ref xx);
            assert(!float.IsNaN(ret));
            return ret;
        }

        public void write_models(ref List<string> outputlines,   ref List<result> results, int num_results, ref receptor rec) 
        {
            if (num_results < 0) num_results = results.Count;
            if (num_results > results.Count) num_results = results.Count;
            assert(num_results!=0);

            string newline="";

            for (int k = 0; k < num_results; ++k)
            {
                result r = results[k];
                //ofs << "MODEL     " << setw(4) << (k + 1) << '\n' << setprecision(2)
                //    << "REMARK 921   NORMALIZED FREE ENERGY PREDICTED BY IDOCK:" << setw(8) << r.e_nd    << " KCAL/MOL\n"
                //    << "REMARK 922        TOTAL FREE ENERGY PREDICTED BY IDOCK:" << setw(8) << r.e       << " KCAL/MOL\n"
                //    << "REMARK 923 INTER-LIGAND FREE ENERGY PREDICTED BY IDOCK:" << setw(8) << r.f       << " KCAL/MOL\n"
                //    << "REMARK 924 INTRA-LIGAND FREE ENERGY PREDICTED BY IDOCK:" << setw(8) << (r.e - r.f) << " KCAL/MOL\n"
                //    << "REMARK 927      BINDING AFFINITY PREDICTED BY RF-SCORE:" << setw(8) << r.rf      << " PKD\n" << setprecision(3);
                newline = "MODEL     " + string.Format("{0,4}", (k + 1).ToString());
                outputlines.Add(newline);
                newline = "REMARK 921   NORMALIZED FREE ENERGY PREDICTED BY IDOCK:" + string.Format("{0,8}", r.e_nd.ToString()) + " KCAL/MOL";
                outputlines.Add(newline);
                newline = "REMARK 922        TOTAL FREE ENERGY PREDICTED BY IDOCK:" + string.Format("{0,8}", r.e.ToString()) + " KCAL/MOL";
                outputlines.Add(newline);
                newline = "REMARK 923 INTER-LIGAND FREE ENERGY PREDICTED BY IDOCK:" + string.Format("{0,8}", r.f.ToString()) + " KCAL/MOL";
                outputlines.Add(newline);
                newline = "REMARK 924 INTRA-LIGAND FREE ENERGY PREDICTED BY IDOCK:" + string.Format("{0,8}", (r.e - r.f).ToString()) + " KCAL/MOL";
                outputlines.Add(newline);
                assert(!float.IsNaN(r.rf));
                newline = "REMARK 927      BINDING AFFINITY PREDICTED BY RF-SCORE:" + string.Format("{0,8}", r.rf.ToString()) + " PKD";
                outputlines.Add(newline);

                int heavy_atom = 0;
                int hydrogen = 0;
                foreach (string line in lines)
                {
                    if (line.Length >= 78) // This line starts with "ATOM" or "HETATM".
                    {
                        bool isH = (line[77] == 'H');
                        float free_energy = 0.0f;
                        if (!isH)
                        {
                            int nxs=heavy_atoms[heavy_atom].xs;
                            int zyx_offset = rec.index(rec.index(r.heavy_atoms[heavy_atom]));
                            List<float> recmap=rec.maps[nxs];
                            if (zyx_offset >= 0 && zyx_offset < recmap.Count)
                                free_energy = recmap[zyx_offset];
                        }
                        Vec3 coordinate = isH ? r.hydrogens[hydrogen++] : r.heavy_atoms[heavy_atom++];
                        //ofs << line.substr(0, 30)
                        //    << setw(8) << coordinate[0]
                        //    << setw(8) << coordinate[1]
                        //    << setw(8) << coordinate[2]
                        //    << line.substr(54, 16)
                        //    << setw(6) << free_energy
                        //    << line.substr(76);
                        string coordstr = string.Format("{0,8}{1,8}{2,8}", coordinate.x.ToString("F3"), coordinate.y.ToString("F3"), coordinate.z.ToString("F3"));
                        string free_energystr = string.Format("{0,6}", free_energy.ToString("F3"));
                        newline = line.Substring(0, 30) + coordstr +
                            line.Substring(54, 16)+free_energystr+line.Substring(76);
                    }
                    else // This line starts with "ROOT", "ENDROOT", "BRANCH", "ENDBRANCH", TORSDOF", which will not change during docking.
                    {
                        newline = line;
                        //ofs << line;
                    }
                    //ofs << '\n';
                    outputlines.Add(newline);
                }
                outputlines.Add("ENDMDL");
                //ofs << "ENDMDL\n";

            }

        }

        public void monte_carlo(ref List<result> results, int seed, ref scoring_function sf, ref receptor rec)
        {
            // Define constants.
            float pi = 3.1415926535897932f; //!< Pi.
            int num_alphas = 5; //!< Number of alpha values for determining step size in BFGS
            int num_mc_iterations = 10 * num_heavy_atoms; //!< The number of iterations correlates to the complexity of ligand.
            int num_entities = 2 + num_active_torsions; // Number of entities to mutate.
            int num_variables = 6 + num_active_torsions; // Number of variables to optimize.
            float e_upper_bound = 4.0f * num_heavy_atoms; // A conformation will be droped if its free energy is not better than e_upper_bound.
            float required_square_error = 1.0f * num_heavy_atoms; // Ligands with RMSD < 1.0 will be clustered into the same cluster.

            mt19937_64 rng = new mt19937_64(seed);
            uniform_real_distribution u01 = new uniform_real_distribution(0, 1);
            uniform_real_distribution u11 = new uniform_real_distribution(-1, 1);
            uniform_real_distribution upi = new uniform_real_distribution(-pi, pi);
            uniform_real_distribution ub0 = new uniform_real_distribution(rec.corner0[0], rec.corner1[0]);
            uniform_real_distribution ub1 = new uniform_real_distribution(rec.corner0[1], rec.corner1[1]);
            uniform_real_distribution ub2 = new uniform_real_distribution(rec.corner0[2], rec.corner1[2]);
            uniform_real_distribution uen = new uniform_real_distribution(0, num_entities - 1);
            normal_distribution n01 = new normal_distribution(0, 1);

            // Generate an initial random conformation c0, and evaluate it.
            conformation c0 = new conformation(num_active_torsions);
            float e0 = 0.0f, f0 = 0.0f;
            change g0 = new change(num_active_torsions);
            bool valid_conformation = false;
            for (int i = 0; i < 1000 ; i++)
            {
                // Randomize conformation c0.
                c0.position = new Vec3(ub0.GetRandomValue(rng), ub1.GetRandomValue(rng), ub2.GetRandomValue(rng));
                c0.orientation = new Quat4(n01.GetRandomValue(rng), n01.GetRandomValue(rng), n01.GetRandomValue(rng), n01.GetRandomValue(rng));
                c0.orientation = IMath.normalize(c0.orientation);
                for (int ii = 0; ii < num_active_torsions; ii++)
                {
                    c0.torsions[ii] = upi.GetRandomValue(rng);
                }
                valid_conformation = evaluate(ref c0, ref sf, ref rec, e_upper_bound, ref e0, ref f0, ref g0);
                if (valid_conformation)
                    break;
            }
            if (!valid_conformation)
                return;
            float best_e = e0; // The best free energy so far.

            // Initialize necessary variables for BFGS.
            conformation c1 = new conformation(num_active_torsions);
            conformation c2 = new conformation(num_active_torsions); // c2 = c1 + ap.
            float e1 = 0.0f, f1 = 0.0f, e2 = 0.0f, f2 = 0.0f;
            change g1 = new change(num_active_torsions);
            change g2 = new change(num_active_torsions);
            change p = new change(num_active_torsions); // Descent direction.
            float alpha, pg1, pg2; // pg1 = p * g1. pg2 = p * g2.
            int num_alpha_trials;

            // Initialize the inverse Hessian matrix to identity matrix.
            // An easier option that works fine in practice is to use a scalar multiple of the identity matrix,
            // where the scaling factor is chosen to be in the range of the eigenvalues of the true Hessian.
            // See N&R for a recipe to find this initializer.
            int h1count=num_variables * (num_variables + 1) >> 1;
            float[] h1 = new float[h1count]; // Symmetric triangular matrix.
            for (int i = 0; i < num_variables; ++i)
                h1[IMath.mr(i, i)] = 1;
            float[] h = new float[h1count];

            // Initialize necessary variables for updating the Hessian matrix h.
            change y = new change(num_active_torsions); // y = g2 - g1.
            change mhy = new change(num_active_torsions); // mhy = -h * y.
            float yhy, yp, ryp, pco;

            for (int mc_i = 0; mc_i < num_mc_iterations; ++mc_i)
            {
                int mutation_entity = 0;

                // Mutate c0 into c1, and evaluate c1.
                //do
                int times = 0;
                while (true)
                {
                    // Make a copy, so the previous conformation is retained.
                    c1 = c0;

                    // Determine an entity to mutate.
                    mutation_entity = (int)uen.GetRandomValue(rng);
                    assert(mutation_entity < num_entities);
                    if (mutation_entity < num_active_torsions) // Mutate an active torsion.
                    {
                        c1.torsions[mutation_entity] = upi.GetRandomValue(rng);
                    }
                    else if (mutation_entity == num_active_torsions) // Mutate position.
                    {
                        c1.position += new Vec3(u11.GetRandomValue(rng), u11.GetRandomValue(rng), u11.GetRandomValue(rng));
                    }
                    else // Mutate orientation.
                    {
                        c1.orientation = IMath.mul(IMath.vec3_to_qtn4(0.01f * new Vec3(u11.GetRandomValue(rng), u11.GetRandomValue(rng), u11.GetRandomValue(rng))), c1.orientation);
                        assert(IMath.normalized(c1.orientation));
                    }

                    float distance = IMath.distance(c1.position, rec.center);
                    if (distance > IMath.norm(rec.size))
                        c1.position = rec.center;

                    if (evaluate(ref c1, ref sf, ref rec, e_upper_bound, ref e1, ref f1, ref g1))
                        break;

                    times++;
                    if (times > 1000)
                        break;
                }
                //while (!evaluate(c1, sf, rec, e_upper_bound, e1, f1, g1));



                // Initialize the Hessian matrix to identity.
                for (int k = 0; k < h1count; k++)
                    h[k] = h1[k];

                // Given the mutated conformation c1, use BFGS to find a local minimum.
                // The conformation of the local minimum is saved to c2, and its derivative is saved to g2.
                // http://en.wikipedia.org/wiki/BFGS_method
                // http://en.wikipedia.org/wiki/Quasi-Newton_method
                // The loop breaks when an appropriate alpha cannot be found.
                while (true)
                {
                    // Calculate p = -h*g, where p is for descent direction, h for Hessian, and g for gradient.
                    for (int i = 0; i < num_variables; ++i)
                    {
                        float sum = 0;
                        for (int j = 0; j < num_variables; ++j)
                            sum += h[IMath.mp(i, j)] * g1[j];
                        p[i] = -sum;
                    }

                    // Calculate pg = p*g = -h*g^2 < 0
                    pg1 = 0;
                    for (int i = 0; i < num_variables; ++i)
                        pg1 += p[i] * g1[i];

                    // Perform a line search to find an appropriate alpha.
                    // Try different alpha values for num_alphas times.
                    // alpha starts with 1, and shrinks to alpha_factor of itself iteration by iteration.
                    alpha = 1.0f;
                    for (num_alpha_trials = 0; num_alpha_trials < num_alphas; ++num_alpha_trials)
                    {
                        // Obtain alpha from the precalculated alpha values.
                        alpha *= 0.1f;

                        // Calculate c2 = c1 + ap.
                        c2.position = c1.position + alpha * new Vec3(p[0], p[1], p[2]);
                        assert(IMath.normalized(c1.orientation));
                        c2.orientation = IMath.mul(IMath.vec3_to_qtn4(alpha * new Vec3(p[3], p[4], p[5])), c1.orientation);
                        assert(IMath.normalized(c2.orientation));
                        for (int i = 0; i < num_active_torsions; ++i)
                        {
                            c2.torsions[i] = c1.torsions[i] + alpha * p[6 + i];
                        }

                        // Evaluate c2, subject to Wolfe conditions http://en.wikipedia.org/wiki/Wolfe_conditions
                        // 1) Armijo rule ensures that the step length alpha decreases f sufficiently.
                        // 2) The curvature condition ensures that the slope has been reduced sufficiently.
                        if (evaluate(ref c2, ref sf, ref rec, e1 + 0.0001f * alpha * pg1, ref e2, ref f2, ref g2))
                        {
                            pg2 = 0;
                            for (int i = 0; i < num_variables; ++i)
                                pg2 += p[i] * g2[i];
                            if (pg2 >= 0.9 * pg1)
                                break; // An appropriate alpha is found.
                        }
                    }

                    // If an appropriate alpha cannot be found, exit the BFGS loop.
                    if (num_alpha_trials == num_alphas)
                        break;

                    // Update Hessian matrix h.
                    for (int i = 0; i < num_variables; ++i) // Calculate y = g2 - g1.
                        y[i] = g2[i] - g1[i];
                    for (int i = 0; i < num_variables; ++i) // Calculate mhy = -h * y.
                    {
                        float sum = 0;
                        for (int j = 0; j < num_variables; ++j)
                            sum += h[IMath.mp(i, j)] * y[j];
                        mhy[i] = -sum;
                    }
                    yhy = 0;
                    for (int i = 0; i < num_variables; ++i) // Calculate yhy = -y * mhy = -y * (-hy).
                        yhy -= y[i] * mhy[i];
                    yp = 0;
                    for (int i = 0; i < num_variables; ++i) // Calculate yp = y * p.
                        yp += y[i] * p[i];
                    ryp = 1 / yp;

                    pco = ryp * (ryp * yhy + alpha);
                    for (int i = 0; i < num_variables; ++i)
                    {
                        for (int j = i; j < num_variables; ++j) // includes i
                        {
                            h[IMath.mr(i, j)] += ryp * (mhy[i] * p[j] + mhy[j] * p[i]) + pco * p[i] * p[j];
                        }
                    }

                    assert(!float.IsNaN(h[0]));

                    // Move to the next iteration.
                    c1 = c2;
                    e1 = e2;
                    f1 = f2;
                    g1 = g2;

                    c2 = new conformation(num_active_torsions);
                    g2 = new change(num_active_torsions);

                }

             
                // Accept c1 according to Metropolis criteria.
                float delta = e0 - e1;
                if (delta > 0 || u01.GetRandomValue(rng) < IMath.exp(delta))
                {
                    // best_e is the best energy of all the conformations in the container.
                    // e1 will be saved if and only if it is even better than the best one.
                    //|| results.Count < results.Capacity
                    if (e1 < best_e)
                    {
                        result.push(results, compose_result(e1, f1, ref c1, ref rec), required_square_error);
                        //if (e1 < best_e) best_e = e0;
                        best_e = e1;
                    }

                    // Save c1 into c0.
                    c0 = c1;
                    e0 = e1;
                }
            }
        }


    };


}
