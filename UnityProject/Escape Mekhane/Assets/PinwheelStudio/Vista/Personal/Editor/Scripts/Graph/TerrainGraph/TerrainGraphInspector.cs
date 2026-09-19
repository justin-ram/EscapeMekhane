#if VISTA
using Pinwheel.Vista.Graph;
using Pinwheel.VistaEditor.UIElements;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pinwheel.VistaEditor.Graph
{
    [CustomEditor(typeof(TerrainDataGraph), true)]
    public class TerrainGraphInspector : Editor
    {
        public delegate void InspectorGuiHandler(TerrainDataGraph target);
        public static event InspectorGuiHandler inspectorGuiCallback;

        private TerrainDataGraph instance;
        private ReadonlyGraphView m_graphView;

        private void OnEnable()
        {
            instance = target as TerrainDataGraph;
        }

        public override void OnInspectorGUI()
        {
            inspectorGuiCallback?.Invoke(instance);
        }

        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement() { name = "inspector-root" };
            root.style.flexGrow = 1;

            root.Add(VistaUI.Spacer(8));
            Button openButton = VistaUI.Button("Open Graph without Biome Context", () =>
            {
                GraphEditorBase.OpenGraph(instance);
                RecordInspectorInteraction();
            }).Primary();
            openButton.tooltip = "Open the graph without a host biome or external inputs such as the biome mask.";
            openButton.style.flexShrink = 0;
            root.Add(openButton);
            root.Add(VistaUI.Spacer(8));

            m_graphView = new ReadonlyGraphView(instance);
            m_graphView.style.minHeight = 800;
            m_graphView.style.flexGrow = 1;
            m_graphView.style.flexShrink = 1;
            m_graphView.RoundedOutline();
            Color32 graphViewBorderColor = new Color32(85, 85, 85, 255);
            m_graphView.style.borderLeftColor = new StyleColor(graphViewBorderColor);
            m_graphView.style.borderTopColor = new StyleColor(graphViewBorderColor);
            m_graphView.style.borderRightColor = new StyleColor(graphViewBorderColor);
            m_graphView.style.borderBottomColor = new StyleColor(graphViewBorderColor);
            root.Add(m_graphView);

            m_graphView.schedule.Execute(() => m_graphView?.FrameGraph()).StartingIn(1);

            if (inspectorGuiCallback != null)
            {
                IMGUIContainer extensionGui = new IMGUIContainer(() =>
                {
                    EditorGUI.BeginChangeCheck();
                    inspectorGuiCallback?.Invoke(instance);
                    if (EditorGUI.EndChangeCheck())
                    {
                        RecordInspectorInteraction();
                    }
                });
                root.Add(extensionGui);
            }

            return root;
        }

        private static void RecordInspectorInteraction()
        {
            SuccessfulActionCounter.Record(
                SuccessfulActionCounter.ActionKeys.ASSET_INSPECTOR_INTERACTION);
        }
    }
}
#endif
