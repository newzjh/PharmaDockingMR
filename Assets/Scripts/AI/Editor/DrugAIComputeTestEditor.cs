using UnityEditor;
using UnityEngine;

namespace AIDrugDiscovery.Editor
{
    [CustomEditor(typeof(DrugAIComputeTest))]
    public class DrugAIComputeTestEditor : UnityEditor.Editor
    {
        private DrugAIComputeTest drugAI;

        private void OnEnable()
        {
            drugAI = (DrugAIComputeTest)target;
        }

        public override void OnInspectorGUI()
        {
            
            DrawDefaultInspector();

            EditorGUILayout.Space(20);

            
            EditorGUILayout.BeginVertical("Box");
            {
                EditorGUILayout.LabelField("Playback Controls", EditorStyles.boldLabel);

                EditorGUILayout.Space(10);

                
                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Pause", GUILayout.Height(30)))
                    {
                        drugAI.Pause();
                    }

                    if (GUILayout.Button("Resume", GUILayout.Height(30)))
                    {
                        drugAI.Resume();
                    }

                    if (GUILayout.Button("Terminate", GUILayout.Height(30)))
                    {
                        drugAI.Terminate();
                    }

                    if (GUILayout.Button("Reset", GUILayout.Height(30)))
                    {
                        drugAI.Reset();
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(10);

                
                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField("Total batches:", "10");
                    EditorGUILayout.LabelField("Current batch:", (drugAI.currentBatch + 1).ToString());
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(20);

            
            EditorGUILayout.BeginVertical("Box");
            {
                EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);

                if (GUILayout.Button("Start Task", GUILayout.Height(30)))
                {
                    drugAI.Reset();
                    drugAI.Start();
                }
            }
            EditorGUILayout.EndVertical();
        }
    }
}
