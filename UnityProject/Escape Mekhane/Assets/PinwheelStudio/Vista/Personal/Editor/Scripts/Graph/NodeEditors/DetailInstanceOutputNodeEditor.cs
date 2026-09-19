#if VISTA
using Pinwheel.Vista;
using Pinwheel.Vista.Graph;
using UnityEditor;
using UnityEngine;

namespace Pinwheel.VistaEditor.Graph
{
    [NodeEditor(typeof(DetailInstanceOutputNode))]
    public class DetailInstanceOutputNodeEditor : InstanceOutputNodeEditorBase, INeedUpdateNodeVisual
    {
        private static readonly GUIContent DETAIL_TEMPLATE = new GUIContent("Detail Template", "Template for the detail");
        private static readonly string VEGETATION_ASSETS_TEXT = "Add vegetation assets";

        public void UpdateVisual(INode node, NodeView nodeView)
        {
            DetailInstanceOutputNode outputNode = node as DetailInstanceOutputNode;
            nodeView.SetPreviewImage(GetPreview(outputNode.detailTemplate));
        }

        public override void OnGUI(INode node)
        {
            DetailInstanceOutputNode outputNode = node as DetailInstanceOutputNode;
            EditorGUI.BeginChangeCheck();
            DetailTemplate template = EditorGUILayout.ObjectField(DETAIL_TEMPLATE, outputNode.detailTemplate, typeof(DetailTemplate), false) as DetailTemplate;
            if (EditorGUI.EndChangeCheck())
            {
                m_graphEditor.RegisterUndo(outputNode);
                outputNode.detailTemplate = template;
            }
            base.OnGUI(node);

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
