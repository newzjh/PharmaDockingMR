using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;

namespace idock
{
    public class ResultComparer : IComparer<result>
    {
        public ResultComparer()
        {
        }

        public int Compare(result val1, result val2)
        {
            if (val1.e > val2.e)
                return 1;
            else if (val1.e < val2.e)
                return -1;
            return 0;
        }
    }

    //! Represents a result found by BFGS local optimization for later clustering.
    public class result
    {
        public float e; //!< Free energy.
        public float f; //!< Inter-molecular free energy.
        public float e_nd; //!< Normalized free energy.
        public float rf; //!< RF-Score binding affinity.
        public List<Vec3> heavy_atoms; //!< Heavy atom coordinates.
        public List<Vec3> hydrogens; //!< Hydrogen atom coordinates.

        //! Constructs a result from free energy e, force f, heavy atom coordinates and hydrogen atom coordinates.
        public result(float e_, float f_,  List<Vec3> heavy_atoms_,  List<Vec3> hydrogens_)
        //: e(e), f(f), heavy_atoms(move(heavy_atoms_)), hydrogens(move(hydrogens_)) {}
        {
            this.e = e_;
            this.f = f_;
            this.heavy_atoms = heavy_atoms_;
            this.hydrogens = hydrogens_;
        }



        //result(const result&) = default;
        //result(result&&) = default;
        //result& operator=(const result&) = default;
        //result& operator=(result&&) = default;

        //! For sorting vector<result>.
        //public bool operator<(const result& r) const
        //{
        //    return e < r.e;
        //}



        //! Clusters a result into an existing result set with a minimum RMSD requirement.
        public static void push( List<result> results,  result r, float required_square_error)
        {
            // If this is the first result, simply save it.
            if (results.Count <= 0)
            {
                results.Add(r);
                return;
            }

            Kernel.assert(r.heavy_atoms.Count == results[0].heavy_atoms.Count);

            // If the container is not empty, find a result to which r is the closest.
            int index = 0;
            float best_square_error = IMath.distance_sqr(r.heavy_atoms, results[0].heavy_atoms);
            for (int i = 1; i < results.Count; ++i)
            {
                float this_square_error = IMath.distance_sqr(r.heavy_atoms, results[i].heavy_atoms);
                if (this_square_error < best_square_error)
                {
                    index = i;
                    best_square_error = this_square_error;
                }
            }

            // Now r is the closest to results[index]. Check if they are in the same cluster.
            if (best_square_error < required_square_error)
            {
                // They are in the same cluster and r is better than results[index], so substitute r for results[index].
                if (r.e < results[index].e)
                {
                    results[index] = r;
                }
            }
            else // They are not in the same cluster, i.e. r itself forms a new cluster.
            {
                // Save this new cluster if the result container is not full yet.
                if (results.Count < results.Capacity)
                {
                    results.Add(r);
                }
                else // Now the container is full.
                {
                    // If r is better than the worst result, then substitute r for it.
                    if (r.e < results[results.Count - 1].e)
                    {
                        results[results.Count - 1] = r;
                    }
                }
            }

            //results.Sort();
            //sort(results.begin(), results.end());
        }
    };

}