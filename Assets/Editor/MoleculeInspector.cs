using UnityEditor;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace MoleculeLogic
{

    [CustomEditor(typeof(Molecule))]
    public class MoleculeInspector : Editor
    {

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            Molecule mol = target as Molecule;
            mol.OnUILayout();
        }
    }

}