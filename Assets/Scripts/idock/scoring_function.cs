using System;
using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;

namespace idock
{

    //! Represents the scoring function used in idock.
    public class scoring_function
    {

        public const int n = 15; //!< Number of XScore atom types.
        public const int np = n * (n + 1) >> 1; //!< Number of XScore atom type pairs.
        public const int ns = 1024; //!< Number of samples in a unit distance.
        public const int cutoff = 8; //!< Atom type pair distance cutoff.
        public const int nr = ns * cutoff * cutoff + 1; //!< Number of samples within the entire cutoff.
        public const int ne = nr * np; //!< Number of values to precalculate.
        public const float cutoff_sqr = cutoff * cutoff; //!< Cutoff square.



        public List<List<float>> ee = new List<List<float>>(); //!< Scoring function values.
        public List<List<float>> dd = new List<List<float>>(); //!< Scoring function derivatives divided by distance.

        //const array<float, n> vdw; //!< Van der Waals distances for XScore atom types.
        private static float[] vdw =
            {
	1.9f, //   C_H
	1.9f, //   C_P
	1.8f, //   N_P
	1.8f, //   N_D
	1.8f, //   N_A
	1.8f, //   N_DA
	1.7f, //   O_A
	1.7f, //   O_DA
	2.0f, //   S_P
	2.1f, //   P_P
	1.5f, //   F_H
	1.8f, //  Cl_H
	2.0f, //  Br_H
	2.2f, //   I_H
	1.2f, // Met_D
};

        private float[] rs; //!< Distance samples.

        private static void assert(bool b)
        {
            Kernel.assert(b);
        }

        //! Returns true if the XScore atom type is hydrophobic.
        private static bool is_hydrophobic(int t)
        {
            return t == 0 || t == 10 || t == 11 || t == 12 || t == 13;
        }

        //! Returns true if the two XScore atom types are both hydrophobic.
        private static bool is_hydrophobic(int t0, int t1)
        {
            return is_hydrophobic(t0) && is_hydrophobic(t1);
        }

        //! Returns true if the XScore atom type is a hydrogen bond donor.
        private static bool is_hbdonor(int t)
        {
            return t == 3 || t == 5 || t == 7 || t == 14;
        }

        //! Returns true if the XScore atom type is a hydrogen bond acceptor.
        private static bool is_hbacceptor(int t)
        {
            return t == 4 || t == 5 || t == 6 || t == 7;
        }

        //! Returns true if the two XScore atom types are a pair of hydrogen bond donor and acceptor.
        private static bool is_hbond(int t0, int t1)
        {
            return (is_hbdonor(t0) && is_hbacceptor(t1)) || (is_hbdonor(t1) && is_hbacceptor(t0));
        }

        //! Constructs an empty scoring function.
        public scoring_function()
        {
            ee.Capacity = np;
            dd.Capacity = np;
            for (int i = 0; i < np; i++)
            {
                List<float> ep = new List<float>(nr);
                List<float> dp = new List<float>(nr);
                for (int j = 0; j < nr; j++)
                {
                    ep.Add(0.0f);
                    dp.Add(0.0f);
                }
                ee.Add(ep);
                dd.Add(dp);
            }
            rs = new float[nr];
 
            float ns_inv = 1.0f / ns;
            for (int i = 0; i < nr; ++i)
            {
                rs[i] = IMath.sqrt(i * ns_inv);
            }

            assert(rs[0] == 0);
            assert(rs[rs.Length-1] == cutoff);
        }

        public double score(int t0, int t1, float r)
        {
	        assert(r <= cutoff);

	        // Calculate the surface distance d.
	        float d = r - (vdw[t0] + vdw[t1]);

	        // The scoring function is a weighted sum of 5 terms.
	        // The first 3 terms depend on d only, while the latter 2 terms depend on t0, t1 and d.
	        return (-0.035579) * IMath.exp(-4 * d * d)
		        +  (-0.005156) * IMath.exp(-0.25f * (d - 3.0f) * (d - 3.0f))
		        +  ( 0.840245) * (d > 0 ? 0.0 : d * d)
		        +  (-0.035069) * ((is_hydrophobic(t0) && is_hydrophobic(t1)) ? ((d >= 1.5) ? 0.0 : ((d <= 0.5) ? 1.0 : 1.5 - d)) : 0.0)
		        +  (-0.587439) * ((is_hbond(t0, t1)) ? ((d >= 0) ? 0.0 : ((d <= -0.7) ? 1 : d * (-1.4285714285714286))): 0.0);
        }

