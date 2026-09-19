#if VISTA
using Pinwheel.Vista;
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Pinwheel.VistaEditor
{
    internal sealed class PolygonalAreaSceneGuiUtils
    {
        private const float ANCHOR_SIZE = 0.1f;
        private static readonly Color ANCHOR_COLOR = Color.white;
        private static readonly Color SEGMENT_COLOR = Color.red;
        private static readonly Color FALLOFF_COLOR = new Color(1, 0, 0, 0.5f);

        private readonly IPolygonalArea m_area;
        private readonly Object m_target;
        private bool m_editingChanged;

        public event Action changed;

        public PolygonalAreaSceneGuiUtils(IPolygonalArea area)
        {
            m_area = area;
            m_target = area as Object;
        }

        public void Dispose()
        {
            Tools.hidden = false;
        }

        public void DrawSceneGUI(bool editMode, float alphaMul = 1)
        {
            Vector3[] anchors = m_area.anchors;
            AnchorUtilities.Transform(anchors, m_area.transform.localToWorldMatrix);

            if (anchors.Length > 1)
            {
                CompareFunction oldZTest = Handles.zTest;
                Handles.zTest = editMode ? CompareFunction.Always : CompareFunction.LessEqual;

                if (m_area is IPolygonalAreaWithFalloff areaWithFalloff)
                {
                    Vector3[] falloffAnchors = areaWithFalloff.falloffAnchors;
                    AnchorUtilities.Transform(falloffAnchors, m_area.transform.localToWorldMatrix);
                    DrawPolygon(falloffAnchors, FALLOFF_COLOR, alphaMul);
                }
                DrawPolygon(anchors, SEGMENT_COLOR, alphaMul);

                Handles.zTest = oldZTest;
            }

            if (!editMode || Event.current.alt)
            {
                Tools.hidden = false;
                return;
            }

            Tools.hidden = true;
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            EditorGUI.BeginChangeCheck();
            for (int i = 0; i < anchors.Length; ++i)
            {
                anchors[i] = Handles.PositionHandle(anchors[i], Quaternion.identity);
            }

            for (int i = 0; i < anchors.Length; ++i)
            {
                Color color = ANCHOR_COLOR;
                color.a *= alphaMul;
                Handles.color = color;
                float size = HandleUtility.GetHandleSize(anchors[i]) * ANCHOR_SIZE;
                if (Handles.Button(anchors[i], Quaternion.identity, size, size, Handles.CubeHandleCap))
                {
                    if (Event.current.control && Event.current.button == 0)
                    {
                        anchors = AnchorUtilities.RemoveAt(anchors, i);
                        GUI.changed = true;
                    }
                    Event.current.Use();
                }
            }

            if (Event.current.shift && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    anchors = AnchorUtilities.Insert(anchors, hit.point);
                    GUI.changed = true;
                }
                else
                {
                    Plane plane = new Plane(Vector3.up, m_area.transform.position);
                    if (plane.Raycast(ray, out float distance))
                    {
                        anchors = AnchorUtilities.Insert(anchors, ray.GetPoint(distance));
                        GUI.changed = true;
                    }
                }
                Event.current.Use();
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(m_target, $"Modify {m_target.name} anchors");
                AnchorUtilities.FlattenY(anchors, 0);
                AnchorUtilities.Transform(anchors, m_area.transform.worldToLocalMatrix);
                m_area.anchors = anchors;
                EditorUtility.SetDirty(m_target);
                GUI.changed = true;
                m_editingChanged = true;
            }

            CatchHotControl();
            if (m_editingChanged && GUIUtility.hotControl == 0)
            {
                m_editingChanged = false;
                changed?.Invoke();
            }
        }

        public void DrawBounds(float maxHeight)
        {
            Vector3[] sourceAnchors = m_area.anchors;
            if (m_area is IPolygonalAreaWithFalloff areaWithFalloff &&
                areaWithFalloff.falloffDirection == FalloffDirection.Outer)
            {
                sourceAnchors = areaWithFalloff.falloffAnchors;
            }
            if (sourceAnchors == null || sourceAnchors.Length < 2)
            {
                return;
            }

            Vector3[] worldAnchors = (Vector3[])sourceAnchors.Clone();
            AnchorUtilities.Transform(worldAnchors, m_area.transform.localToWorldMatrix);

            float y0 = m_area.transform.position.y;
            float y1 = y0 + maxHeight * m_area.transform.lossyScale.y;
            float minY = Mathf.Min(y0, y1);
            float maxY = Mathf.Max(y0, y1);
            Color faceColor = new Color(SEGMENT_COLOR.r, SEGMENT_COLOR.g, SEGMENT_COLOR.b, 0.025f);
            Color outlineColor = new Color(SEGMENT_COLOR.r, SEGMENT_COLOR.g, SEGMENT_COLOR.b, 0.5f);

            using (new Handles.DrawingScope(Color.white, Matrix4x4.identity))
            {
                CompareFunction oldZTest = Handles.zTest;
                Handles.zTest = CompareFunction.LessEqual;
                Vector3[] vertices = new Vector3[4];
                for (int i = 0; i < worldAnchors.Length; ++i)
                {
                    Vector3 pointA = worldAnchors[i];
                    Vector3 pointB = worldAnchors[(i + 1) % worldAnchors.Length];
                    vertices[0] = new Vector3(pointA.x, minY, pointA.z);
                    vertices[1] = new Vector3(pointA.x, maxY, pointA.z);
                    vertices[2] = new Vector3(pointB.x, maxY, pointB.z);
                    vertices[3] = new Vector3(pointB.x, minY, pointB.z);
                    Handles.DrawSolidRectangleWithOutline(vertices, faceColor, outlineColor);
                }
                Handles.zTest = oldZTest;
            }
        }

        private static void DrawPolygon(Vector3[] points, Color color, float alphaMul)
        {
            color.a *= alphaMul;
            Handles.color = color;
            Handles.DrawPolyLine(points[0], points[points.Length - 1]);
            Handles.DrawPolyLine(points);
        }

        private void CatchHotControl()
        {
            int controlId = GUIUtility.GetControlID(GetHashCode(), FocusType.Passive);
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                GUIUtility.hotControl = controlId;
            }
            else if (Event.current.type == EventType.MouseUp && GUIUtility.hotControl == controlId)
            {
                GUIUtility.hotControl = 0;
            }
        }
    }
}
#endif
