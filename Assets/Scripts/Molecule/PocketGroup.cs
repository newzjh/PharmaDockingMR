using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MoleculeLogic
{
    public class PocketGroup : Combineable
    {
        List<bool> oldshowpockets = new List<bool>();

        public override bool IsDirty(MoleculeScheme scheme)
        {
            if (oldshowpockets.Count != scheme.showpockets.Count)
                return true;

            for (int i = 0; i < oldshowpockets.Count; i++)
            {
                if (oldshowpockets[i] != scheme.showpockets[i])
                    return true;
            }

            return false;

            //return oldvalue != scheme.showpocket;
        }

        public override void SetDirty(MoleculeScheme scheme)
        {
            oldshowpockets.Clear();
            oldshowpockets.AddRange(scheme.showpockets);
            //oldvalue = scheme.showpocket;
        }

        public override int GetBreathInterval()
        {
            return 30;
        }
    }
}