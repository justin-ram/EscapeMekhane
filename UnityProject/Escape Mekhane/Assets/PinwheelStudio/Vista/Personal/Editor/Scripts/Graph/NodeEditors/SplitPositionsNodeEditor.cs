#if VISTA
using Pinwheel.Vista.Graph;
using UnityEditor;
using UnityEngine;

namespace Pinwheel.VistaEditor.Graph
{
    [NodeEditor(typeof(SplitPositionsNode))]
    public class SplitPositionsNodeEditor : ExecutableNodeEditorBase
    {
        private static readonly GUIContent OUTPUT_COUNT = new GUIContent("Output Count", "Number of buffers to distribute positions across.");
        private static readonly GUIContent SEED = new GUIContent("Seed", "Changes which output receives each position.");

        public override void OnGUI(INode node)
        {
            SplitPositionsNode n = node as SplitPositionsNode;
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

            EditorGUI.BeginChangeCheck();
            int seed = EditorGUILayout.IntField(SEED, n.seed);
            if (EditorGUI.EndChangeCheck())
            {
                m_graphEditor.RegisterUndo(n);
                n.seed = seed;
            }
        }
    }
}
#endif
