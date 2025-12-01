using UnityEditor;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace MoleculeLogic
{

    [CustomEditor(typeof(BondGroup))]
    public class BondGroupInspector : Editor
    {

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (GUILayout.Button("Clear"))
            {
                BondGroup bg = target as BondGroup;
                Bond[] bonds = bg.GetComponentsInChildren<Bond>();
                for (int i = 0; i < bonds.Length; i++)
                {
                    DestroyImmediate(bonds[i].gameObject);
                }
                Resources.UnloadUnusedAssets();
            }
        }
    }

}