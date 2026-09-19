#if VISTA
using Pinwheel.Vista;
using UnityEditor;
using UnityEngine;

namespace Pinwheel.VistaEditor
{
    [CustomEditor(typeof(TerrainSeal))]
    public class TerrainSealInspector : Editor
    {
        private PolygonalAreaSceneGuiUtils m_polygonalSceneGui;
        private VistaManager m_manager;
        private bool m_isEditingAnchors;

        private void OnEnable()
        {
            m_polygonalSceneGui = new PolygonalAreaSceneGuiUtils(target as IPolygonalArea);
            VistaManager[] managers = Utilities.FindObjectsNoSortCompat<VistaManager>();
            m_manager = managers.Length > 0 ? managers[0] : null;
        }

        private void OnDisable()
        {
            m_polygonalSceneGui.Dispose();
        }

        public override void OnInspectorGUI()
        {
            TerrainSeal seal = target as TerrainSeal;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.BoundsField("Bounds", seal.worldBounds);
            }

            if (m_isEditingAnchors)
            {
                EditorGUILayout.LabelField(
                    "- Use arrow gizmos to move an anchor.\n" +
                    "- Shift Click to add a new anchor between the 2 nearest ones.\n" +
                    "- Ctrl Click on an anchor to remove it.",
                    EditorCommon.Styles.infoLabel);
            }

            PolygonalAreaInspectorUtils.EditAnchorButtons(
                area: seal,
                isEditing: m_isEditingAnchors,
                editingChanged: SetAnchorEditing,
                areaChanged: Repaint);
            using (new EditorGUI.DisabledScope(m_isEditingAnchors))
            {
                PolygonalAreaInspectorUtils.SnapToButtons(
                    area: seal,
                    areaChanged: Repaint);
            }
        }

        private void OnSceneGUI()
        {
            m_polygonalSceneGui.DrawBounds(m_manager != null ? m_manager.terrainMaxHeight : 0);
            m_polygonalSceneGui.DrawSceneGUI(m_isEditingAnchors);
        }

        private void SetAnchorEditing(bool isEditing)
        {
            m_isEditingAnchors = isEditing;
        }
    }
}
#endif