        //! Aggregates the five term values evaluated at (t0, t1, r2).
        public static void score(float[] vv, int offset, int t0, int t1, float r2)
        {
            assert(r2 <= cutoff_sqr);

            float d = IMath.sqrt(r2) - (vdw[t0] + vdw[t1]);
            vv[offset + 0] += IMath.exp(-4.0f * d * d);
            vv[offset + 1] += IMath.exp(-0.25f * (d - 3.0f) * (d - 3.0f));
            vv[offset + 2] += d < 0.0f ? d * d : 0.0f;
            vv[offset + 3] += is_hydrophobic(t0, t1) ? (d >= 1.5f ? 0.0f : (d <= 0.5f ? 1.0f : 1.5f - d)) : 0.0f;
            vv[offset + 4] += is_hbond(t0, t1) ? (d >= 0.0f ? 0.0f : (d <= -0.7f ? 1.0f : d * -1.4285714285714286f)) : 0.0f;
        }


        ////! Precalculates the scoring function values of sample points for the type combination of t0 and t1.
        //public void precalculate(int t0, int t1)
        //{
        //    assert(t0 <= t1);
        //    int offset = nr * ((t1 * (t1 + 1) >> 1) + t0);
        //    float s = vdw[t0] + vdw[t1];
        //    bool hydrophobic = is_hydrophobic(t0, t1);
        //    bool hbond = is_hbond(t0, t1);

        //    // Evaluate the scoring function value at (t0, t1, r).
        //    float[] et = Kernel.GetPointer<float, float>(ee.ToArray(), offset); // e.data() + offset;
        //    for (int i = 0; i < nr; ++i)
        //    {
        //        // Calculate the surface distance d.
        //        float d = rs[i] - s;

        //        // The scoring function is a weighted sum of 5 terms. The first 3 terms depend on d only, while the latter 2 terms depend on t0, t1 and d.
        //        et[i] =
        //            (-0.035579f) * exp(-4.0f * d * d)
        //          + (-0.005156f) * exp(-0.25f * (d - 3.0f) * (d - 3.0f))
        //          + (d < 0.0f ? 0.840245f * d * d : 0.0f)
        //          + (hydrophobic ? (-0.035069f) * (d >= 1.5f ? 0.0f : (d <= 0.5f ? 1.0f : 1.5f - d)) : 0.0f)
        //          + (hbond ? (-0.587439f) * (d >= 0.0f ? 0.0f : (d <= -0.7f ? 1.0f : d * -1.4285714285714286f)) : 0.0f);
        //    }

        //    // Evaluate the scoring function derivative divided by distance at (t0, t1, r).
        //    float[] dt = Kernel.GetPointer<float, float>(dd.ToArray(), offset); //d.data() + offset;
        //    for (int i = 0; i < nr - 1; ++i)
        //    {
        //        dt[i] = (et[i + 1] - et[i]) / ((rs[i + 1] - rs[i]) * rs[i]);
        //    }
        //}

        public void precalculate(int t0, int t1)
        {
	        int p = IMath.mr(t0, t1);
	        List<float> ep = ee[p];
            List<float> dp = dd[p];
	        assert(ep.Count == nr);
	        assert(dp.Count == nr);

	        // Calculate the value of scoring function evaluated at (t0, t1, d).
	        for (int i = 0; i < nr; ++i)
	        {
		        ep[i] = (float)score(t0, t1, rs[i]);
	        }

	        // Calculate the dor of scoring function evaluated at (t0, t1, d).
	        for (int i = 1; i < nr - 1; ++i)
	        {
		        dp[i] = (ep[i + 1] - ep[i]) / ((rs[i + 1] - rs[i]) * rs[i]);
	        }
	        dp[0] = 0;
	        dp[dp.Count-1] = 0;
        }


        //! Clears precalculated values.
        public void clear()
        {
            rs=null;
        }


    }

}
