using UnityEditor;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace MoleculeLogic
{

    [CustomEditor(typeof(MoleculeController))]
    public class MoleculeControllerInspector : Editor
    {

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            MoleculeController mc = target as MoleculeController;
            mc.OnUILayout();
        }
    }

}