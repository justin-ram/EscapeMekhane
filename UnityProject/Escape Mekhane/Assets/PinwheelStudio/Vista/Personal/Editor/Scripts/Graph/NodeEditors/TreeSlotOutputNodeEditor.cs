#if VISTA
using Pinwheel.Vista;
using Pinwheel.Vista.Graph;
using UnityEditor;
using UnityEngine;

namespace Pinwheel.VistaEditor.Graph
{
    [NodeEditor(typeof(TreeSlotOutputNode))]
    public class TreeSlotOutputNodeEditor : InstanceOutputNodeEditorBase, INeedUpdateNodeVisual
    {
        private static readonly GUIContent SLOT_NAME = new GUIContent("Slot Name", "Name displayed by terrain tools for this tree slot.");
        private static readonly GUIContent DEFAULT_TREE_TEMPLATE = new GUIContent("Default Tree Template", "Tree template used when the terrain tool does not provide a selection for this slot.");

        public void UpdateVisual(INode node, NodeView nodeView)
        {
            TreeSlotOutputNode outputNode = node as TreeSlotOutputNode;
            Texture preview = null;
            if (outputNode.treeTemplate != null && outputNode.treeTemplate.prefab != null)
            {
                preview = AssetPreview.GetAssetPreview(outputNode.treeTemplate.prefab);
            }
            nodeView.SetPreviewImage(preview);
        }

        public override void OnGUI(INode node)
        {
            TreeSlotOutputNode outputNode = node as TreeSlotOutputNode;
            EditorGUI.BeginChangeCheck();
            string slotName = EditorGUILayout.TextField(SLOT_NAME, outputNode.slotName);
            TreeTemplate treeTemplate = EditorGUILayout.ObjectField(
                DEFAULT_TREE_TEMPLATE,
                outputNode.treeTemplate,
                typeof(TreeTemplate),
                false) as TreeTemplate;
            if (EditorGUI.EndChangeCheck())
            {
                m_graphEditor.RegisterUndo(outputNode);
                outputNode.slotName = slotName;
                outputNode.treeTemplate = treeTemplate;
            }
            base.OnGUI(node);
        }
    }
}
#endif
