using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace MoleculeLogic
{

    [InitializeOnLoad]
    [CustomEditor(typeof(MoleculeFactory))]
    public class MoleculeFactoryInspector : Editor
    {

        MoleculeFactoryInspector()
        {
        }

        string m_lastOpenDir = null;
        public override void OnInspectorGUI()
        {
            MoleculeFactory mf = target as MoleculeFactory;
            mf.OnUILayout();

            if (GUILayout.Button("Load From File"))
            {
                m_lastOpenDir = PlayerPrefs.GetString("LastDir");
                if (m_lastOpenDir == null || !Directory.Exists(m_lastOpenDir))
                {
#if !UNITY_WEBPLAYER
                    m_lastOpenDir = System.IO.Directory.GetCurrentDirectory();
#else
                m_lastOpenDir = Application.persistentDataPath;                
#endif
                }

                string path = EditorUtility.OpenFilePanel("Load molecule",
                    m_lastOpenDir,
                    "pdb;*.ent;*.mol2;*.ml2;*.sy2");
                if (path != null && path.Length > 0)
                {
                    mf.CreateFromFile(path);
                    m_lastOpenDir = Path.GetDirectoryName(path);
                    PlayerPrefs.SetString("LastDir", m_lastOpenDir);
                }
            }


        }
    }

}