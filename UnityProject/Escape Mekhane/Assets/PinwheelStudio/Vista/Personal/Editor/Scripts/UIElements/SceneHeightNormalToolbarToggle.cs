#if VISTA
#if UNITY_2021_2_OR_NEWER
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using Pinwheel.Vista;
using System.Net.Mime;

namespace Pinwheel.VistaEditor.UIElements
{
    [InitializeOnLoad]
    [EditorToolbarElement(ID, typeof(SceneView))]
    public class SceneHeightNormalToolbarToggle : EditorToolbarToggle
    {
        public const string ID = "vista-scene-height-normal-toggle";

        private const float MAX_RAYCAST_DISTANCE = 1000000f;
        private const float LABEL_OFFSET = 16f;
        private const float LABEL_WIDTH = 120f;

        private static bool s_isEnabled;

        static SceneHeightNormalToolbarToggle()
        {
            SceneViewOverlay.populateItemCallback += PopulateSceneViewOverlay;
            SceneView.duringSceneGui += DuringSceneViewGUI;
        }

        public SceneHeightNormalToolbarToggle()
        {
            text = "";
            onIcon = Resources.Load<Texture2D>("Vista/Textures/HeightNormalProbe");
            offIcon = onIcon;
            tooltip = "Measure scene height and surface steepness";
            value = s_isEnabled;
        }

        protected override void ToggleValue()
        {
            base.ToggleValue();
            s_isEnabled = value;
            SceneView.RepaintAll();
        }

        private static void PopulateSceneViewOverlay(Collector<string> elementIds)
        {
            elementIds.Add(ID);
        }

        private static void DuringSceneViewGUI(SceneView sceneView)
        {
            if (!s_isEnabled || Event.current == null)
                return;

            if (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDrag)
            {
                sceneView.Repaint();
            }

            if (Event.current.type != EventType.Repaint)
                return;

            Vector2 mousePosition = Event.current.mousePosition;
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, MAX_RAYCAST_DISTANCE, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return;

            float steepness = Vector3.Angle(hit.normal, Vector3.up);
            DrawHitMarker(hit);
            DrawMeasurement(sceneView, mousePosition, hit.point.y, steepness);
        }

        private static void DrawHitMarker(RaycastHit hit)
        {
            Color oldColor = Handles.color;
            Handles.color = new Color(0.25f, 0.85f, 1f, 1f);
            float handleSize = HandleUtility.GetHandleSize(hit.point);
            Handles.DrawWireDisc(hit.point, hit.normal, handleSize * 0.2f);
            Handles.ArrowHandleCap(
                0,
                hit.point,
                Quaternion.LookRotation(hit.normal),
                handleSize * 0.4f,
                EventType.Repaint);
            Handles.color = oldColor;
        }

        private static void DrawMeasurement(SceneView sceneView, Vector2 mousePosition, float height, float steepness)
        {
            GUIContent content = new GUIContent($"Height: {height:0.00}m\nSteepness: {steepness:0.0}*");
            GUIStyle style = EditorStyles.label;
            Vector2 boxSize = new Vector2(
                LABEL_WIDTH,
                EditorGUIUtility.singleLineHeight * 2f);

            float x = Mathf.Clamp(mousePosition.x + LABEL_OFFSET, 0f, Mathf.Max(0f, sceneView.position.width - boxSize.x));
            float y = Mathf.Clamp(mousePosition.y + LABEL_OFFSET, 0f, Mathf.Max(0f, sceneView.position.height - boxSize.y));

            Handles.BeginGUI();
            GUI.Box(new Rect(new Vector2(x, y), boxSize), content);
            Handles.EndGUI();
        }
    }
}
#endif
#endif
