using System;
using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;

namespace idock
{
    public struct Vec3
    {
        public const float kEpsilon = 1e-005f;

        public float x;
        public float y;
        public float z;

        public float this[int index] 
        {
            get
            {
                if (index == 0)
                    return x;
                else if (index == 1)
                    return y;
                else if (index == 2)
                    return z;
                else
                    return 0.0f;
            }
            set
            {
                if (index == 0)
                    x = value;
                else if (index == 1)
                    y = value;
                else if (index == 2)
                    z = value;
            }
        }

        public Vec3(float _x, float _y, float _z)
        {
            x = _x;
            y = _y;
            z = _z;
        }

        public static Vec3 operator -(Vec3 a)
        {
            return new Vec3(-a.x, -a.y, -a.z);
        }
        public static Vec3 operator -(Vec3 a, Vec3 b)
        {
            return new Vec3(a.x-b.x, a.y-b.y, a.z-b.z);
        }
        public static Vec3 operator *(float d, Vec3 a)
        {
            return new Vec3(a.x * d, a.y * d, a.z * d);
        }
        public static Vec3 operator *(Vec3 a, float d)
        {
            return new Vec3(a.x * d, a.y * d, a.z * d);
        }
        public static Vec3 operator /(Vec3 a, float d)
        {
            return new Vec3(a.x/d, a.y/d, a.z/d);        
        }
        public static Vec3 operator +(Vec3 a, Vec3 b)
        {
            return new Vec3(a.x +b.x, a.y +b.y, a.z +b.z);
        }

        public static Vec3 zero = new Vec3(0.0f, 0.0f, 0.0f);
        public static Vec3 one = new Vec3(1.0f, 1.0f, 1.0f);
    }

    public struct Quat4
    {
        public float w;
        public float x;
        public float y;
        public float z;

        public Quat4(float _w, float _x, float _y, float _z)
        {
            w = _w;
            x = _x;
            y = _y;
            z = _z;
        }

        public float this[int index]
        {
            get
            {
                if (index == 0)
                    return w;
                else if (index == 1)
                    return x;
                else if (index == 2)
                    return y;
                else if (index == 3)
                    return z;
                else
                    return 0.0f;
            }
            set
            {
                if (index == 0)
                    w = value;
                else if (index == 1)
                    x = value;
                else if (index == 2)
                    y = value;
                else if (index == 3)
                    z = value;
            }
        }
    }

    public class IMath
    {


        public static float sqrt(float v)
        {
            return (float)Math.Sqrt(v);
            //return Mathf.Sqrt(v);
        }

        public static float sin(float v)
        {
            return (float)Math.Sin(v);
            //return Mathf.Sin(v);
        }

        public static float cos(float v)
        {
            return (float)Math.Cos(v);
            //return Mathf.Cos(v);
        }

        public static float fabs(float v)
        {
            return (float)Math.Abs(v);
            //return Mathf.Abs(v);
        }

        public static float exp(float v)
        {
            return (float)Math.Exp(v);
            //return Mathf.Exp(v);
        }

        public static void assert(bool b) 
        {
            Kernel.assert(b);
        }

        //! Returns true if the absolute difference between a scalar and zero is within the constant tolerance.
        public static bool zero(float a)
        {
	        return fabs(a) < 1e-4;
        }

        //! Returns true if the absolute difference between two scalars is within the constant tolerance.
        public static bool equal(float a, float b)
        {
	        return zero(a - b);
        }

        //! Returns true is a vector is approximately (0, 0, 0).
        public static bool zero(Vec3 a)
        {
	        return zero(a[0]) && zero(a[1]) && zero(a[2]);
        }


        public static int mr(int x, int y)
        {
            assert(x <= y);
            return (y * (y + 1) >> 1) + x;
        }

        public static int mp(int x, int y)
        {
            return x <= y ? mr(x, y) : mr(y, x);
        }

        public static float norm_sqr(Vec3 a)
        {
            return a[0] * a[0] + a[1] * a[1] + a[2] * a[2];
        }

        public static float norm_sqr(Quat4 a)
        {
            return a[0] * a[0] + a[1] * a[1] + a[2] * a[2] + a[3] * a[3];
        }

        public static float norm(Vec3 a)
        {
            return sqrt(norm_sqr(a));
        }

        public static float norm(Quat4 a)
        {
            return sqrt(norm_sqr(a));
        }

        public static bool normalized(Vec3 a)
        {
            return fabs(norm_sqr(a) - 1.0f) < 3e-3f;
        }

        //! Returns true if the current Quat4 is normalized.
        public static bool normalized(Quat4 a)
        {
            return fabs(norm_sqr(a) - 1.0f) < 3e-3f;
        }

