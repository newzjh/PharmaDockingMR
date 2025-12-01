using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MoleculeLogic
{
    public class ChainGroup : Combineable
    {
        public override int GetBreathInterval()
        {
            return 3;
        }


    }
}