#if VISTA
using Pinwheel.Vista.Graph;
using UnityEditor;
using UnityEngine;

namespace Pinwheel.VistaEditor.Graph
{
    [NodeEditor(typeof(SplitMaskNode))]
    public class SplitMaskNodeEditor : ImageNodeEditorBase
    {
        private static readonly GUIContent OUTPUT_COUNT = new GUIContent("Output Count", "Number of masks to create.");

        public override void OnGUI(INode node)
        {
            SplitMaskNode n = node as SplitMaskNode;
            EditorGUI.BeginChangeCheck();
            int outputCount = EditorGUILayout.DelayedIntField(OUTPUT_COUNT, n.outputCount);
            if (EditorGUI.EndChangeCheck())
            {
                m_graphEditor.RegisterUndo(n);
                n.outputCount = outputCount;
            }

            for (int i = 0; i < n.weights.Length; ++i)
            {
                EditorGUI.BeginChangeCheck();
                int weight = EditorGUILayout.IntField($"Weight {i + 1}", n.weights[i]);
                if (EditorGUI.EndChangeCheck())
                {
                    m_graphEditor.RegisterUndo(n);
                    n.SetWeight(i, weight);
                }
            }
        }
    }
}
#endif
