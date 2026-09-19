#if VISTA
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using Pinwheel.VistaEditor;
using Pinwheel.Vista.Graph;
using Pinwheel.VistaEditor.Graph;

namespace Pinwheel.VistaEditor.Graph
{
    [NodeEditor(typeof(GenericMountainNode))]
    public class GenericMountainNodeEditor : ImageNodeEditorBase
    {
        private static readonly GUIContent MOUNTAIN_SCALE = new GUIContent("Mountain Scale", "Size of the mountains and their details");
        private static readonly GUIContent PEAK_VARIATION_SCALE = new GUIContent("Peak Variation Scale", "Size of the smooth pattern that makes the peaks reach different heights");
        private static readonly GUIContent SEED = new GUIContent("Seed", "Randomize the result with an integer");
        private static readonly GUIContent MIN_HEIGHT = new GUIContent("Min Height", "Height of the lowest point of the terrain");
        private static readonly GUIContent MAX_HEIGHT = new GUIContent("Max Height", "Height of the highest point of the terrain");

        public override void OnGUI(INode node)
        {
            GenericMountainNode n = node as GenericMountainNode;
            EditorGUI.BeginChangeCheck();
            float mountainScale = EditorGUILayout.FloatField(MOUNTAIN_SCALE, n.mountainScale);
            float peakVariationScale = EditorGUILayout.FloatField(PEAK_VARIATION_SCALE, n.peakVariationScale);
            int seed = EditorGUILayout.IntField(SEED, n.seed);
            float minHeight = EditorGUILayout.Slider(MIN_HEIGHT, n.minHeight, 0f, 1f);
            float maxHeight = EditorGUILayout.Slider(MAX_HEIGHT, n.maxHeight, 0f, 1f);

            if (EditorGUI.EndChangeCheck())
            {
                m_graphEditor.RegisterUndo(n);
                n.mountainScale = mountainScale;
                n.peakVariationScale = peakVariationScale;
                n.seed = seed;
                n.minHeight = minHeight;
                n.maxHeight = maxHeight;
            }
        }
    }
}
#endif
