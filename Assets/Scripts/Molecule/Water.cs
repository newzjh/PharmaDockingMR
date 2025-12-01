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
using UnityEngine;

namespace MoleculeLogic
{
    /// <summary>
    /// <see cref="Atom" /> subclass for water atoms.
    /// </summary>
    /// <remarks>
    /// Adds functionality to toggle visibility based on the <see cref="Molecule.ShowWaters" /> property.
    /// </remarks>
    class Water : Atom
    {
        /// <summary>
        /// Attaches an event handedler to <see cref="Molecule.ShowWatersChanged" />.
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();

        }


        public override void Represent(MoleculeScheme scheme, MolType type, bool dirty, int level)
        {
            if (!dirty)
                return;

            if (displayobj)
            {
                DestroyImmediate(displayobj);
            }

            if (scheme.showwater!=0)
            {
                base.Represent(scheme, type, dirty, level);
            }

        }

    
    }
}
