using System;
using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;

namespace idock
{

    //! Represents a solution found by BFGS local optimization for later clustering.
    public class solution
    {
        //	float e; //!< Free energy.
        //	vector<float> x; //!< Conformation vector.
        public Quat4[] q; //!< Frame quaternions.
        public Vec3[] c; //!< Heavy atom coordinates.
    };

    public class mt19937_64
    {
        public int seed;
        public Random rnd=new Random();
        public mt19937_64(int seed_)
        {
            this.seed = seed_;
        }
    }



    public class uniform_real_distribution
    {
        private float mmin;
        private float mmax;


        public uniform_real_distribution(float rmin_, float rmax_)
        {
            mmin = rmin_;
            mmax = rmax_;

            //Random.Range(mmin,mmax)
        }

        public float GetRandomValue(mt19937_64 rng)
        {
            //return UnityEngine.Random.Range(mmin, mmax);
            
            return mmin + (float)rng.rnd.NextDouble() * (mmax - mmin);
        }

    }

    public class normal_distribution
    {
        private float mean;
        private float std;

        private double PI = 3.1415926;

        public normal_distribution(float mean_, float std_)
        {
            mean = mean_;
            std = std_;
        }

        public float GetRandomValue(mt19937_64 rng)
        {
            //return UnityEngine.Random.Range(mmin, mmax);
            double u_1 = rng.rnd.NextDouble();
            double u_2 = rng.rnd.NextDouble();
            double R = Math.Sqrt((-2) * Math.Log(u_2));
            double theta = 2 * PI * u_1;
            return (float)(mean + (R * Math.Sin(theta)) * std);
        }

    }

    

    public class Kernel
    {
        public static void assert(bool b)
        {
            if (!b)
            {
                bool q = true;
            }
        }

        public static T1[] GetPointer<T1, T2>(T2[] inputarray, int offset)
        {
            //得到结构体的大小
            int deststride = Marshal.SizeOf(typeof(T1));
            int srcstride = Marshal.SizeOf(typeof(T2));

            int srcsize = inputarray.Length - offset;
            if (srcsize < 0) srcsize = 0;
            int destsize = srcsize * srcstride / deststride;
            if (destsize < 0) destsize = 0;

            T1[] array = new T1[destsize];

            //分配结构体大小的内存空间
            IntPtr tempPtr = Marshal.AllocHGlobal(destsize * deststride);

            if (typeof(T2) == typeof(float))
            {
                Marshal.Copy((float[])(object)inputarray, offset, tempPtr, srcsize);
            }
            else if (typeof(T2) == typeof(Int16))
            {
                Marshal.Copy((Int16[])(object)inputarray, offset, tempPtr, srcsize);
            }
            else if (typeof(T2) == typeof(Int32))
            {
                Marshal.Copy((Int32[])(object)inputarray, offset, tempPtr, srcsize);
            }
            else if (typeof(T2) == typeof(Int64))
            {
                Marshal.Copy((Int64[])(object)inputarray, offset, tempPtr, srcsize);
            }
            else if (typeof(T2) == typeof(byte))
            {
                Marshal.Copy((byte[])(object)inputarray, offset, tempPtr, srcsize);
            }
            else if (typeof(T2) == typeof(char))
            {
                Marshal.Copy((char[])(object)inputarray, offset, tempPtr, srcsize);
            }
            else if (typeof(T2) == typeof(double))
            {
                Marshal.Copy((double[])(object)inputarray, offset, tempPtr, srcsize);
            }

            ////将结构体拷到分配好的内存空间
            //Marshal.StructureToPtr(inputarray, destPtr, false);

            ////从内存空间拷到byte数组
            ////Marshal.Copy(structPtr, bytes, 0, size);
            //Marshal.PtrToStructure(destPtr, array);

            if (typeof(T1) == typeof(float))
            {
                Marshal.Copy(tempPtr, (float[])(object)array, 0, destsize);
            }
            else if (typeof(T1) == typeof(Int16))
            {
                Marshal.Copy(tempPtr, (Int16[])(object)array, 0, destsize);
            }
            else if (typeof(T1) == typeof(Int32))
            {
                Marshal.Copy(tempPtr, (Int32[])(object)array, 0, destsize);
            }
            else if (typeof(T1) == typeof(Int64))
            {
                Marshal.Copy(tempPtr, (Int64[])(object)array, 0, destsize);
            }
            else if (typeof(T1) == typeof(byte))
            {
                Marshal.Copy(tempPtr, (byte[])(object)array, 0, destsize);
            }
            else if (typeof(T1) == typeof(char))
            {
                Marshal.Copy(tempPtr, (char[])(object)array, 0, destsize);
            }
            else if (typeof(T1) == typeof(double))
            {
                Marshal.Copy(tempPtr, (double[])(object)array, 0, destsize);
            }

            //释放内存空间
            Marshal.FreeHGlobal(tempPtr);

            //返回byte数组
            return array;

        }

