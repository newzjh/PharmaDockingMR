using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MoleculeLogic
{
    public class PharmaGroup : Combineable
    {
        List<bool> oldshowpharmas = new List<bool>();

        public override bool IsDirty(MoleculeScheme scheme)
        {
            if (oldshowpharmas.Count != scheme.showpharmas.Count)
                return true;

            for (int i = 0; i < oldshowpharmas.Count; i++)
            {
                if (oldshowpharmas[i] != scheme.showpharmas[i])
                    return true;
            }

            return false;

            //return oldvalue != scheme.showpharma;
        }

        public override void SetDirty(MoleculeScheme scheme)
        {
            oldshowpharmas.Clear();
            oldshowpharmas.AddRange(scheme.showpharmas);
 
            //oldvalue = scheme.showpharma;
        }

        public override int GetBreathInterval()
        {
            return 30;
        }

    }
}