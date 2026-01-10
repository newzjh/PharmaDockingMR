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
            // 绘制默认Inspector
            DrawDefaultInspector();

            EditorGUILayout.Space(20);

            // 播放器控制区域
            EditorGUILayout.BeginVertical("Box");
            {
                EditorGUILayout.LabelField("播放器控制", EditorStyles.boldLabel);

                EditorGUILayout.Space(10);

                // 控制按钮
                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("暂停", GUILayout.Height(30)))
                    {
                        drugAI.Pause();
                    }

                    if (GUILayout.Button("继续", GUILayout.Height(30)))
                    {
                        drugAI.Resume();
                    }

                    if (GUILayout.Button("终止", GUILayout.Height(30)))
                    {
                        drugAI.Terminate();
                    }

                    if (GUILayout.Button("重置", GUILayout.Height(30)))
                    {
                        drugAI.Reset();
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(10);

                // 批次信息
                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField("总批次数:", "10");
                    EditorGUILayout.LabelField("当前批次:", (drugAI.currentBatch + 1).ToString());
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(20);

            // 快速操作按钮
            EditorGUILayout.BeginVertical("Box");
            {
                EditorGUILayout.LabelField("快速操作", EditorStyles.boldLabel);

                if (GUILayout.Button("启动任务", GUILayout.Height(30)))
                {
                    drugAI.Reset();
                    drugAI.Start();
                }
            }
            EditorGUILayout.EndVertical();
        }
    }
}
