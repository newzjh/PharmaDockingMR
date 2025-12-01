using System;
using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;

namespace idock
{

    public delegate float u01Handler();


    public class forest : List<tree>
    {

        private float nt_inv; //!< Inverse of the number of trees.
        private mt19937_64 rng;
        //mutable mutex m;
        //! Returns a random value from uniform distribution in [0, 1) in a thread safe manner.


        private static void assert(bool b)
        {
            Kernel.assert(b);
        }

        //! Constructs a random forest of a number of empty trees.
        public forest(int nt, int seed)
        {
            if (nt>0 && this.Capacity<nt)
                this.Capacity = nt;
            for (int i = 0; i < nt; i++)
                this.Add(new tree());
            nt_inv = 1.0f / nt;
            //rng(seed), uniform_01(0, 1), u01_s([&]()
            //    lock_guard<mutex> guard(m);
            //    return uniform_01(rng);
            

        }


        //! Predicts the y value of the given sample x.
        public float PredictsY(ref List<float> x)
        {
            float y = 0;
            foreach (tree t in this)
            {
                y += t.PredictsY(ref x);
            }
            return y *= nt_inv;
        }

        //! Clears node samples to save memory.
        public void clear()
        {
            foreach (tree t in this)
            {
                t.clear();
            }
        }

 
    };

}