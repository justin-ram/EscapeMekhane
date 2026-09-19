#if VISTA
using UnityEngine;
using UnityEditor;
using Pinwheel.VistaEditor;
using Pinwheel.Vista.Graph;

namespace Pinwheel.VistaEditor.Graph
{
    [NodeEditor(typeof(ScatterNode))]
    public class ScatterNodeEditor : ExecutableNodeEditorBase
    {
        private static readonly GUIContent SPACING = new GUIContent("Spacing", "Distance between base points in world meters");
        private static readonly GUIContent SPREAD_COUNT = new GUIContent("Spread Count", "Number of jittered copies spread from each base point");
        private static readonly GUIContent SPREAD_DISTANCE = new GUIContent("Spread Distance", "How far the copies scatter from their base point");
        private static readonly GUIContent KEEP_SOURCE_POINTS = new GUIContent("Keep Source Points", "Should it include the base points in the result?");
        private static readonly GUIContent DENSITY_MULTIPLIER = new GUIContent("Density Multiplier", "Scales the density mask. The blacker the mask, the higher the chance a point is removed");
        private static readonly GUIContent SEED = new GUIContent("Seed", "An integer to randomize the result");

        public override void OnGUI(INode node)
        {
            ScatterNode n = node as ScatterNode;
            EditorGUI.BeginChangeCheck();
            Vector2 spacing = EditorCommon.InlineVector2Field(SPACING, n.spacing);
            int spreadCount = EditorGUILayout.IntSlider(SPREAD_COUNT, n.spreadCount, 0, 15);
            float spreadDistance = EditorGUILayout.Slider(SPREAD_DISTANCE, n.spreadDistance, 0f, 1f);
            bool keepSourcePoints = EditorGUILayout.Toggle(KEEP_SOURCE_POINTS, n.keepSourcePoints);
            float densityMultiplier = EditorGUILayout.Slider(DENSITY_MULTIPLIER, n.densityMultiplier, 0f, 2f);
            int seed = EditorGUILayout.IntField(SEED, n.seed);
            if (EditorGUI.EndChangeCheck())
            {
                m_graphEditor.RegisterUndo(n);
                n.spacing = spacing;
                n.spreadCount = spreadCount;
                n.spreadDistance = spreadDistance;
                n.keepSourcePoints = keepSourcePoints;
                n.densityMultiplier = densityMultiplier;
                n.seed = seed;
            }
        }
    }
}
#endif
