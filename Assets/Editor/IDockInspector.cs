using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace MoleculeLogic
{

    [InitializeOnLoad]
    [CustomEditor(typeof(iDock))]
    public class IDockInspector : Editor
    {
        IDockInspector()
        {
        }

        private void OnMsg(string msg, float progress)
        {
            EditorUtility.DisplayProgressBar("idock", msg, progress);
        }

        public override void OnInspectorGUI()
        {
            iDock idockobj = target as iDock;

            if (GUILayout.Button("idock"))
            {
                if (!Application.isPlaying)
                    return;

                string path = EditorUtility.OpenFilePanel("Load molecule",
                    Application.dataPath + "/idock/examples",
                    "conf");
                if (path != null && path.Length > 0)
                {
                    idock.AsynPool<int>.trace = OnMsg;
                    idock.imain.processByConfig(path);
                    EditorUtility.ClearProgressBar();
                    idock.AsynPool<int>.trace = null;
                }

            }
        }
    }

}