        public static Vec3 normalize(Vec3 a)
        {
            float norm_inv = 1.0f / norm(a);
            return new Vec3
            (
                a[0] * norm_inv,
                a[1] * norm_inv,
                a[2] * norm_inv
            );
        }

        public static Quat4 normalize(Quat4 a)
        {
            float norm_inv = 1.0f / norm(a);
            Quat4 q= new Quat4
            (
                a[0] * norm_inv,
                a[1] * norm_inv,
                a[2] * norm_inv,
                a[3] * norm_inv
            );
            return q;
        }

        //! Returns the dot product of two vectors.
        public static float dot(Vec3 a, Vec3 b)
        {
	        return a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
        }

        //! Returns the cross product of two vectors.
        public static Vec3 cross(Vec3 a, Vec3 b)
        {
	        return new Vec3
	        (
		        a[1] * b[2] - a[2] * b[1],
		        a[2] * b[0] - a[0] * b[2],
		        a[0] * b[1] - a[1] * b[0]
	        );
        }

        public static float distance(Vec3 a, Vec3 b)
        {
            return norm(a - b);
        }

        //! Returns the accumulated square Euclidean distance between two vectors of vectors.
        public static float distance(List<Vec3> a, List<Vec3> b)
        {
            int n = a.Count;
            assert(n > 0);
            assert(n == b.Count);
            float sum = 0;
            for (int i = 0; i < n; ++i)
            {
                sum += distance(a[i], b[i]);
            }
            return sum;
        }

        public static float distance_sqr(Vec3 a, Vec3 b)
        {
            return norm_sqr(a - b);
        }

        //! Returns the accumulated square Euclidean distance between two vectors of vectors.
        public static float distance_sqr( List<Vec3> a,  List<Vec3> b)
        {
            int n = a.Count;
	        assert(n > 0);
	        assert(n == b.Count);
	        float sum = 0;
	        for (int i = 0; i < n; ++i)
	        {
		        sum += distance_sqr(a[i], b[i]);
	        }
	        return sum;
        }

        //! Converts a vector of size 3 to a Quat4.
        public static Quat4 vec3_to_qtn4(Vec3 rotation)
        {
	        if (zero(rotation))
	        {
                return new Quat4(1, 0, 0, 0);
	        }
	        else
	        {
		        float angle = norm(rotation);
                Vec3 axis = (1.0f / angle) * rotation;
		        return vec4_to_qtn4(axis, angle);
	        }
        }

        public static Quat4 vec4_to_qtn4(Vec3 axis, float angle)
        {
            assert(normalized(axis));
            float h = angle * 0.5f;
            float s = sin(h);
            float c = cos(h);
            Quat4 ret = new Quat4(c, s * axis[0], s * axis[1], s * axis[2]);

            return ret;
        }

        public static Quat4 mul(Quat4 a, Quat4 b)
        {
            assert(normalized(a));
            assert(normalized(b));
            Quat4 ret = new Quat4
            (
                a[0] * b[0] - a[1] * b[1] - a[2] * b[2] - a[3] * b[3],
                a[0] * b[1] + a[1] * b[0] + a[2] * b[3] - a[3] * b[2],
                a[0] * b[2] - a[1] * b[3] + a[2] * b[0] + a[3] * b[1],
                a[0] * b[3] + a[1] * b[2] - a[2] * b[1] + a[3] * b[0]
            );
            return ret;
        }

        // http://www.boost.org/doc/libs/1_46_1/libs/math/Quat4/TQE.pdf
        // http://en.wikipedia.org/wiki/Quaternions_and_spatial_rotation
        public static float[] qtn4_to_mat3(Quat4 a)
        {
            assert(normalized(a));
            float ww = a[0] * a[0];
            float wx = a[0] * a[1];
            float wy = a[0] * a[2];
            float wz = a[0] * a[3];
            float xx = a[1] * a[1];
            float xy = a[1] * a[2];
            float xz = a[1] * a[3];
            float yy = a[2] * a[2];
            float yz = a[2] * a[3];
            float zz = a[3] * a[3];

            float[] ret= new float[]
	        {
		        ww+xx-yy-zz, 2*(-wz+xy), 2*(wy+xz),
		        2*(wz+xy), ww-xx+yy-zz, 2*(-wx+yz),
		        2*(-wy+xz), 2*(wx+yz), ww-xx-yy+zz,
	        };
            return ret;
        }

        public static Vec3 mul(float[] m, Vec3 v)
        {
            Vec3 ret = new Vec3(
                m[0] * v[0] + m[1] * v[1] + m[2] * v[2],
                m[3] * v[0] + m[4] * v[1] + m[5] * v[2],
                m[6] * v[0] + m[7] * v[1] + m[8] * v[2]
            );
            return ret;
        }
    }
}