        public static bool evaluate(float[] e, float[] g, float[] a, float[] q,
            float[] c, float[] d, float[] f, float[] t, float[] x, int nf, int na, int np,
            float eub, int[] shared, float[] sfe, float[] sfd, int sfs,
            Vec3 cr0, Vec3 cr1, int[] npr, float gri, List<List<float>> mps, int gid, int gds)
        {
            int gd3 = 3 * gds;
            int gd4 = 4 * gds;

            int[] act = shared;

            int[] beg = GetPointer<int, int>(act, nf); //&act[nf];
            int[] end = GetPointer<int, int>(beg, nf);// &beg[nf];
            int[] nbr = GetPointer<int, int>(end, nf); //&end[nf];
            int[] prn = GetPointer<int, int>(nbr, nf); // &nbr[nf];
            float[] yy0 = GetPointer<float, int>(prn, nf); //(float*)&prn[nf];
            float[] yy1 = GetPointer<float, float>(yy0, nf); //&yy0[nf];
            float[] yy2 = GetPointer<float, float>(yy1, nf); //&yy1[nf];
            float[] xy0 = GetPointer<float, float>(yy2, nf); //&yy2[nf];
            float[] xy1 = GetPointer<float, float>(xy0, nf); //&xy0[nf];
            float[] xy2 = GetPointer<float, float>(xy1, nf); //&xy1[nf];
            int[] brs = GetPointer<int, float>(xy2, nf); //(int*)&xy2[nf];
            float[] co0 = GetPointer<float, int>(brs, nf - 1); //(float*)&brs[nf - 1];
            float[] co1 = GetPointer<float, float>(co0, na);// &co0[na];
            float[] co2 = GetPointer<float, float>(co1, na); //&co1[na];
            int[] xst = GetPointer<int, float>(co2, na); //(int*)&co2[na];
            int[] ip0 = GetPointer<int, int>(xst, na); //&xst[na];
            int[] ip1 = GetPointer<int, int>(ip0, np);// &ip0[np];
            int[] ipp = GetPointer<int, int>(ip1, np); //&ip1[np];

            float y, y0, y1, y2, v0, v1, v2, c0, c1, c2, e000, e100, e010, e001, a0, a1, a2, ang, sng, r0, r1, r2, r3, vs, dr, f0, f1, f2, t0, t1, t2, d0, d1, d2;
            float q0, q1, q2, q3, q00, q01, q02, q03, q11, q12, q13, q22, q23, q33, m0, m1, m2, m3, m4, m5, m6, m7, m8;
            int i, j, k, b, w, i0, i1, i2, k0, k1, k2, z;
            float[] map;

            t0 = 0;
            t1 = 0;
            t2 = 0;
            q3 = 0;
            q2 = 0;
            q1 = 0;
            q0 = 0;
            m8 = 0;
            m7 = 0;
            m8 = 0;
            m6 = 0;
            m5 = 0;
            m4 = 0;
            m3 = 0;
            m2 = 0;
            m1 = 0;
            m0 = 0;
            f2 = 0;
            f1 = 0;
            f0 = 0;

            // Apply position, orientation and torsions.
            c[i = gid] = x[k = gid];
            c[i += gds] = x[k += gds];
            c[i += gds] = x[k += gds];
            q[i = gid] = x[k += gds];
            q[i += gds] = x[k += gds];
            q[i += gds] = x[k += gds];
            q[i += gds] = x[k += gds];
            y = 0.0f;
            for (k = 0, b = 0, w = 6 * gds + gid; k < nf; ++k)
            {
                // Load rotorY from memory into registers.
                i0 = beg[k] * gd3;
                i0 += gds;
                y0 = c[i0];
                i0 += gds;
                y1 = c[i0];
                i0 += gds;
                y2 = c[i0];

                // Translate orientation of active frames from Quat4 into 3x3 matrix.
                if (act[k] != 0)
                {
                    q0 = q[k0 = k * gd4 + gid];
                    q1 = q[k0 += gds];
                    q2 = q[k0 += gds];
                    q3 = q[k0 += gds];
                    assert(IMath.fabs(q0 * q0 + q1 * q1 + q2 * q2 + q3 * q3 - 1.0f) < 0.0001f);
                    q00 = q0 * q0;
                    q01 = q0 * q1;
                    q02 = q0 * q2;
                    q03 = q0 * q3;
                    q11 = q1 * q1;
                    q12 = q1 * q2;
                    q13 = q1 * q3;
                    q22 = q2 * q2;
                    q23 = q2 * q3;
                    q33 = q3 * q3;
                    m0 = q00 + q11 - q22 - q33;
                    m1 = 2 * (q12 - q03);
                    m2 = 2 * (q02 + q13);
                    m3 = 2 * (q03 + q12);
                    m4 = q00 - q11 + q22 - q33;
                    m5 = 2 * (q23 - q01);
                    m6 = 2 * (q13 - q02);
                    m7 = 2 * (q01 + q23);
                    m8 = q00 - q11 - q22 + q33;
                }

                // Evaluate c and d of frame atoms. Aggregate e into y.
                for (i = beg[k], z = end[k]; i < z; ++i)
                {
                    i0 = i * gd3 + gid;
                    i1 = i0 + gds;
                    i2 = i1 + gds;

                    // The first atom of a frame is assumed to be its rotor Y.
                    if (i == beg[k])
                    {
                        c0 = y0;
                        c1 = y1;
                        c2 = y2;
                    }
                    else
                    {
                        // Calculate coordinate from transformation matrix and offset.
                        v0 = co0[i];
                        v1 = co1[i];
                        v2 = co2[i];
                        c0 = y0 + m0 * v0 + m1 * v1 + m2 * v2;
                        c1 = y1 + m3 * v0 + m4 * v1 + m5 * v2;
                        c2 = y2 + m6 * v0 + m7 * v1 + m8 * v2;

                        // Store coordinate from registers into memory.
                        c[i0] = c0;
                        c[i1] = c1;
                        c[i2] = c2;
                    }

                    // TODO: move conditional expression out to bypass short circuiting.
                    // Penalize out-of-box case.
                    if (c0 < cr0[0] || cr1[0] <= c0 || c1 < cr0[1] || cr1[1] <= c1 || c2 < cr0[2] || cr1[2] <= c2)
                    {
                        y += 10.0f;
                        d[i0] = 0.0f;
                        d[i1] = 0.0f;
                        d[i2] = 0.0f;
                        continue;
                    }

                    // Find the index of the current coordinate
                    k0 = (int)((c0 - cr0[0]) * gri);
                    k1 = (int)((c1 - cr0[1]) * gri);
                    k2 = (int)((c2 - cr0[2]) * gri);
                    assert(k0 + 1 < npr[0]);
                    assert(k1 + 1 < npr[1]);
                    assert(k2 + 1 < npr[2]);
                    k0 = npr[0] * (npr[1] * k2 + k1) + k0;

                    // Retrieve the grid map and lookup the value
                    map = mps[xst[i]].ToArray();
                    e000 = map[k0];
                    e100 = map[k0 + 1];
                    e010 = map[k0 + npr[0]];
                    e001 = map[k0 + npr[0] * npr[1]];
                    y += e000;
                    d[i0] = (e100 - e000) * gri;
                    d[i1] = (e010 - e000) * gri;
                    d[i2] = (e001 - e000) * gri;
                }
                for (j = 0, z = nbr[k]; j < z; ++j)
                {
                    i = brs[b++];
                    i0 = beg[i] * gd3 + gid;
                    i1 = i0 + gds;
                    i2 = i1 + gds;
                    c[i0] = y0 + m0 * yy0[i] + m1 * yy1[i] + m2 * yy2[i];
                    c[i1] = y1 + m3 * yy0[i] + m4 * yy1[i] + m5 * yy2[i];
                    c[i2] = y2 + m6 * yy0[i] + m7 * yy1[i] + m8 * yy2[i];

                    // Skip inactive BRANCH frame
                    if (!(act[i] != 0)) continue;

                    // Update a of BRANCH frame
                    a0 = m0 * xy0[i] + m1 * xy1[i] + m2 * xy2[i];
                    a1 = m3 * xy0[i] + m4 * xy1[i] + m5 * xy2[i];
                    a2 = m6 * xy0[i] + m7 * xy1[i] + m8 * xy2[i];
                    assert(IMath.fabs(a0 * a0 + a1 * a1 + a2 * a2 - 1.0f) < 0.0001f);
                    a[k0 = i * gd3 + gid] = a0;
                    a[k0 += gds] = a1;
                    a[k0 += gds] = a2;

                    // Update q of BRANCH frame
                    ang = x[w += gds] * 0.5f;
                    sng = IMath.sin(ang);
                    r0 = IMath.cos(ang);
                    r1 = sng * a0;
                    r2 = sng * a1;
                    r3 = sng * a2;
                    q00 = r0 * q0 - r1 * q1 - r2 * q2 - r3 * q3;
                    q01 = r0 * q1 + r1 * q0 + r2 * q3 - r3 * q2;
                    q02 = r0 * q2 - r1 * q3 + r2 * q0 + r3 * q1;
                    q03 = r0 * q3 + r1 * q2 - r2 * q1 + r3 * q0;
                    assert(IMath.fabs(q00 * q00 + q01 * q01 + q02 * q02 + q03 * q03 - 1.0f) < 0.0001f);
                    q[k0 = i * gd4 + gid] = q00;
                    q[k0 += gds] = q01;
                    q[k0 += gds] = q02;
                    q[k0 += gds] = q03;
                }
            }
            assert(b == nf - 1);
            //	assert(w == nv * gds + gid);
            assert(k == nf);

            // Calculate intra-ligand free energy.
            for (i = 0; i < np; ++i)
            {
                i0 = ip0[i] * gd3 + gid;
                i1 = i0 + gds;
                i2 = i1 + gds;
                k0 = ip1[i] * gd3 + gid;
                k1 = k0 + gds;
                k2 = k1 + gds;
                v0 = c[k0] - c[i0];
                v1 = c[k1] - c[i1];
                v2 = c[k2] - c[i2];
                vs = v0 * v0 + v1 * v1 + v2 * v2;
                if (vs < 64.0f)
                {
                    j = ipp[i] + (int)(sfs * vs);
                    y += sfe[j];
                    dr = sfd[j];
                    d0 = dr * v0;
                    d1 = dr * v1;
                    d2 = dr * v2;
                    d[i0] -= d0;
                    d[i1] -= d1;
                    d[i2] -= d2;
                    d[k0] += d0;
                    d[k1] += d1;
                    d[k2] += d2;
                }
            }

            // If the free energy is no better than the upper bound, refuse this conformation.
            if (y >= eub) return false;

            // Store e from register into memory.
            e[gid] = y;

            // Calculate and aggregate the force and torque of BRANCH frames to their parent frame.
            f[k0 = gid] = 0.0f;
            t[k0] = 0.0f;
            for (i = 1, z = 3 * nf; i < z; ++i)
            {
                f[k0 += gds] = 0.0f;
                t[k0] = 0.0f;
            }
            //	assert(w == nv * gds + gid);
            assert(k == nf);
            while (k != 0)
            {
                --k;

                // Load f, t and rotorY from memory into register
                k0 = k * gd3 + gid;
                k1 = k0 + gds;
                k2 = k1 + gds;
                f0 = f[k0];
                f1 = f[k1];
                f2 = f[k2];
                t0 = t[k0];
                t1 = t[k1];
                t2 = t[k2];
                y0 = c[i0 = beg[k] * gd3 + gid];
                y1 = c[i0 += gds];
                y2 = c[i0 += gds];

                // Aggregate frame atoms.
                for (i = beg[k], z = end[k]; i < z; ++i)
                {
                    i0 = i * gd3 + gid;
                    i1 = i0 + gds;
                    i2 = i1 + gds;
                    d0 = d[i0];
                    d1 = d[i1];
                    d2 = d[i2];

                    // The derivatives with respect to the position, orientation, and torsions
                    // would be the negative total force acting on the ligand,
                    // the negative total torque, and the negative torque projections, respectively,
                    // where the projections refer to the torque applied to the branch moved by the torsion,
                    // projected on its rotation axi
                    f0 += d0;
                    f1 += d1;
                    f2 += d2;
                    if (i == beg[k]) continue;

                    v0 = c[i0] - y0;
                    v1 = c[i1] - y1;
                    v2 = c[i2] - y2;
                    t0 += v1 * d2 - v2 * d1;
                    t1 += v2 * d0 - v0 * d2;
                    t2 += v0 * d1 - v1 * d0;
                }

                if (k != 0)
                {
                    // Save the aggregated torque of active BRANCH frames to g.
                    if (act[k] != 0)
                    {
                        g[w -= gds] = t0 * a[k0] + t1 * a[k1] + t2 * a[k2]; // dot product
                    }

                    // Aggregate the force and torque of current frame to its parent frame.
                    k0 = prn[k] * gd3 + gid;
                    k1 = k0 + gds;
                    k2 = k1 + gds;
                    f[k0] += f0;
                    f[k1] += f1;
                    f[k2] += f2;
                    v0 = y0 - c[i0 = beg[prn[k]] * gd3 + gid];
                    v1 = y1 - c[i0 += gds];
                    v2 = y2 - c[i0 += gds];
                    t[k0] += t0 + v1 * f2 - v2 * f1;
                    t[k1] += t1 + v2 * f0 - v0 * f2;
                    t[k2] += t2 + v0 * f1 - v1 * f0;
                }
            }
            assert(w == 6 * gds + gid);

            // Save the aggregated force and torque of ROOT frame to g.
            g[i0 = gid] = f0;
            g[i0 += gds] = f1;
            g[i0 += gds] = f2;
            g[i0 += gds] = t0;
            g[i0 += gds] = t1;
            g[i0 += gds] = t2;
            return true;
        }

