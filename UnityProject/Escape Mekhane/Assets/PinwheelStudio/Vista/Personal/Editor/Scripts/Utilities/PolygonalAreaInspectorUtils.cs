#if VISTA
using Pinwheel.Vista;
using Pinwheel.Vista.Geometric;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Pinwheel.VistaEditor
{
    internal static class PolygonalAreaInspectorUtils
    {
        private static readonly GUIContent EDIT_ANCHORS = new GUIContent("Edit Anchors");
        private static readonly GUIContent END_EDIT_ANCHORS = new GUIContent("End Editing Anchors");
        private static readonly GUIContent DOWN_ARROW = new GUIContent("▼");
        private static readonly GUIContent FLIP = new GUIContent("Flip");
        private static readonly GUIContent SQUARE = new GUIContent("Square");
        private static readonly GUIContent CIRCLE = new GUIContent("Circle");
        private static readonly GUIContent HEXAGON = new GUIContent("Hexagon");
        private static readonly GUIContent CENTERIZE_PIVOT_POINT = new GUIContent("Centerize Pivot Point");

        public static void EditAnchorButtons(
            IPolygonalArea area,
            bool isEditing,
            Action<bool> editingChanged,
            Action areaChanged = null,
            Func<bool> prepareCenterizePivot = null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorCommon.IndentSpace();
            if (isEditing)
            {
                Rect rect = EditorGUILayout.GetControlRect();
                if (EditorCommon.ConfirmButton(rect, END_EDIT_ANCHORS))
                {
                    editingChanged?.Invoke(false);
                }
            }
            else if (GUILayout.Button(EDIT_ANCHORS, EditorStyles.miniButtonLeft))
            {
                editingChanged?.Invoke(true);
                SceneView.RepaintAll();
            }

            if (GUILayout.Button(DOWN_ARROW, EditorStyles.miniButtonRight, GUILayout.Width(25)))
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(FLIP, false, () => Flip(area, areaChanged));
                menu.AddSeparator(null);
                menu.AddItem(SQUARE, false, () => SetSquare(area, areaChanged));
                menu.AddItem(CIRCLE, false, () => SetCircle(area, areaChanged));
                menu.AddItem(HEXAGON, false, () => SetHexagon(area, areaChanged));
                menu.AddSeparator(null);
                menu.AddItem(CENTERIZE_PIVOT_POINT, false, () =>
                {
                    if (prepareCenterizePivot == null || prepareCenterizePivot())
                    {
                        CenterizePivot(area, areaChanged);
                    }
                });
                menu.ShowAsContext();
            }
            EditorGUILayout.EndHorizontal();
        }

        public static void SnapToButtons(
            IPolygonalArea area,
            Action areaChanged = null,
            Action snapToHexGrid = null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorCommon.IndentSpace();
            if (GUILayout.Button("Snap To...", EditorStyles.miniButton))
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Current Tile"), false, () => SnapToCurrentTile(area, areaChanged));
                menu.AddItem(new GUIContent("Selected Tiles"), false, () => SnapToSelectedTiles(area, areaChanged));
                menu.AddItem(new GUIContent("All Tiles"), false, () => SnapToAllTiles(area, areaChanged));
                if (snapToHexGrid != null)
                {
                    menu.AddItem(new GUIContent("Hex Grid"), false, snapToHexGrid.Invoke);
                }
                menu.ShowAsContext();
            }
            EditorGUILayout.EndHorizontal();
        }

        private static void Flip(IPolygonalArea area, Action changed)
        {
            Vector3[] anchors = area.anchors;
            Array.Reverse(anchors);
            ApplyAnchors(area, anchors, changed);
        }

        private static void SetSquare(IPolygonalArea area, Action changed)
        {
            ApplyAnchors(area, new Vector3[]
            {
                new Vector3(-500, 0, -500),
                new Vector3(-500, 0, 500),
                new Vector3(500, 0, 500),
                new Vector3(500, 0, -500)
            }, changed);
        }

        private static void SetCircle(IPolygonalArea area, Action changed)
        {
            const int segmentCount = 16;
            Vector3[] anchors = new Vector3[segmentCount];
            for (int i = 0; i < segmentCount; ++i)
            {
                float angle = 360f * i / segmentCount * Mathf.Deg2Rad;
                anchors[i] = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 500;
            }
            ApplyAnchors(area, anchors, changed);
        }

        private static void SetHexagon(IPolygonalArea area, Action changed)
        {
            List<Vector3> anchors = new List<Vector3>();
            Hexagon2D hexagon = new Hexagon2D(Vector2.zero, 500, Hexagon2D.Orientation.Top);
            for (int i = 0; i < 6; ++i)
            {
                Vector2 point = hexagon.GetPoint(i);
                anchors.Add(new Vector3(point.x, 0, point.y));
            }
            ApplyAnchors(area, anchors.ToArray(), changed);
        }

        private static void CenterizePivot(IPolygonalArea area, Action changed)
        {
            Vector3[] anchors = area.anchors;
            if (anchors.Length == 0)
            {
                return;
            }

            Vector3 pivotOffset = Vector3.zero;
            for (int i = 0; i < anchors.Length; ++i)
            {
                pivotOffset += anchors[i];
            }
            pivotOffset /= anchors.Length;
            pivotOffset.y = 0;

            Object target = area as Object;
            Undo.RecordObject(area.transform, $"Modify {target.name}");
            Undo.RecordObject(target, $"Modify {target.name}");
            for (int i = 0; i < anchors.Length; ++i)
            {
                anchors[i] -= pivotOffset;
                anchors[i].y = 0;
            }
            area.transform.position = area.transform.TransformPoint(pivotOffset);
            area.anchors = anchors;
            EditorUtility.SetDirty(target);
            SceneView.RepaintAll();
            changed?.Invoke();
        }

        private static void SnapToCurrentTile(IPolygonalArea area, Action changed)
        {
            List<ITile> tiles = GetAllTiles();
            Vector3 position = area.transform.position;
            for (int i = 0; i < tiles.Count; ++i)
            {
                Bounds bounds = tiles[i].worldBounds;
                if (position.x >= bounds.min.x && position.x <= bounds.max.x &&
                    position.z >= bounds.min.z && position.z <= bounds.max.z)
                {
                    SnapToBounds(area, bounds, changed);
                    return;
                }
            }
        }

        private static void SnapToSelectedTiles(IPolygonalArea area, Action changed)
        {
            List<ITile> tiles = new List<ITile>();
            GameObject[] selectedObjects = Selection.gameObjects;
            for (int i = 0; i < selectedObjects.Length; ++i)
            {
                ITile tile = selectedObjects[i].GetComponent<ITile>();
                if (tile != null)
                {
                    tiles.Add(tile);
                }
            }
            SnapToTiles(area, tiles, changed);
        }

        private static void SnapToAllTiles(IPolygonalArea area, Action changed)
        {
            SnapToTiles(area, GetAllTiles(), changed);
        }

        private static List<ITile> GetAllTiles()
        {
            return Utilities.FindObjectsNoSortCompat<MonoBehaviour>()
                .OfType<ITile>()
                .ToList();
        }

        private static void SnapToTiles(IPolygonalArea area, List<ITile> tiles, Action changed)
        {
            if (tiles.Count == 0)
            {
                return;
            }

            Bounds bounds = tiles[0].worldBounds;
            for (int i = 1; i < tiles.Count; ++i)
            {
                bounds.Encapsulate(tiles[i].worldBounds);
            }
            SnapToBounds(area, bounds, changed);
        }

        private static void SnapToBounds(IPolygonalArea area, Bounds bounds, Action changed)
        {
            Object target = area as Object;
            Undo.RecordObject(area.transform, $"Modify {target.name}");
            Undo.RecordObject(target, $"Modify {target.name}");
            area.transform.position = new Vector3(bounds.center.x, 0, bounds.center.z);
            area.anchors = new Vector3[]
            {
                area.transform.InverseTransformPoint(new Vector3(bounds.min.x, 0, bounds.min.z)),
                area.transform.InverseTransformPoint(new Vector3(bounds.min.x, 0, bounds.max.z)),
                area.transform.InverseTransformPoint(new Vector3(bounds.max.x, 0, bounds.max.z)),
                area.transform.InverseTransformPoint(new Vector3(bounds.max.x, 0, bounds.min.z))
            };
            EditorUtility.SetDirty(target);
            SceneView.RepaintAll();
            changed?.Invoke();
        }

        private static void ApplyAnchors(IPolygonalArea area, Vector3[] anchors, Action changed)
        {
            Object target = area as Object;
            Undo.RecordObject(target, $"Modify {target.name} anchors");
            area.anchors = anchors;
            EditorUtility.SetDirty(target);
            SceneView.RepaintAll();
            changed?.Invoke();
        }
    }
}
#endif
