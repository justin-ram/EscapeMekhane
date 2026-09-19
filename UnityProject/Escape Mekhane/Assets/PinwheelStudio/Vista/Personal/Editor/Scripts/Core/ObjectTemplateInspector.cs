#if VISTA
using Pinwheel.Vista;
using UnityEditor;
using UnityEngine;

namespace Pinwheel.VistaEditor
{
    [CustomEditor(typeof(ObjectTemplate))]
    public class ObjectTemplateInspector : Editor
    {
        private ObjectTemplate m_instance;
        private bool m_hasChanged;

        private void OnEnable()
        {
            m_instance = target as ObjectTemplate;
            m_hasChanged = false;
        }

        private void OnDisable()
        {
            if (m_hasChanged)
                RecordInspectorInteraction();
        }

        private static readonly GUIContent PREFAB = new GUIContent("Prefab", "The game object prefab");
        private static readonly GUIContent PREFAB_VARIANTS = new GUIContent("Variants", "Variants of the prefab");
        private static readonly GUIContent ALIGN_TO_NORMAL = new GUIContent("Align To Normal", "Align the instance up vector to surface normal vector");
        private static readonly GUIContent NORMAL_ALIGNMENT_ERROR = new GUIContent("Normal Alignment Error", "Adding variation to normal alignment");
        private static readonly string PROPS_ASSETS_TEXT = "Explore curated decorative assets";

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();

            GameObject prefab = m_instance.prefab;
            bool alignToNormal = m_instance.alignToNormal;
            float normalAlignmentError = m_instance.normalAlignmentError;

            EditorGUI.BeginChangeCheck();
            prefab = EditorGUILayout.ObjectField(PREFAB, m_instance.prefab, typeof(GameObject), false) as GameObject;
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(m_instance, $"Modify {m_instance.name}");
                m_instance.prefab = prefab;
                EditorUtility.SetDirty(m_instance);
                serializedObject.Update();
            }

            if (prefab != null)
            {
                if (TemplateUtils.IsVariantsSupported())
                {
                    EditorGUI.BeginChangeCheck();
                    SerializedProperty variantsProps = serializedObject.FindProperty("m_prefabVariants");
                    EditorGUILayout.PropertyField(variantsProps, PREFAB_VARIANTS, true);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(m_instance, $"Modify {m_instance.name}");
                        EditorUtility.SetDirty(m_instance);
                    }
                    serializedObject.ApplyModifiedProperties();
                    serializedObject.Update();
                    variantsProps.Dispose();
                }
            }

            if (!EditorCommon.IsPersonalEdition() &&
                EditorCommon.HasAssignedObjectReference(serializedObject.FindProperty("m_prefabVariants")))
            {
                EditorGUILayout.HelpBox(
                    "Object prefab variants are retained in this asset but no longer used during terrain population. Create one Object Template per prefab, then use Split Positions in the graph to distribute positions between their Object Output nodes.",
                    MessageType.Warning);
            }

            EditorGUI.BeginChangeCheck();
            alignToNormal = EditorGUILayout.Toggle(ALIGN_TO_NORMAL, m_instance.alignToNormal);
            if (alignToNormal)
            {
                normalAlignmentError = EditorGUILayout.Slider(NORMAL_ALIGNMENT_ERROR, m_instance.normalAlignmentError, 0f, 1f);
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(m_instance, $"Modify {m_instance.name}");
                m_instance.prefab = prefab;
                m_instance.alignToNormal = alignToNormal;
                m_instance.normalAlignmentError = normalAlignmentError;
                EditorUtility.SetDirty(m_instance);
                serializedObject.Update();
            }

            if (EditorGUILayout.LinkButton(PROPS_ASSETS_TEXT))
            {
                NetUtils.TrackClick("props-aff", UILocation.Inspector_ObjectTemplate);
                Application.OpenURL(Links.PROPS_ASSETS);
            }

            if (EditorGUI.EndChangeCheck())
                m_hasChanged = true;
        }

        private static void RecordInspectorInteraction()
        {
            SuccessfulActionCounter.Record(
                SuccessfulActionCounter.ActionKeys.ASSET_INSPECTOR_INTERACTION);
        }
    }
}
#endif
