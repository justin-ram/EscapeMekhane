#if VISTA
using Pinwheel.Vista;
using Pinwheel.Vista.Graph;
using UnityEditor;
using UnityEngine;

namespace Pinwheel.VistaEditor.Graph
{
    [NodeEditor(typeof(DetailDensityOutputNode))]
    public class DetailDensityOutputNodeEditor : ImageNodeEditorBase, INeedUpdateNodeVisual
    {
        private static readonly GUIContent DETAIL_TEMPLATE = new GUIContent("Detail Template", "Template asset for this detail. Right click on the Project window, select Vista>Detail Template to create one");
        private static readonly GUIContent DENSITY_MULTIPLIER = new GUIContent("Density Multiplier", "Multiplier for detail density");
        private static readonly string VEGETATION_ASSETS_TEXT = "Add vegetation assets";

        public void UpdateVisual(INode node, NodeView nodeView)
        {
            DetailDensityOutputNode outputNode = node as DetailDensityOutputNode;
            nodeView.SetPreviewImage(GetPreview(outputNode.detailTemplate));
        }

        public override void OnGUI(INode node)
        {
            DetailDensityOutputNode outputNode = node as DetailDensityOutputNode;
            EditorGUI.BeginChangeCheck();
            DetailTemplate detailTemplate = EditorGUILayout.ObjectField(DETAIL_TEMPLATE, outputNode.detailTemplate, typeof(DetailTemplate), false) as DetailTemplate;
            float densityMultiplier = EditorGUILayout.Slider(DENSITY_MULTIPLIER, outputNode.densityMultiplier, 0f, 2f);
            if (EditorGUI.EndChangeCheck())
            {
                m_graphEditor.RegisterUndo(outputNode);
                outputNode.detailTemplate = detailTemplate;
                outputNode.densityMultiplier = densityMultiplier;
            }

            if (EditorGUILayout.LinkButton(VEGETATION_ASSETS_TEXT))
            {
                NetUtils.TrackClick("vegetation-aff", UILocation.NodeEditor);
                Application.OpenURL(Links.VEGETATION_ASSETS);
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
