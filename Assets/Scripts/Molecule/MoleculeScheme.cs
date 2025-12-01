//=============================================================================
// This file is part of The Scripps Research Institute's C-ME Application built
// by InterKnowlogy.  
//
// Copyright (C) 2006, 2007 Scripps Research Institute / InterKnowlogy, LLC.
// All rights reserved.
//
// For information about this application contact Tim Huckaby at
// TimHuck@InterKnowlogy.com or (760) 930-0075 x201.
//
// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND,
// EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.
//=============================================================================

using System;
using System.Collections.Generic;

namespace MoleculeLogic
{
    /// <summary>
    /// Emuneration for the various coloring methods.
    /// </summary>
    public enum ColorScheme
    {
        Structure,
        Atom,
        Residue,
        Chain,
        Temperature
    }

    public enum MoleculeStyle
    { 
        Wireframe = 0,
        Stick = 1,
        Ball = 2,
        BallStick = 3,
        Cartoon = 4,
        Surface = 5,
        Micro=6,
        Unused = 7,
        AEO=8,
        Max,
    }

    public class MoleculeScheme
    {
        public bool Combineable
        {
            get
            {
                return (style >= 0 && style <= 6);
            }
        }

        public bool ContainStyle(MoleculeStyle right)
        {
            //return (left & (int)right) == (int)right;
            return style == (int)right;
        }
        public int style = 4;
        public int showwater = 0;
        public int showtext = 0;
        public List<bool> showpockets = new List<bool>();
        public List<bool> showpharmas = new List<bool>();
        public List<bool> showatoms = new List<bool>();
        public List<bool> showparts = new List<bool>();

        public MoleculeScheme()
        {
            int atomtypemax = (int)AtomType.Max;
            for (int i = 0; i < atomtypemax; i++)
            {
                showatoms.Add(true);
            }
            //int compondtypemax = (int)CompondType.Max;
            //for (int i = 0; i < compondtypemax; i++)
            //{
            //    if (i<=(int)CompondType.Ligand0)
            //        showcomponds.Add(true);
            //    else
            //        showcomponds.Add(false);
            //}
        }
    }
}
