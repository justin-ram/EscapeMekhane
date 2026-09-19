#if VISTA
using Pinwheel.Vista.Graph;
using UnityEditor;
using UnityEngine;
using Pinwheel.VistaEditor;
using UnityEditor.Graphs;

namespace Pinwheel.VistaEditor.Graph
{
    [NodeEditor(typeof(MissingNode))]
    public class MissingNodeEditor : ExecutableNodeEditorBase, INeedUpdateNodeVisual
    {
        private static readonly GUIContent COPY_JSON = new GUIContent("Copy Preserved JSON");

        public override void OnGUI(INode node)
        {
            MissingNode missingNode = node as MissingNode;
            string message = $"Missing node type: {missingNode.missingTypeName}.\nData is preserved and this node will be restored when the containing module is installed.";

            EditorGUILayout.LabelField(message, EditorCommon.Styles.p1);

            if (GUILayout.Button(COPY_JSON))
            {
                EditorGUIUtility.systemCopyBuffer = missingNode.preservedSerializedData.ToString() ?? string.Empty;
            }
        }

        public void UpdateVisual(INode node, NodeView nv)
        {
            MissingNode missingNode = node as MissingNode;
            string missingTypeName = missingNode != null ? missingNode.missingTypeName : string.Empty;
            string shortTypeName = missingTypeName;

            if (!string.IsNullOrEmpty(missingTypeName))
            {
                int lastDotIndex = missingTypeName.LastIndexOf('.');
                if (lastDotIndex >= 0 && lastDotIndex < missingTypeName.Length - 1)
                {
                    shortTypeName = missingTypeName.Substring(lastDotIndex + 1);
                }
            }

            if (!string.IsNullOrEmpty(shortTypeName))
            {
                nv.title = $"Missing ({shortTypeName})";
            }
            else
            {
                nv.title = "Missing Node";
            }
        }
    }
}
#endif
