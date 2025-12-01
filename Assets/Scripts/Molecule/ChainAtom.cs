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

namespace MoleculeLogic
{
    /// <summary>
    /// <see cref="Atom" /> subclass for standard protein chain atoms. 
    /// </summary>
    /// <remarks>
    /// Adds functionality to toggle visibility based on the <see cref="Molecule.ShowFullChain" /> property.
    /// </remarks>
    public class ChainAtom : Atom
    {
        /// <summary>
        /// Attaches an event handedler to <see cref="Molecule.ShowFullChainChanged" />.
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();

        }
     
    }
}
