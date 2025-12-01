using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MoleculeLogic
{
    public class AtomGroup : Combineable
    {
        int oldshowwater = -2;
        List<bool> oldshowatoms = new List<bool>();

        public override bool IsDirty(MoleculeScheme scheme)
        {
            if (base.IsDirty(scheme))
                return true;

            if (oldshowwater != scheme.showwater)
                return true;

            if (oldshowatoms.Count != scheme.showatoms.Count)
                return true;

            for (int i = 0; i < oldshowatoms.Count; i++)
            {
                if (oldshowatoms[i] != scheme.showatoms[i])
                    return true;
            }

            return false;
        }

        public override void SetDirty(MoleculeScheme scheme)
        {
            base.SetDirty(scheme);
            oldshowwater = scheme.showwater;
            oldshowatoms.Clear();
            oldshowatoms.AddRange(scheme.showatoms);

        }
 

    }
}