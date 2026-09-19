#if VISTA
using Pinwheel.Vista.Graph;
using UnityEditor;
using UnityEngine;

namespace Pinwheel.VistaEditor.Graph
{
    [NodeEditor(typeof(SamplePointNode))]
    public class SamplePointNodeEditor : ImageNodeEditorBase
    {
        private static readonly GUIContent INDEX = new GUIContent("Index", "Normalized position in the buffer: 0 selects the first sample and 1 selects the last.");

        public override void OnGUI(INode node)
        {
            SamplePointNode samplePointNode = node as SamplePointNode;
            EditorGUI.BeginChangeCheck();
            float index = EditorGUILayout.Slider(INDEX, samplePointNode.index, 0, 1);
            if (EditorGUI.EndChangeCheck())
            {
                m_graphEditor.RegisterUndo(samplePointNode);
                samplePointNode.index = index;
            }
        }
    }
}
#endif
