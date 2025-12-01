using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;

namespace idock
{
    public class frame 
    {
	    public int parent; //!< Frame array index pointing to the parent of current frame. For ROOT frame, this field is not used.
	    public int rotorXsrn; //!< Serial atom number of the parent frame atom which forms a rotatable bond with the rotorY atom of current frame.
	    public int rotorYsrn; //!< Serial atom number of the current frame atom which forms a rotatable bond with the rotorX atom of parent frame.
	    public int rotorXidx; //!< Index pointing to the parent frame atom which forms a rotatable bond with the rotorY atom of current frame.
	    public int rotorYidx; //!< Index pointing to the current frame atom which forms a rotatable bond with the rotorX atom of parent frame.
	    public int habegin; //!< The inclusive beginning index to the heavy atoms of the current frame.
	    public int haend; //!< The exclusive ending index to the heavy atoms of the current frame.
	    public int hybegin; //!< The inclusive beginning index to the hydrogen atoms of the current frame.
	    public int hyend; //!< The exclusive ending index to the hydrogen atoms of the current frame.
	    public bool active; //!< Indicates if the current frame is active.
	    public Vec3 parent_rotorY_to_current_rotorY; //!< Vector pointing from the origin of parent frame to the origin of current frame.
	    public Vec3 parent_rotorX_to_current_rotorY; //!< Normalized vector pointing from rotor X of parent frame to rotor Y of current frame.

	    //! Constructs an active frame, and relates it to its parent frame.
	    public frame( int parent,  int rotorXsrn,  int rotorYsrn,  int rotorXidx, int  habegin, int hybegin) 
        {
            this.parent=parent;
            this.rotorXsrn = rotorXsrn;
            this.rotorYsrn = rotorYsrn;
            this.rotorXidx = rotorXidx;
            this.habegin = habegin;
            this.hybegin = hybegin;
            this.active = true;
        }


        private string setw(int n)
        {
            string s = "";
            for (int i = 0; i < n; i++)
                s += ' ';
            return s;
        }

        public void output(StreamWriter ofs)
        {
            string ret = "BRANCH" + setw(4) + rotorXsrn.ToString() + setw(4) + rotorYsrn.ToString() + "\n";
            ofs.WriteLine(ret);
        }
    }

}
