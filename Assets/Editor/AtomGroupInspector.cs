using UnityEditor;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace MoleculeLogic
{

    [CustomEditor(typeof(AtomGroup))]
    public class AtomGroupInspector : Editor
    {

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (GUILayout.Button("Clear"))
            {
                AtomGroup ag = target as AtomGroup;
                Atom[] atoms= ag.GetComponentsInChildren<Atom>();
                for (int i = 0; i < atoms.Length; i++)
                {
                    DestroyImmediate(atoms[i].gameObject);
                }
                Resources.UnloadUnusedAssets();
            }
        }
    }

}