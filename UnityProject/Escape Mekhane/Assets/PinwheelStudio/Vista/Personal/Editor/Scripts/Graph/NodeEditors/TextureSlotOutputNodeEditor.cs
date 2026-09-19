#if VISTA
using Pinwheel.Vista;
using Pinwheel.Vista.Graph;
using UnityEditor;
using UnityEngine;

namespace Pinwheel.VistaEditor.Graph
{
    [NodeEditor(typeof(TextureSlotOutputNode))]
    public class TextureSlotOutputNodeEditor : ImageNodeEditorBase, INeedUpdateNodeVisual
    {
        private static readonly GUIContent SLOT_NAME = new GUIContent("Slot Name", "Name displayed by terrain tools for this texture slot.");
        private static readonly GUIContent DEFAULT_TERRAIN_LAYER = new GUIContent("Default Terrain Layer", "Terrain layer used when the terrain tool does not provide a selection for this slot.");
        private static readonly GUIContent ORDER = new GUIContent("Order", "Sorting order of this layer in the graph output.");

        public void UpdateVisual(INode node, NodeView nodeView)
        {
            TextureSlotOutputNode outputNode = node as TextureSlotOutputNode;
            Texture preview = null;
            if (outputNode.terrainLayer != null && outputNode.terrainLayer.diffuseTexture != null)
            {
                preview = AssetPreview.GetAssetPreview(outputNode.terrainLayer.diffuseTexture);
            }
            nodeView.SetPreviewImage(preview);
        }

        public override void OnGUI(INode node)
        {
            TextureSlotOutputNode outputNode = node as TextureSlotOutputNode;
            EditorGUI.BeginChangeCheck();
            string slotName = EditorGUILayout.TextField(SLOT_NAME, outputNode.slotName);
            TerrainLayer defaultTerrainLayer = EditorGUILayout.ObjectField(
                DEFAULT_TERRAIN_LAYER,
                outputNode.terrainLayer,
                typeof(TerrainLayer),
                false) as TerrainLayer;
            int order = EditorGUILayout.IntField(ORDER, outputNode.order);
            if (EditorGUI.EndChangeCheck())
            {
                m_graphEditor.RegisterUndo(outputNode);
                outputNode.slotName = slotName;
                outputNode.terrainLayer = defaultTerrainLayer;
                outputNode.order = order;
            }
        }
    }
}
#endif
