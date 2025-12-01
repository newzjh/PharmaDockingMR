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

using System.Collections.Generic;
using UnityEngine;

namespace MoleculeLogic
{
    /// <summary>
    /// Container object to group residues by chain and set chain-based temperature colors.
    /// </summary>
    public class Chain : HoverObject
    {
        public string ChainIdentifier;
        public Color ChainColor;
        public Dictionary<int, Residue> residues = new Dictionary<int, Residue>();

        public static Chain CreateChain(Molecule molecule, string chainIdentifier, int index, Transform parent)
        {
            GameObject chainobj = new GameObject();
            chainobj.name = "Chain" + index;
            chainobj.transform.parent = parent;// molecule.transform;
            chainobj.transform.localScale = Vector3.one;
            chainobj.transform.localEulerAngles = Vector3.zero;
            chainobj.transform.localPosition = Vector3.zero;
            Chain chain = chainobj.AddComponent<Chain>();
            chain.ChainIdentifier = chainIdentifier;
            return chain;
        }




        /// <summary>
        /// Assigns colors to all the chains for a molecule.
        /// </summary>
        /// <param name="chains">A list of chains.</param>
        public static void SetChainColors(Dictionary<string,Chain> chains)
        {
            string[] keys=new string[chains.Keys.Count];
            chains.Keys.CopyTo(keys, 0);
            for (int index = 0; index < keys.Length; index++)
            {
                string key = keys[index];
                Chain chain = chains[key];
                if (chain.ChainIdentifier == "")
                    chain.ChainColor = Color.red;
                else
                    chain.ChainColor = GetChainColor(index);
            }
        }

        /// <summary>
        /// Selects one of five chain colors.
        /// </summary>
        /// <param name="index">A chain color index.</param>
        /// <returns>A color.</returns>
        private static Color GetChainColor(int index)
        {
            index = index % 5;

            if (index == 0) return Color.blue;
            else if (index == 1) return Color.yellow;
            else if (index == 2) return Color.green;
            else if (index == 3) return Color.blue;
            else return Color.magenta;
        }
    }
}
