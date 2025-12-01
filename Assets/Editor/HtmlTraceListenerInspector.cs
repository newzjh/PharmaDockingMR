using UnityEditor;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace LogTrace
{

    [CustomEditor(typeof(HtmlTraceListener))]
    public class HtmlTraceListenerInspector : Editor
    {

        public override void OnInspectorGUI()
        {
            HtmlTraceListener listener = target as HtmlTraceListener;
            if (GUILayout.Button("Open Log"))
            {
                string filepath = Application.persistentDataPath + "/log.html";
                System.Diagnostics.Process.Start(filepath);
            }
        }
    }

}