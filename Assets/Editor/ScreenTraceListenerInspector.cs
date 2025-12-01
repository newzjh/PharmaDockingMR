using UnityEditor;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace LogTrace
{

    [CustomEditor(typeof(ScreenTraceListener))]
    public class ScreenTraceListenerInspector : Editor
    {

        public override void OnInspectorGUI()
        {
            ScreenTraceListener listener = target as ScreenTraceListener;
            listener.OnGUIEx();
        }
    }

}