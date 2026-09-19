#if VISTA
using Pinwheel.Vista.Graph;
using UnityEditor;
using UnityEngine;

namespace Pinwheel.VistaEditor.Graph
{
    [NodeEditor(typeof(CurvatureNode))]
    public class CurvatureNodeEditor : ImageNodeEditorBase
    {
        private static readonly GUIContent RADIUS = new GUIContent("Radius", "Size of the neighborhood in world meters. Each pixel is compared against the average height of this area.");
        private static readonly GUIContent MAX_DEVIATION = new GUIContent("Max Deviation", "Height difference in world meters that maps to a fully white mask. Lower values make the mask stronger.");

        public override void OnGUI(INode node)
        {
            CurvatureNode n = node as CurvatureNode;
            EditorGUI.BeginChangeCheck();
            float radius = EditorGUILayout.Slider(RADIUS, n.radius, CurvatureNode.MIN_RADIUS, CurvatureNode.MAX_RADIUS);
            float maxDeviation = EditorGUILayout.Slider(MAX_DEVIATION, n.maxDeviation, CurvatureNode.MIN_MAX_DEVIATION, 20f);
            if (EditorGUI.EndChangeCheck())
            {
                m_graphEditor.RegisterUndo(n);
                n.radius = radius;
                n.maxDeviation = maxDeviation;
            }
        }
    }
}
#endif
