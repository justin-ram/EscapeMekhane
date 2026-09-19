#if VISTA
using Pinwheel.Vista;
using Pinwheel.Vista.Graph;
using UnityEditor;
using UnityEngine;

namespace Pinwheel.VistaEditor.Graph
{
    [NodeEditor(typeof(DetailDensitySlotOutputNode))]
    public class DetailDensitySlotOutputNodeEditor : ImageNodeEditorBase, INeedUpdateNodeVisual
    {
        private static readonly GUIContent SLOT_NAME = new GUIContent("Slot Name", "Name displayed by terrain tools for this detail slot.");
        private static readonly GUIContent DEFAULT_DETAIL_TEMPLATE = new GUIContent("Default Detail Template", "Detail template used when the terrain tool does not provide a selection for this slot.");
        private static readonly GUIContent DENSITY_MULTIPLIER = new GUIContent("Density Multiplier", "Multiplier for detail density.");

        public void UpdateVisual(INode node, NodeView nodeView)
        {
            DetailDensitySlotOutputNode outputNode = node as DetailDensitySlotOutputNode;
            nodeView.SetPreviewImage(GetPreview(outputNode.detailTemplate));
        }

        public override void OnGUI(INode node)
        {
            DetailDensitySlotOutputNode outputNode = node as DetailDensitySlotOutputNode;
            EditorGUI.BeginChangeCheck();
            string slotName = EditorGUILayout.TextField(SLOT_NAME, outputNode.slotName);
            DetailTemplate detailTemplate = EditorGUILayout.ObjectField(DEFAULT_DETAIL_TEMPLATE, outputNode.detailTemplate, typeof(DetailTemplate), false) as DetailTemplate;
            float densityMultiplier = EditorGUILayout.Slider(DENSITY_MULTIPLIER, outputNode.densityMultiplier, 0f, 2f);
            if (EditorGUI.EndChangeCheck())
            {
                m_graphEditor.RegisterUndo(outputNode);
                outputNode.slotName = slotName;
                outputNode.detailTemplate = detailTemplate;
                outputNode.densityMultiplier = densityMultiplier;
            }
        }

        private static Texture GetPreview(DetailTemplate template)
        {
            if (template == null)
                return null;
            if (template.renderMode == DetailRenderMode.VertexLit)
                return template.prefab != null ? AssetPreview.GetAssetPreview(template.prefab) : null;
            return template.texture != null ? AssetPreview.GetAssetPreview(template.texture) : null;
        }
    }
}
#endif
