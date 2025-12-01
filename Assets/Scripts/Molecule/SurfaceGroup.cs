using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MoleculeLogic
{
    public class SurfaceGroup : Combineable
    {

        public override int GetBreathInterval()
        {
            return 1;
        }

    }
}