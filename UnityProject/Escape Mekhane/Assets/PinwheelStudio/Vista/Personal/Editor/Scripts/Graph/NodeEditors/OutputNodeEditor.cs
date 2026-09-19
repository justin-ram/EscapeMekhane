#if VISTA
using Pinwheel.Vista.Graph;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pinwheel.VistaEditor.Graph
{
    [NodeEditor(typeof(OutputNode))]
    public class OutputNodeEditor : ExecutableNodeEditorBase, INeedUpdateNodeVisual
    {
        private static readonly GUIContent NAME = new GUIContent("Name", "Name of the output");
        private static readonly GUIContent SLOT_TYPE = new GUIContent("Slot Type", "Data type of the slot");

        private static List<NameSelectorEntry> s_nameSelector;
        public static List<NameSelectorEntry> nameSelector
        {
            get
            {
                if (s_nameSelector == null)
                {
                    s_nameSelector = new List<NameSelectorEntry>();
                }
                return s_nameSelector;
            }
        }

        public void UpdateVisual(INode node, NodeView nv)
        {
            OutputNode n = node as OutputNode;
            PortView pv = nv.Q<PortView>();
            if (pv != null)
            {
                pv.portName = !string.IsNullOrEmpty(n.outputName) ? n.outputName : "(not set)";
            }
        }

        public override void OnGUI(INode node)
        {
            OutputNode n = node as OutputNode;
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            string outputName = EditorGUILayout.DelayedTextField(NAME, n.outputName);
            if (nameSelector.Count > 0)
            {
                Rect dropDownRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, EditorStyles.popup, GUILayout.Width(20));
                if (GUI.Button(dropDownRect, "", EditorStyles.popup))
                {
                    GenericMenu menu = new GenericMenu();
                    foreach (NameSelectorEntry s in nameSelector)
                    {
                        menu.AddItem(
                            new GUIContent(s.name),
                            false,
                            () =>
                            {
                                string candidateName = s.name;
                                if (!OutputNode.IsOutputNameAllowed(candidateName))
                                {
                                    ShowReservedOutputNameError(candidateName);
                                    candidateName = string.Empty;
                                }
                                else if (!IsOutputNameUnique(n, candidateName))
                                {
                                    ShowDuplicateOutputNameError(candidateName);
                                    candidateName = string.Empty;
                                }
                                m_graphEditor.RegisterUndo(n);
                                n.outputName = candidateName;
                                n.SetSlotType(s.slotType);
                                m_graphEditor.UpdateNodesVisual();
                            });
                        menu.DropDown(dropDownRect);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            List<Type> slotTypes = SlotProvider.GetAllSlotTypes();
            int selectedTypeIndex = slotTypes.IndexOf(n.slotType);
            string[] slotTypeLabels = new string[slotTypes.Count];
            for (int i = 0; i < slotTypes.Count; ++i)
            {
                slotTypeLabels[i] = ObjectNames.NicifyVariableName(slotTypes[i].Name);
            }
            selectedTypeIndex = EditorGUILayout.Popup(SLOT_TYPE, selectedTypeIndex, slotTypeLabels);
            if (EditorGUI.EndChangeCheck())
            {
                if (!OutputNode.IsOutputNameAllowed(outputName))
                {
                    ShowReservedOutputNameError(outputName);
                    outputName = string.Empty;
                }
                else if (!IsOutputNameUnique(n, outputName))
                {
                    ShowDuplicateOutputNameError(outputName);
                    outputName = string.Empty;
                }
                m_graphEditor.RegisterUndo(n);
                n.outputName = outputName;
                if (selectedTypeIndex >= 0 && selectedTypeIndex < slotTypes.Count)
                {
                    n.SetSlotType(slotTypes[selectedTypeIndex]);
                }
                else
                {
                    n.SetSlotType(slotTypes[0]);
                }
            }
        }

        private bool IsOutputNameUnique(OutputNode targetNode, string outputName)
        {
            if (string.IsNullOrEmpty(outputName))
                return true;

            List<OutputNode> outputNodes = m_graphEditor.clonedGraph.GetNodesOfType<OutputNode>();
            for (int i = 0; i < outputNodes.Count; ++i)
            {
                OutputNode outputNode = outputNodes[i];
                if (!ReferenceEquals(outputNode, targetNode) &&
                    string.Equals(outputNode.outputName, outputName, StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }

        private static void ShowDuplicateOutputNameError(string outputName)
        {
            EditorUtility.DisplayDialog(
                "Duplicate Output Name",
                $"A generic output named '{outputName}' already exists in this graph. Generic output names must be unique.",
                "OK");
        }

        private static void ShowReservedOutputNameError(string outputName)
        {
            EditorUtility.DisplayDialog(
                "Reserved Output Name",
                $"'{outputName}' is reserved for graph input and cannot be used as an output name.",
                "OK");
        }
    }
}
#endif