        void monte_carlo(float[] s0e, int[] lig, int nv, int nf, int na, int np, int seed, int nbi,
            float[] sfe, float[] sfd, int sfs, Vec3 cr0, Vec3 cr1, int[] npr,
                float gri, List<List<float>> mps, int gid, int gds)
        {
            int nls = 5; // Number of line search trials for determining step size in BFGS
            float eub = 40.0f * na; // A conformation will be droped if its free energy is not better than e_upper_bound.
            float[] s0x = GetPointer<float, float>(s0e, gds);// &s0e[gds];
            float[] s0g = GetPointer<float, float>(s0x, (nv + 1) * gds); //&s0x[(nv + 1) * gds];
            float[] s0a = GetPointer<float, float>(s0g, nv * gds); // &s0g[nv * gds];
            float[] s0q = GetPointer<float, float>(s0a, 3 * nf * gds); //&s0a[3 * nf * gds];
            float[] s0c = GetPointer<float, float>(s0q, 4 * nf * gds); // &s0q[4 * nf * gds];
            float[] s0d = GetPointer<float, float>(s0c, 3 * na * gds); // &s0c[3 * na * gds];
            float[] s0f = GetPointer<float, float>(s0d, 3 * na * gds); //&s0d[3 * na * gds];
            float[] s0t = GetPointer<float, float>(s0f, 3 * nf * gds); //&s0f[3 * nf * gds];
            float[] s1e = GetPointer<float, float>(s0t, 3 * nf * gds); //&s0t[3 * nf * gds];
            float[] s1x = GetPointer<float, float>(s1e, gds); //&s1e[gds];
            float[] s1g = GetPointer<float, float>(s1x, (nv + 1) * gds); //&s1x[(nv + 1) * gds];
            float[] s1a = GetPointer<float, float>(s1g, nv * gds); //&s1g[nv * gds];
            float[] s1q = GetPointer<float, float>(s1a, 3 * nf * gds); //&s1a[3 * nf * gds];
            float[] s1c = GetPointer<float, float>(s1q, 4 * nf * gds); //&s1q[4 * nf * gds];
            float[] s1d = GetPointer<float, float>(s1c, 3 * na * gds); //&s1c[3 * na * gds];
            float[] s1f = GetPointer<float, float>(s1d, 3 * na * gds); //&s1d[3 * na * gds];
            float[] s1t = GetPointer<float, float>(s1f, 3 * nf * gds); //&s1f[3 * nf * gds];
            float[] s2e = GetPointer<float, float>(s1t, 3 * nf * gds); //&s1t[3 * nf * gds];
            float[] s2x = GetPointer<float, float>(s2e, gds); //&s2e[gds];
            float[] s2g = GetPointer<float, float>(s2x, (nv + 1) * gds); //&s2x[(nv + 1) * gds];
            float[] s2a = GetPointer<float, float>(s2g, nv * gds); //&s2g[nv * gds];
            float[] s2q = GetPointer<float, float>(s2a, 3 * nf * gds); //&s2a[3 * nf * gds];
            float[] s2c = GetPointer<float, float>(s2q, 4 * nf * gds); //&s2q[4 * nf * gds];
            float[] s2d = GetPointer<float, float>(s2c, 3 * na * gds); //&s2c[3 * na * gds];
            float[] s2f = GetPointer<float, float>(s2d, 3 * na * gds); //&s2d[3 * na * gds];
            float[] s2t = GetPointer<float, float>(s2f, 3 * nf * gds); //&s2f[3 * nf * gds];
            float[] bfh = GetPointer<float, float>(s2t, 3 * nf * gds);// &s2t[3 * nf * gds];
            float[] bfp = GetPointer<float, float>(bfh, (nv * (nv + 1) >> 1) * gds); //&bfh[(nv*(nv+1)>>1) * gds];
            float[] bfy = GetPointer<float, float>(bfp, nv * gds); //&bfp[nv * gds];
            float[] bfm = GetPointer<float, float>(bfy, nv * gds); //&bfy[nv * gds];
            float rd0, rd1, rd2, rd3, rst;
            float sum, pg1, pga, pgc, alp, pg2, pr0, pr1, pr2, nrm, ang, sng, pq0, pq1, pq2, pq3, s1xq0, s1xq1, s1xq2, s1xq3, s2xq0, s2xq1, s2xq2, s2xq3, bpi;
            float yhy, yps, ryp, pco, bpj, bmj, ppj;
            int g, i, j, o0, o1, o2;
            mt19937_64 rng = new mt19937_64(seed);
            uniform_real_distribution uniform_01 = new uniform_real_distribution(0, 1);

            // Randomize s0x.
            rd0 = uniform_01.GetRandomValue(rng);
            s0x[o0 = gid] = rd0 * cr1[0] + (1 - rd0) * cr0[0];
            rd0 = uniform_01.GetRandomValue(rng);
            s0x[o0 += gds] = rd0 * cr1[1] + (1 - rd0) * cr0[1];
            rd0 = uniform_01.GetRandomValue(rng);
            s0x[o0 += gds] = rd0 * cr1[2] + (1 - rd0) * cr0[2];
            rd0 = uniform_01.GetRandomValue(rng);
            rd1 = uniform_01.GetRandomValue(rng);
            rd2 = uniform_01.GetRandomValue(rng);
            rd3 = uniform_01.GetRandomValue(rng);
            rst = 1 / IMath.sqrt(rd0 * rd0 + rd1 * rd1 + rd2 * rd2 + rd3 * rd3);
            s0x[o0 += gds] = rd0 * rst;
            s0x[o0 += gds] = rd1 * rst;
            s0x[o0 += gds] = rd2 * rst;
            s0x[o0 += gds] = rd3 * rst;
            for (i = 6; i < nv; ++i)
            {
                s0x[o0 += gds] = uniform_01.GetRandomValue(rng);
            }
            evaluate(s0e, s0g, s0a, s0q, s0c, s0d, s0f, s0t, s0x, nf, na, np, eub, lig, sfe, sfd, sfs, cr0, cr1, npr, gri, mps, gid, gds);

            // Repeat for a number of generations.
            for (g = 0; g < nbi; ++g)
            {
                // Mutate s0x into s1x
                o0 = gid;
                s1x[o0] = s0x[o0] + uniform_01.GetRandomValue(rng);
                o0 += gds;
                s1x[o0] = s0x[o0] + uniform_01.GetRandomValue(rng);
                o0 += gds;
                s1x[o0] = s0x[o0] + uniform_01.GetRandomValue(rng);
                //		for (i = 3; i < nv + 1; ++i)
                for (i = 2 - nv; i < 0; ++i)
                {
                    o0 += gds;
                    s1x[o0] = s0x[o0];
                }
                evaluate(s1e, s1g, s1a, s1q, s1c, s1d, s1f, s1t, s1x, nf, na, np, eub, lig, sfe, sfd, sfs, cr0, cr1, npr, gri, mps, gid, gds);

                // Initialize the inverse Hessian matrix to identity matrix.
                // An easier option that works fine in practice is to use a scalar multiple of the identity matrix,
                // where the scaling factor is chosen to be in the range of the eigenvalues of the true Hessian.
                // See N&R for a recipe to find this initializer.
                bfh[o0 = gid] = 1.0f;
                for (j = 1; j < nv; ++j)
                {
                    for (i = 0; i < j; ++i)
                    {
                        bfh[o0 += gds] = 0.0f;
                    }
                    bfh[o0 += gds] = 1.0f;
                }

                // Use BFGS to optimize the mutated conformation s1x into local optimum s2x.
                // http://en.wikipedia.org/wiki/BFGS_method
                // http://en.wikipedia.org/wiki/Quasi-Newton_method
                // The loop breaks when no appropriate alpha can be found.
                while (true)
                {
                    // Calculate p = -h * g, where p is for descent direction, h for Hessian, and g for gradient.
                    sum = bfh[o1 = gid] * s1g[o0 = gid];
                    for (i = 1; i < nv; ++i)
                    {
                        sum += bfh[o1 += i * gds] * s1g[o0 += gds];
                    }
                    bfp[o2 = gid] = -sum;
                    for (j = 1; j < nv; ++j)
                    {
                        sum = bfh[o1 = (j * (j + 1) >> 1) * gds + gid] * s1g[o0 = gid];
                        for (i = 1; i < nv; ++i)
                        {
                            sum += bfh[o1 += i > j ? i * gds : gds] * s1g[o0 += gds];
                        }
                        bfp[o2 += gds] = -sum;
                    }

                    // Calculate pg = p * g = -h * g^2 < 0
                    o0 = gid;
                    pg1 = bfp[o0] * s1g[o0];
                    for (i = 1; i < nv; ++i)
                    {
                        o0 += gds;
                        pg1 += bfp[o0] * s1g[o0];
                    }
                    pga = 0.0001f * pg1;
                    pgc = 0.9f * pg1;

                    // Perform a line search to find an appropriate alpha.
                    // Try different alpha values for nls times.
                    // alpha starts with 1, and shrinks to 0.1 of itself iteration by iteration.
                    alp = 1.0f;
                    for (j = 0; j < nls; ++j)
                    {
                        // Calculate x2 = x1 + a * p.
                        o0 = gid;
                        s2x[o0] = s1x[o0] + alp * bfp[o0];
                        o0 += gds;
                        s2x[o0] = s1x[o0] + alp * bfp[o0];
                        o0 += gds;
                        s2x[o0] = s1x[o0] + alp * bfp[o0];
                        o0 += gds;
                        s1xq0 = s1x[o0];
                        pr0 = bfp[o0];
                        o0 += gds;
                        s1xq1 = s1x[o0];
                        pr1 = bfp[o0];
                        o0 += gds;
                        s1xq2 = s1x[o0];
                        pr2 = bfp[o0];
                        o0 += gds;
                        s1xq3 = s1x[o0];
                        assert(IMath.fabs(s1xq0 * s1xq0 + s1xq1 * s1xq1 + s1xq2 * s1xq2 + s1xq3 * s1xq3 - 1.0f) < 0.00001f);
                        nrm = IMath.sqrt(pr0 * pr0 + pr1 * pr1 + pr2 * pr2);
                        ang = 0.5f * alp * nrm;
                        sng = IMath.sin(ang) / nrm;
                        pq0 = IMath.cos(ang);
                        pq1 = sng * pr0;
                        pq2 = sng * pr1;
                        pq3 = sng * pr2;
                        assert(IMath.fabs(pq0 * pq0 + pq1 * pq1 + pq2 * pq2 + pq3 * pq3 - 1.0f) < 2e-3f);
                        s2xq0 = pq0 * s1xq0 - pq1 * s1xq1 - pq2 * s1xq2 - pq3 * s1xq3;
                        s2xq1 = pq0 * s1xq1 + pq1 * s1xq0 + pq2 * s1xq3 - pq3 * s1xq2;
                        s2xq2 = pq0 * s1xq2 - pq1 * s1xq3 + pq2 * s1xq0 + pq3 * s1xq1;
                        s2xq3 = pq0 * s1xq3 + pq1 * s1xq2 - pq2 * s1xq1 + pq3 * s1xq0;
                        assert(IMath.fabs(s2xq0 * s2xq0 + s2xq1 * s2xq1 + s2xq2 * s2xq2 + s2xq3 * s2xq3 - 1.0f) < 2e-3f);
                        s2x[o0 -= 3 * gds] = s2xq0;
                        s2x[o0 += gds] = s2xq1;
                        s2x[o0 += gds] = s2xq2;
                        s2x[o0 += gds] = s2xq3;
                        for (i = 6; i < nv; ++i)
                        {
                            bpi = bfp[o0];
                            o0 += gds;
                            s2x[o0] = s1x[o0] + alp * bpi;
                        }

                        // Evaluate x2, subject to Wolfe conditions http://en.wikipedia.org/wiki/Wolfe_conditions
                        // 1) Armijo rule ensures that the step length alpha decreases f sufficiently.
                        // 2) The curvature condition ensures that the slope has been reduced sufficiently.
                        if (evaluate(s2e, s2g, s2a, s2q, s2c, s2d, s2f, s2t, s2x, nf, na, np, s1e[gid] + alp * pga, lig, sfe, sfd, sfs, cr0, cr1, npr, gri, mps, gid, gds))
                        {
                            o0 = gid;
                            pg2 = bfp[o0] * s2g[o0];
                            for (i = 1; i < nv; ++i)
                            {
                                o0 += gds;
                                pg2 += bfp[o0] * s2g[o0];
                            }
                            if (pg2 >= pgc) break;
                        }

                        alp *= 0.1f;
                    }

                    // If no appropriate alpha can be found, exit the BFGS loop.
                    if (j == nls) break;

                    // Calculate y = g2 - g1.
                    o0 = gid;
                    bfy[o0] = s2g[o0] - s1g[o0];
                    for (i = 1; i < nv; ++i)
                    {
                        o0 += gds;
                        bfy[o0] = s2g[o0] - s1g[o0];
                    }

                    // Calculate m = -h * y.
                    sum = bfh[o1 = gid] * bfy[o0 = gid];
                    for (i = 1; i < nv; ++i)
                    {
                        sum += bfh[o1 += i * gds] * bfy[o0 += gds];
                    }
                    bfm[o2 = gid] = -sum;
                    for (j = 1; j < nv; ++j)
                    {
                        sum = bfh[o1 = (j * (j + 1) >> 1) * gds + gid] * bfy[o0 = gid];
                        for (i = 1; i < nv; ++i)
                        {
                            sum += bfh[o1 += i > j ? i * gds : gds] * bfy[o0 += gds];
                        }
                        bfm[o2 += gds] = -sum;
                    }

                    // Calculate yhy = -y * m = -y * (-h * y) = y * h * y.
                    o0 = gid;
                    yhy = -bfy[o0] * bfm[o0];
                    for (i = 1; i < nv; ++i)
                    {
                        o0 += gds;
                        yhy -= bfy[o0] * bfm[o0];
                    }

                    // Calculate yps = y * p.
                    o0 = gid;
                    yps = bfy[o0] * bfp[o0];
                    for (i = 1; i < nv; ++i)
                    {
                        o0 += gds;
                        yps += bfy[o0] * bfp[o0];
                    }

                    // Update Hessian matrix h.
                    ryp = 1.0f / yps;
                    pco = ryp * (ryp * yhy + alp);
                    o2 = gid;
                    for (j = 0; j < nv; ++j)
                    {
                        bpj = bfp[o2];
                        bmj = bfm[o2];
                        ppj = pco * bpj;
                        bfh[o1 = (j * (j + 3) >> 1) * gds + gid] += (ryp * 2 * bmj + ppj) * bpj;
                        for (i = j + 1; i < nv; ++i)
                        {
                            o0 = i * gds + gid;
                            bpi = bfp[o0];
                            bfh[o1 += i * gds] += ryp * (bmj * bpi + bfm[o0] * bpj) + ppj * bpi;
                        }
                        o2 += gds;
                    }

                    // Move to the next iteration, i.e. e1 = e2, x1 = x2, g1 = g2.
                    o0 = gid;
                    s1e[o0] = s2e[o0];
                    //			for (i = 1; i < 2 * (nv + 1); ++i)
                    for (i = -1 - 2 * nv; i < 0; ++i)
                    {
                        o0 += gds;
                        s1e[o0] = s2e[o0];
                    }
                }

                // Accept x1 according to Metropolis criteria.
                if (s1e[gid] < s0e[gid])
                {
                    o0 = gid;
                    s0e[o0] = s1e[o0];
                    //			for (i = 1; i < nv + 2; ++i)
                    for (i = -1 - nv; i < 0; ++i)
                    {
                        o0 += gds;
                        s0e[o0] = s1e[o0];
                    }
                }
            }
        }

    }

}